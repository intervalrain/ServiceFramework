using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.Contracts;
using EdgeSync.ServiceFramework.Enums;
using EdgeSync.ServiceFramework.Exceptions;
using EdgeSync.ServiceFramework.JetStream;

using Microsoft.Extensions.Logging;

using NATS.Client.Core;
using NATS.Client.Services;

namespace EdgeSync.ServiceFramework;

/// <summary>
/// Represents a framework for managing NATS (NATS.io) service connections and operations.
/// </summary>
public abstract class ServiceHandler : MessageTransportBase, IDisposable
{
    private static readonly ConcurrentDictionary<Type, List<MethodInfo>> _endpointMethodsCache = new();
    private static readonly Regex SubjectParseRegex = new(@"^(?<protocol>[^\.]+)\.(?<groupID>[^\.]+)\.(?<deviceID>[^\.]+)\..*$", RegexOptions.Compiled);

    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    private readonly object _disposeLock = new();
    private bool _disposed = false;

    /// <summary>
    /// NATS connection instance.
    /// </summary>
    private INatsConnection? _natsConnection;

    /// <summary>
    /// NATS service context instance.
    /// </summary>
    private INatsSvcContext? _svcContext;

    /// <summary>
    /// NATS service server instance.
    /// </summary>
    private INatsSvcServer? _svcServer;

    /// <summary>
    /// Gets the name of the service.
    /// </summary>
    public abstract string ServiceName { get; }

    /// <summary>
    /// Gets the version of the service.
    /// </summary>
    public abstract string ServiceVersion { get; }

    /// <summary>
    /// Gets the queue group for the service.
    /// </summary>
    public abstract string QueueGroup { get; }

    /// <summary>
    /// Gets the reconnection interval in milliseconds.
    /// </summary>
    public virtual int ReconnectionIntervalMs { get; } = 5000;

    public ILogger<ServiceHandler> Logger { get; }

