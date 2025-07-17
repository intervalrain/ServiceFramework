using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using EdgeSync.ServiceFramework.Abstractions.JetStream;

using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.Contracts;
using EdgeSync.ServiceFramework.Enums;
using EdgeSync.ServiceFramework.Exceptions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NATS.Client.Services;
using NATS.Net;

namespace EdgeSync.ServiceFramework.Core;

/// <summary>
/// Represents a framework for managing NATS (NATS.io) service connections and operations.
/// </summary>
public abstract class ServiceHandler : MessageTransportBase, IHostedService
{
    private static readonly ConcurrentDictionary<Type, List<MethodInfo>> _endpointMethodsCache = new();
    private static readonly Regex SubjectParseRegex = new(@"^(?<protocol>[^\.]+)\.(?<groupID>[^\.]+)\.(?<deviceID>[^\.]+)\..*$", RegexOptions.Compiled);

    private readonly IJetStreamClient _client;
    private INatsSvcContext? _svcContext;
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
    /// New constructor with named connection support
    /// </summary>
    public ServiceHandler(
        ILogger<ServiceHandler> logger,
        IJetStreamClientFactory factory,
        string connectionName = "bus") : base(factory, connectionName)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = factory.CreateClient(connectionName);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _client.TryConnectAsync();
        if (_client.NatsConnection == null) throw new NullReferenceException();

        try
        {
            _svcContext = _client.NatsConnection.CreateServicesContext();
            _svcServer = await _svcContext.AddServiceAsync(new NatsSvcConfig(ServiceName, ServiceVersion)
            {
                QueueGroup = QueueGroup,
            }, cancellationToken);

            await RegisterEndpointsAsync(cancellationToken);

            Logger.LogInformation("Service {ServiceName} initialized", ServiceName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize service {ServiceName}", ServiceName);
            throw;
        }

    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_svcServer != null)
            {
                await _svcServer.StopAsync(cancellationToken);
                _svcServer = null;
            }
            
            _svcContext = null;
            
            Logger.LogInformation("Service {ServiceName} stopped", ServiceName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error stopping service {ServiceName}", ServiceName);
        }
    }

    /// <summary>
    /// Registers endpoints based on methods decorated with SubjectAttribute.
    /// </summary>
    protected async Task RegisterEndpointsAsync(CancellationToken cancellationToken = default)
    {
        if (_svcServer == null)
        {
            throw new InvalidOperationException("NATS service is not established");
        }

        var methods = _endpointMethodsCache.GetOrAdd(GetType(), t =>
            t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(m => m.GetCustomAttribute<SubjectAttribute>() != null)
                .ToList());

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

        await (Task)addEndpointMethod.Invoke(this,
        [
            endpointName,
            handler,
            subject,
            cancellationToken
        ])!;
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

        await _client.PublishAsync(subject, payload);
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
}