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

using ShadowAgent.Infrastructure.Models;

namespace EdgeSync.ServiceFramework;

/// <summary>
/// Represents a framework for managing NATS (NATS.io) service connections and operations.
/// </summary>
public abstract class ServiceHandler : MessageTransportBase, IDisposable
{
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
    /// Gets or sets the name of the service.
    /// </summary>
    public abstract string ServiceName { get; }

    /// <summary>
    /// Gets or sets the version of the service.
    /// </summary>
    public abstract string ServiceVersion { get; }

    /// <summary>
    /// Gets or sets the queue group for the service.
    /// </summary>
    public abstract string QueueGroup { get; }

    public ILogger<ServiceHandler> Logger { get; }
    
    public ServiceHandler(
        ILogger<ServiceHandler> logger,
        INatsConnection connection,
        IBrokerJetStreamClient broker,
        IBusJetStreamClient bus) : base(broker, bus)
    {
        Logger = logger;
        _natsConnection = connection;
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
        DisconnectAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
        base.Dispose();
    }

    private async Task InitializeServiceAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_natsConnection == null) throw new NatsException("No connection to NATS");
            _svcContext = new NatsSvcContext(_natsConnection);
            await AddServiceAsync(cancellationToken);
            await RegisterEndpointsAsync(cancellationToken);
            Logger.LogInformation("Service {ServiceName} initialized.", ServiceName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize service {ServiceName}", ServiceName);
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await InitializeServiceAsync(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_natsConnection != null && _natsConnection.ConnectionState != NatsConnectionState.Open)
                {
                    Logger.LogInformation("NATS connection lost. Attempting te reconnect...");
                    await InitializeServiceAsync(cancellationToken);
                }
                await Task.Delay(5000, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in ExecuteAsync");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Lists all subclasses of the ServiceHandler class.
    /// </summary>
    /// <returns>A list of Type objects representing the subclasses of ServiceHandler.</returns>
    public static List<Type> ListSubclasses()
    {
        var subclasses = new List<Type>();
        var baseType = typeof(ServiceHandler);
        var assembly = baseType.Assembly;

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsSubclassOf(baseType))
            {
                subclasses.Add(type);
            }
        }

        return subclasses;
    }

    /// <summary>
    /// Establishes a connection to the NATS server.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected()) return;
        try
        {
            NatsOpts opts = NatsOpts.Default with
            {
                Url = ServiceConfig.MsgBusUrl,
                AuthOpts = NatsAuthOpts.Default with { CredsFile = ServiceConfig.MsgBusCredFile }
            };
            _natsConnection = await NatsConnClient.CreateClientConnectionAsync(opts, cancellationToken: cancellationToken);
            _svcContext = new NatsSvcContext(_natsConnection);
            Logger.LogInformation("Connected to NATS server.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create NATS client connection.");
            throw;
        }
    }

    /// <summary>
    /// Disconnects from the NATS server and disposes of resources.
    /// </summary>
    public async Task DisconnectAsync()
    {
        try
        {
            if (_svcServer != null)
            {
                await _svcServer.DisposeAsync();
                _svcServer = null;
            }
            if (_natsConnection != null)
            {
                await _natsConnection.DisposeAsync();
                _natsConnection = null;
            }
            Logger.LogInformation("Disconnected from NATS servet");
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
        return _natsConnection != null && _natsConnection.ConnectionState == NatsConnectionState.Open;
    }

    /// <summary>
    /// Handles statistics updates for a NATS service endpoint.
    /// </summary>
    /// <param name="endpoint">The NATS service endpoint.</param>
    /// <param name="node">The JSON node containing statistics data.</param>
    public void StatsHandler(INatsSvcEndpoint endpoint, JsonNode node)
    {
        Logger.LogInformation($"Nats connection state changed for endpoint '{endpoint.Name}' to {node}");
    }

    /// <summary>
    /// Handles service messages.
    /// </summary>
    // public virtual async ValueTask ServiceHandler<T>(object svcMsgObj, T msg)
    // {
    //     // TODO: Implement the service handler logic here.
    //     // This method is intended to handle incoming service messages.
    //     // For now, we will simulate some asynchronous work.
    //     await Task.CompletedTask;
    // }

    /// <summary>
    /// Asynchronously adds a service to the NATS server with the specified configuration.
    /// </summary>
    /// <exception cref="Exception">Thrown when the NATS connection is not established.</exception>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task AddServiceAsync(CancellationToken cancellationToken = default)
    {
        if (_svcContext == null)
        {
            throw new Exception("Nats connection is not established.");
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
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AddEndpointAsync<T>(string endpointName, Func<ServiceMsgContext<T>, T, ValueTask> handler, string? customSubject = null, CancellationToken cancellationToken = default)
    {
        if (_svcServer == null)
        {
            throw new Exception("Nats connection is not established.");
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
                        await handler(svcMsgCtx, msg.Data!);
                    }
                    catch (ServiceHandlerException ex)
                    {
                        // Handle service handler exceptions
                        Logger.LogError(ex, $"Service handler exception in endpoint '{endpointName}': {ex.Message}");
                        var responseModel = ex.ToResponseModel();
                        await ReplyAsync(svcMsgCtx, responseModel);
                    }
                    catch (Exception ex)
                    {
                        // Handle generic exceptions
                        Logger.LogError(ex, $"Unhandled exception in endpoint '{endpointName}': {ex.Message}");

                        // Extract information from the subject if possible

                        TryParseSubject(svcMsgCtx.Subject, out var protoVer, out var groupId, out var deviceId);

                        var responseModel = new ServiceResponseModelDto
                        {
                            Cmd = endpointName,
                            SeqId = 0, // This should ideally be set to a meaningful value
                            ReqSeqId = string.Empty, // This should ideally be extracted from the request
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
            Logger.LogInformation($"Added endpoint '{endpointName}' for service '{ServiceName}'");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Failed to add endpoint '{endpointName}' for service '{ServiceName}'");
        }

    }

    /// <summary>
    /// Sends a reply message asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the reply message data.</typeparam>
    /// <param name="svcMsgObj">The service message object.</param>
    /// <param name="msg">The reply message data.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ReplyAsync<T, TR>(ServiceMsgContext<T> svcMsgCtx, TR msg)
    {
        var replyMsg = JsonSerializer.Serialize(msg);
        Logger.LogInformation($"Replying to message '{svcMsgCtx.ServiceMsg.Subject}' with '{replyMsg}'");
        await svcMsgCtx.ServiceMsg.ReplyAsync(replyMsg);
    }

    /// <summary>
    /// Sends an error reply message asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the error data.</typeparam>
    /// <param name="svcMsgObj">The service message object.</param>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="data">The error data.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ReplyErrorAsync<T>(object svcMsgObj, int code, string message, T data)
    {
        if (svcMsgObj is NatsSvcMsg<T> svcMsg)
        {
            await svcMsg.ReplyErrorAsync(code, message, data);
        }
        else
        {
            Logger.LogError($"{ServiceName}: Invalid service message object type.");
            throw new InvalidCastException($"{ServiceName}: message object is not of type NatsSvcMsg<T>.");
        }
    }

    public async Task PublishMsgAsync(string subject, byte[] payload)
    {
        if (_natsConnection == null)
        {
            throw new InvalidOperationException("NATS connection is not established");
        }

        await _natsConnection.PublishAsync(subject, payload);
    }

    // todo: add generic type call back
    protected async Task RegisterEndpointsAsync(CancellationToken cancellationToken = default)
    {
        if (_svcServer == null)
        {
            throw new InvalidOperationException("NATS service is not established");
        }

        var methods = GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(m => m.GetCustomAttributes(typeof(ServiceHandlerAttribute), false).Length > 0);

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<ServiceHandlerAttribute>();
            if (attr == null) continue;

            var endpointName = attr.EndpointName;
            var subject = attr.CustomSubject ?? $"{ServiceName}.{ServiceVersion}.{endpointName}";

            try
            {
                var parameters = method.GetParameters();
                if (parameters.Length != 2)
                {
                    throw new InvalidOperationException($"Method '{method.Name}' must have exactly two parameters: ServiceMsgContex<T> and T.");
                }

                // 檢查第一個參數是否為 ServiceMsgContex<T>
                var ctxParamType = parameters[0].ParameterType;
                if (!ctxParamType.IsGenericType || ctxParamType.GetGenericTypeDefinition() != typeof(ServiceMsgContext<>))
                {
                    throw new InvalidOperationException($"Method '{method.Name}' first parameter must be of type ServiceMsgContex<T>.");
                }

                // 獲取 T 的類型
                var msgType = ctxParamType.GetGenericArguments()[0];
                var dataParamType = parameters[1].ParameterType;
                if (dataParamType != msgType)
                {
                    throw new InvalidOperationException($"Method '{method.Name}' second parameter type '{dataParamType}' must match T from ServiceMsgContex<T>.");
                }

                // 檢查返回值
                if (method.ReturnType != typeof(ValueTask))
                {
                    throw new InvalidOperationException($"Method '{method.Name}' must return ValueTask.");
                }

                // 創建委派
                var handlerType = typeof(Func<,,>).MakeGenericType(
                    typeof(ServiceMsgContext<>).MakeGenericType(msgType),
                    msgType,
                    typeof(ValueTask));
                var handler = Delegate.CreateDelegate(handlerType, this, method);

                // 調用 AddEndpointAsync
                var addEndpointMethod = typeof(ServiceHandler).GetMethod(nameof(AddEndpointAsync))
                    ?.MakeGenericMethod(msgType) ?? throw new Exception("Unexpected error");

                await (Task)addEndpointMethod.Invoke(this,
                [
                    endpointName,
                    handler,
                    subject,
                    cancellationToken
                ])!;

                Logger.LogInformation($"Registered endpoint '{endpointName}' with subject '{subject}'");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Failed to register endpoint '{endpointName}'");
                throw;
            }
        }
    }

    public static bool TryParseSubject(string input, out string? protoVer, out string? groupId, out string? deviceId)
    {
        protoVer = null;
        groupId = null;
        deviceId = null;

        Regex regex = new Regex(@"^(?<protocol>[^\.]+)\.(?<groupID>[^\.]+)\.(?<deviceID>[^\.]+)\..*$", RegexOptions.Compiled);
        var match = regex.Match(input);

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
    /// Parses subject and message into a DTO with proper error handling
    /// </summary>
    /// <typeparam name="T">The type of DTO to create</typeparam>
    /// <param name="svcMsgCtx">The service message context</param>
    /// <param name="message">The raw message string</param>
    /// <param name="command">The command name for error reporting</param>
    /// <returns>The parsed DTO</returns>
    /// <exception cref="ServiceHandlerException">Thrown if subject format is invalid or parsing fails</exception>
    public T ParseApiRequest<T>(ServiceMsgContext<string> svcMsgCtx, string message, string command) where T : class, IServiceBasicDto
    {
        // Parse subject
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
            // Parse message into DTO
            var method = typeof(T).GetMethod("FromMessage", 
                BindingFlags.Public | BindingFlags.Static| BindingFlags.FlattenHierarchy,
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
                Logger.LogError($"Failed to deserialize message {typeof(T).Name}. {message}");
                Logger.LogDebug($"Attempting to parse message for {typeof(T).Name}: {message.Substring(0, Math.Min(message.Length, 200))}");
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

            // Set subject info on DTO
            dto.ProtoVer = protoVer!;
            dto.GroupId = groupId!;
            dto.DeviceId = deviceId!;

            return dto;
        }
        catch (TargetInvocationException tex)
        {
            Logger.LogError($"Inner exception in FromBytes: {tex.InnerException?.Message}, Stack trace: {tex.InnerException?.StackTrace}");
            throw new ServiceHandlerException(
                (int)ServiceResultCode.InternalServerError,
                $"Error in FromBytes: {tex.InnerException?.Message}",
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
            // Re-throw ServiceHandlerException as is
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Inner exception in FromBytes: {ex.InnerException?.Message}, Stack trace: {ex.InnerException?.StackTrace}");
            // Wrap other exceptions
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
}