    /// <summary>
    /// Legacy constructor for backward compatibility
    /// </summary>
    public ServiceHandler(
        ILogger<ServiceHandler> logger,
        INatsConnection connection,
        IBrokerJetStreamClient broker,
        IBusJetStreamClient bus) : base(broker, bus)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        DefaultLazy = new Lazy<IJetStreamClient>(() => bus); // Use bus as default for backward compatibility
        _natsConnection = DefaultLazy.Value.NatsConnection;
    }

    /// <summary>
    /// New constructor with named connection support
    /// </summary>
    public ServiceHandler(
        ILogger<ServiceHandler> logger,
        IJetStreamClientFactory factory,
        string connectionName = "Bus") : base(factory, connectionName)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await ConnectAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await DisconnectAsync();
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        if (_disposed) return;

        lock (_disposeLock)
        {
            if (_disposed) return;

            try
            {
                DisconnectAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during disposal");
            }
            finally
            {
                _connectionSemaphore?.Dispose();
                _disposed = true;
                GC.SuppressFinalize(this);
                base.Dispose();
            }
        }
    }

    private async Task InitializeServiceAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        try
        {
            if (_natsConnection == null)
                throw new InvalidOperationException("No connection to NATS");

            _svcContext = new NatsSvcContext(_natsConnection);
            await AddServiceAsync(cancellationToken);
            await RegisterEndpointsAsync(cancellationToken);
            Logger.LogInformation("Service {ServiceName} initialized", ServiceName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize service {ServiceName}", ServiceName);
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(ServiceConfig.MsgBusUrl))
        {
            Logger.LogError("ServiceConfig.MsgBusUrl is not set. Cannot initialize service");
            return;
        }

        await InitializeServiceAsync(cancellationToken);

        var reconnectAttempt = 0;
        const int maxReconnectAttempts = 100; // Allow for many reconnects as this is a background service

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_natsConnection != null && _natsConnection.ConnectionState != NatsConnectionState.Open)
                {
                    reconnectAttempt++;
                    Logger.LogWarning("NATS connection lost. Attempting to reconnect {ReconnectAttempt}/{MaxReconnects} for service {ServiceName}...", 
                        reconnectAttempt, maxReconnectAttempts, ServiceName);
                    
                    await DisconnectAsync();
                    await InitializeServiceAsync(cancellationToken);
                    
                    Logger.LogInformation("Service {ServiceName} successfully reconnected after {ReconnectAttempt} attempts", 
                        ServiceName, reconnectAttempt);
                    reconnectAttempt = 0; // Reset counter on successful reconnection
                }
                await Task.Delay(ReconnectionIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Logger.LogInformation("ExecuteAsync cancelled");
                break;
            }
            catch (Exception ex)
            {
                reconnectAttempt++;
                Logger.LogError(ex, "Error in ExecuteAsync for service {ServiceName} on attempt {ReconnectAttempt}/{MaxReconnects}", 
                    ServiceName, reconnectAttempt, maxReconnectAttempts);

                if (reconnectAttempt >= maxReconnectAttempts)
                {
                    Logger.LogError("Maximum reconnection attempts ({MaxReconnects}) reached for service {ServiceName}. Stopping service.", 
                        maxReconnectAttempts, ServiceName);
                    return;
                }

                await Task.Delay(ReconnectionIntervalMs, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Lists all subclasses of the ServiceHandler class.
    /// </summary>
    /// <returns>A list of Type objects representing the subclasses of ServiceHandler.</returns>
    public static List<Type> ListSubclasses()
    {
        var baseType = typeof(ServiceHandler);
        var assembly = baseType.Assembly;

        return assembly.GetTypes()
            .Where(type => type.IsSubclassOf(baseType) && !type.IsAbstract)
            .ToList();
    }

    /// <summary>
    /// Establishes a connection to the NATS server.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _connectionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected()) return;

            try
            {
                if (string.IsNullOrEmpty(ServiceConfig.MsgBusUrl))
                    throw new InvalidOperationException("ServiceConfig.MsgBusUrl is not configured");

                var opts = NatsOpts.Default with
                {
                    Url = ServiceConfig.MsgBusUrl,
                    AuthOpts = NatsAuthOpts.Default with { CredsFile = ServiceConfig.MsgBusCredFile }
                };

                _natsConnection = await NatsConnClient.CreateClientConnectionAsync(opts, Logger, cancellationToken: cancellationToken);
                _svcContext = new NatsSvcContext(_natsConnection);
                Logger.LogInformation("Connected to NATS server");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to create NATS client connection");
                throw;
            }
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Disconnects from the NATS server and disposes of resources.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_disposed) return;

        try
        {
            if (_svcServer != null)
            {
                await _svcServer.DisposeAsync();
                _svcServer = null;
            }

            _svcContext = null;

            if (_natsConnection != null)
            {
                await _natsConnection.DisposeAsync();
                _natsConnection = null;
            }

            Logger.LogInformation("Disconnected from NATS server");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to disconnect from NATS server");
        }
    }

    /// <summary>
    /// Checks if the service is connected to the NATS server.
    /// </summary>
    /// <returns>True if connected, otherwise false.</returns>
    public bool IsConnected()
    {
        return _natsConnection?.ConnectionState == NatsConnectionState.Open;
    }

    /// <summary>
    /// Handles statistics updates for a NATS service endpoint.
    /// </summary>
    /// <param name="endpoint">The NATS service endpoint.</param>
    /// <param name="node">The JSON node containing statistics data.</param>
    public void StatsHandler(INatsSvcEndpoint endpoint, JsonNode node)
    {
        Logger.LogInformation("NATS connection state changed for endpoint '{EndpointName}' to {State}",
            endpoint.Name, node);
    }

    /// <summary>
    /// Asynchronously adds a service to the NATS server with the specified configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the NATS connection is not established.</exception>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task AddServiceAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_svcContext == null)
        {
            throw new InvalidOperationException("NATS connection is not established");
        }

        var config = new NatsSvcConfig(ServiceName, ServiceVersion)
        {
            QueueGroup = QueueGroup
        };

        _svcServer = await _svcContext.AddServiceAsync(config, cancellationToken);
    }

    /// <summary>
    /// Adds an endpoint to the NATS server asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the endpoint data.</typeparam>
    /// <param name="endpointName">The name of the endpoint.</param>
    /// <param name="handler">The handler function for the endpoint.</param>
    /// <param name="customSubject">Optional custom subject pattern.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AddEndpointAsync<T>(
        string endpointName,
        Func<ServiceMsgContext<T>, T, ValueTask> handler,
        string? customSubject = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(endpointName);
        ArgumentNullException.ThrowIfNull(handler);

        if (_svcServer == null)
        {
            throw new InvalidOperationException("NATS service is not established");
        }

        var subject = customSubject ?? $"{ServiceName}.{ServiceVersion}.{endpointName}";

        try
        {
            await _svcServer.AddEndpointAsync<T>(
                name: endpointName,
                subject: subject,
                cancellationToken: cancellationToken,
                handler: async (msg) =>
                {
                    var svcMsgCtx = new ServiceMsgContext<T>
                    {
                        ServiceMsg = msg
                    };

                    try
                    {
                        if (msg.Data == null)
                        {
                            Logger.LogWarning("Received null data for endpoint '{EndpointName}'", endpointName);
                            return;
                        }

                        await handler(svcMsgCtx, msg.Data);
                    }
                    catch (ServiceHandlerException ex)
                    {
                        Logger.LogError(ex, "Service handler exception in endpoint '{EndpointName}': {Message}",
                            endpointName, ex.Message);
                        var responseModel = ex.ToResponseModel();
                        await ReplyAsync(svcMsgCtx, responseModel);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Unhandled exception in endpoint '{EndpointName}': {Message}",
                            endpointName, ex.Message);

                        TryParseSubject(svcMsgCtx.Subject, out var protoVer, out var groupId, out var deviceId);

                        var responseModel = new ServiceResponseModelDto
                        {
                            Cmd = endpointName,
                            SeqId = 0,
                            ReqSeqId = ExtractRequestSequenceId(msg) ?? string.Empty,
                            RspSeqId = Guid.NewGuid().ToString(),
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            data = new ServiceResponseDataModelDto
                            {
                                GroupID = groupId,
                                DeviceUID = deviceId,
                                Target = "dm",
                                Result = new ServiceResultModelDto
                                {
                                    Code = (int)ServiceResultCode.InternalServerError,
                                    Message = $"Internal service error: {ex.Message}"
                                }
                            }
                        };

                        await ReplyAsync(svcMsgCtx, responseModel);
                    }
                });

            Logger.LogInformation("Added endpoint '{EndpointName}' for service '{ServiceName}'",
                endpointName, ServiceName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to add endpoint '{EndpointName}' for service '{ServiceName}'",
                endpointName, ServiceName);
            throw;
        }
    }

    /// <summary>
    /// Sends a reply message asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the service message context.</typeparam>
    /// <typeparam name="TR">The type of the reply message data.</typeparam>
    /// <param name="svcMsgCtx">The service message context.</param>
    /// <param name="msg">The reply message data.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ReplyAsync<T, TR>(ServiceMsgContext<T> svcMsgCtx, TR msg)
    {
        ArgumentNullException.ThrowIfNull(svcMsgCtx);
        ArgumentNullException.ThrowIfNull(msg);

        var replyMsg = JsonSerializer.Serialize(msg, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        });

        Logger.LogDebug("Replying to message '{Subject}' with data length: {Length}",
            svcMsgCtx.ServiceMsg.Subject, replyMsg.Length);

        await svcMsgCtx.ServiceMsg.ReplyAsync(replyMsg);
    }

    /// <summary>
    /// Sends an error reply message asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the error data.</typeparam>
    /// <param name="svcMsg">The service message object.</param>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="data">The error data.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ReplyErrorAsync<T>(NatsSvcMsg<T> svcMsg, int code, string message, T data)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            await svcMsg.ReplyErrorAsync(code, message, data);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to send error reply: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Publishes a message to the specified subject.
    /// </summary>
    /// <param name="subject">The subject to publish to.</param>
    /// <param name="payload">The message payload.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task PublishMsgAsync(string subject, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(payload);
        ThrowIfDisposed();

        if (_natsConnection == null)
        {
            throw new InvalidOperationException("NATS connection is not established");
        }

        await _natsConnection.PublishAsync(subject, payload);
    }

    /// <summary>
    /// Registers endpoints based on methods decorated with SubjectAttribute.
    /// </summary>
    protected async Task RegisterEndpointsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_svcServer == null)
        {
            throw new InvalidOperationException("NATS service is not established");
        }

        var methods = GetEndpointMethods();

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<SubjectAttribute>();
            if (attr == null) continue;

            var endpointName = attr.EndpointName;
            var subject = attr.CustomSubject ?? $"{ServiceName}.{ServiceVersion}.{endpointName}";

            try
            {
                await RegisterSingleEndpointAsync(method, endpointName, subject, cancellationToken);
                Logger.LogInformation("Registered endpoint '{EndpointName}' with subject '{Subject}'",
                    endpointName, subject);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to register endpoint '{EndpointName}'", endpointName);
                throw;
            }
        }
    }

    private List<MethodInfo> GetEndpointMethods()
    {
        var type = GetType();
        return _endpointMethodsCache.GetOrAdd(type, t =>
            t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(m => m.GetCustomAttribute<SubjectAttribute>() != null)
                .ToList());
    }

    private async Task RegisterSingleEndpointAsync(MethodInfo method, string endpointName, string subject, CancellationToken cancellationToken)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != 2)
        {
            throw new InvalidOperationException(
                $"Method '{method.Name}' must have exactly two parameters: ServiceMsgContext<T> and T");
        }

        var ctxParamType = parameters[0].ParameterType;
        if (!ctxParamType.IsGenericType ||
            ctxParamType.GetGenericTypeDefinition() != typeof(ServiceMsgContext<>))
        {
            throw new InvalidOperationException(
                $"Method '{method.Name}' first parameter must be of type ServiceMsgContext<T>");
        }

        var msgType = ctxParamType.GetGenericArguments()[0];
        var dataParamType = parameters[1].ParameterType;
        if (dataParamType != msgType)
        {
            throw new InvalidOperationException(
                $"Method '{method.Name}' second parameter type '{dataParamType}' must match T from ServiceMsgContext<T>");
        }

        if (method.ReturnType != typeof(ValueTask))
        {
            throw new InvalidOperationException($"Method '{method.Name}' must return ValueTask");
        }

        var handlerType = typeof(Func<,,>).MakeGenericType(
            typeof(ServiceMsgContext<>).MakeGenericType(msgType),
            msgType,
            typeof(ValueTask));
        var handler = Delegate.CreateDelegate(handlerType, this, method);

        var addEndpointMethod = typeof(ServiceHandler)
            .GetMethod(nameof(AddEndpointAsync))?
            .MakeGenericMethod(msgType)
            ?? throw new InvalidOperationException("Unexpected error: AddEndpointAsync method not found");

        await (Task)addEndpointMethod.Invoke(this, new object[]
        {
            endpointName,
            handler,
            subject,
            cancellationToken
        })!;
    }

    /// <summary>
    /// Attempts to parse a subject string into its components.
    /// </summary>
    public static bool TryParseSubject(string input, out string? protoVer, out string? groupId, out string? deviceId)
    {
        protoVer = null;
        groupId = null;
        deviceId = null;

        if (string.IsNullOrEmpty(input))
            return false;

        var match = SubjectParseRegex.Match(input);

        if (match.Success)
        {
            protoVer = match.Groups["protocol"].Value;
            groupId = match.Groups["groupID"].Value;
            deviceId = match.Groups["deviceID"].Value;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Parses subject and message into a DTO with proper error handling.
    /// </summary>
    /// <typeparam name="T">The type of DTO to create.</typeparam>
    /// <param name="svcMsgCtx">The service message context.</param>
    /// <param name="message">The raw message string.</param>
    /// <param name="command">The command name for error reporting.</param>
    /// <returns>The parsed DTO.</returns>
    /// <exception cref="ServiceHandlerException">Thrown if subject format is invalid or parsing fails.</exception>
    public T ParseApiRequest<T>(ServiceMsgContext<string> svcMsgCtx, string message, string command)
        where T : class, IServiceBasicDto
    {
        ArgumentNullException.ThrowIfNull(svcMsgCtx);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(command);

        if (!TryParseSubject(svcMsgCtx.Subject, out var protoVer, out var groupId, out var deviceId))
        {
            throw new ServiceHandlerException(
                (int)ServiceResultCode.ServiceResultInvalidSubject,
                $"Invalid format of subject: {svcMsgCtx.Subject}",
                command
            )
            {
                ReqSeqId = string.Empty
            };
        }

        try
        {
            var method = typeof(T).GetMethod("FromMessage",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
                null,
                [typeof(byte[])],
                null);

            if (method == null)
            {
                throw new ServiceHandlerException(
                    (int)ServiceResultCode.ServiceResultDeserializationError,
                    $"Failed to find method to deserialize message {typeof(T).Name}",
                    command,
                    groupId,
                    deviceId
                )
                {
                    ReqSeqId = string.Empty
                };
            }

            var dto = method.Invoke(null, [Encoding.UTF8.GetBytes(message)]) as T;
            if (dto == null)
            {
                var truncatedMessage = message.Length > 200 ? message[..200] + "..." : message;
                Logger.LogError("Failed to deserialize message {TypeName}. Message: {Message}",
                    typeof(T).Name, truncatedMessage);

                throw new ServiceHandlerException(
                    (int)ServiceResultCode.ServiceResultDeserializationError,
                    $"Failed to deserialize message {typeof(T).Name}",
                    command,
                    groupId,
                    deviceId
                )
                {
                    ReqSeqId = string.Empty
                };
            }

            dto.ProtoVer = protoVer!;
            dto.GroupId = groupId!;
            dto.DeviceId = deviceId!;

            return dto;
        }
        catch (TargetInvocationException tex)
        {
            Logger.LogError(tex, "Inner exception in FromMessage: {Message}", tex.InnerException?.Message);
            throw new ServiceHandlerException(
                (int)ServiceResultCode.InternalServerError,
                $"Error in FromMessage: {tex.InnerException?.Message}",
                command,
                groupId,
                deviceId
            )
            {
                ReqSeqId = string.Empty
            };
        }
        catch (ServiceHandlerException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error parsing message: {Message}", ex.Message);
            throw new ServiceHandlerException(
                (int)ServiceResultCode.InternalServerError,
                $"Error parsing message: {ex.Message}",
                command,
                groupId,
                deviceId
            )
            {
                ReqSeqId = string.Empty
            };
        }
    }

    private static string? ExtractRequestSequenceId<T>(NatsSvcMsg<T> msg)
    {
        // Implementation depends on your message format
        // This is a placeholder - implement based on your actual message structure
        return null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ServiceHandler));
        }
    }
}