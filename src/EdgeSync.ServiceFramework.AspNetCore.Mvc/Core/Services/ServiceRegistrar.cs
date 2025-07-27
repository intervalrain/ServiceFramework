using System.Reflection;
using EdgeSync.ServiceFramework.Abstractions;
using EdgeSync.ServiceFramework.Abstractions.Attributes;
using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Core.Serializers;
using EdgeSync.ServiceFramework.Data;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.Services;
using ProtobufEmpty = Google.Protobuf.WellKnownTypes.Empty;
using EmptyMessage = Google.Protobuf.WellKnownTypes.Empty;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;

/// <summary>
/// Service registration implementation for NATS request-response services
/// Extracted from ServiceFrameworkBackgroundService to follow SRP
/// Handles service server creation and endpoint registration
/// </summary>
public class ServiceRegistrar : IServiceRegistrar
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectionResolver _connectionResolver;
    private readonly ISerializerAdapterFactory _serializerAdapterFactory;
    private readonly AutoConventionOptions _options;
    private readonly ServiceFrameworkOptions _serviceFrameworkOptions;
    private readonly ILogger<ServiceRegistrar> _logger;
    private readonly List<INatsSvcServer> _serviceServers = [];

    public ServiceRegistrar(
        IServiceProvider serviceProvider,
        IConnectionResolver connectionResolver,
        ISerializerAdapterFactory serializerAdapterFactory,
        IOptions<AutoConventionOptions> options,
        IOptions<ServiceFrameworkOptions> serviceFrameworkOptions,
        ILogger<ServiceRegistrar> logger)
    {
        _serviceProvider = serviceProvider;
        _connectionResolver = connectionResolver;
        _serializerAdapterFactory = serializerAdapterFactory;
        _options = options.Value;
        _serviceFrameworkOptions = serviceFrameworkOptions.Value;
        _logger = logger;
    }

    public async Task<ServiceRegistrationResult> RegisterRequestResponseServicesAsync(
        List<(Type ServiceType, List<NatsMethodInfo> Methods)> reqrspServices, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting registration of {ServiceCount} request-response services", reqrspServices.Count);

        var result = new ServiceRegistrationResult();

        try
        {
            foreach (var (serviceType, methods) in reqrspServices)
            {
                // Group by both ServiceName and ChannelName to handle different connections
                var groupedReqRspMethods = methods.GroupBy(m => new
                {
                    ServiceName = m.ServiceName,
                    ChannelName = GetChannelName(serviceType, m)
                });

                foreach (var group in groupedReqRspMethods)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var serviceName = group.Key.ServiceName;
                    var channelName = group.Key.ChannelName;
                    var groupMethods = group.ToList();

                    var connection = await _connectionResolver.GetConnectionAsync(channelName);
                    if (connection == null)
                    {
                        _logger.LogWarning("Cannot register service {ServiceName} - connection unavailable for channel {ChannelName}", 
                            serviceName, channelName);
                        result.FailedServices.Add((serviceType, groupMethods, $"Connection unavailable for channel {channelName}"));
                        continue;
                    }

                    try
                    {
                        var svcServer = await CreateSvcServer(serviceName, groupMethods, connection, cancellationToken);
                        await SetupRequestResponseServiceGroup(connection, svcServer, serviceType, serviceName, groupMethods, cancellationToken);
                        
                        result.RegisteredServices.Add((serviceType, groupMethods, svcServer));
                        _logger.LogInformation("Registered request-response service '{ServiceName}' with {MethodCount} methods during startup",
                            serviceName, groupMethods.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to register service {ServiceName}: {Message}", serviceName, ex.Message);
                        result.FailedServices.Add((serviceType, groupMethods, ex.Message));
                    }
                }
            }

            _logger.LogInformation("Service registration completed. Registered {RegisteredCount} services, failed {FailedCount}",
                result.RegisteredServices.Count, result.FailedServices.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during service registration");
            throw;
        }
    }

    public async Task<INatsSvcServer> CreateSvcServer(string serviceName, List<NatsMethodInfo> methods, INatsConnection connection, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Setting up request-response service '{ServiceName}' with {MethodCount} methods",
            serviceName, methods.Count);

        // Get service info from the first method's service type (they should all be from the same service)
        var serviceType = methods.First().Method.DeclaringType;
        var serviceInfo = GetServiceInfo(serviceType!, serviceName);

        // create server
        _logger.LogInformation("Setting up NATS service group for request-response: '{ServiceName}' version '{ServiceVersion}' with {MethodCount} methods on connection {ConnectionId}",
            serviceInfo.ServiceName, serviceInfo.ServiceVersion, methods.Count, connection.ServerInfo?.ClientId);

        // Create service context
        var svcContext = new NatsSvcContext(connection);

        // Create service configuration using the service info
        var config = new NatsSvcConfig(serviceInfo.ServiceName, serviceInfo.ServiceVersion)
        {
            QueueGroup = serviceInfo.QueueGroup
        };

        // Add service to context
        var svcServer = await svcContext.AddServiceAsync(config, cancellationToken);
        _serviceServers.Add(svcServer);

        return svcServer;
    }

    public async Task SetupRequestResponseServiceGroup(INatsConnection connection, INatsSvcServer svcServer, Type serviceType, string serviceName, List<NatsMethodInfo> methods, CancellationToken cancellationToken)
    {
        try
        {
            // Add all endpoints for this service
            foreach (var methodInfo in methods)
            {
                var methodParams = methodInfo.Method.GetParameters();
                var endpointName = methodInfo.SubjectAttribute?.EndpointName ?? methodInfo.Method.Name.ToLower().Replace("async", "");

                if (methodParams.Length > 0)
                {
                    var requestType = methodParams[0].ParameterType;

                    // Create and add endpoint using reflection to handle generic types
                    var addEndpointMethod = GetType().GetMethod(nameof(AddServiceEndpoint), BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.MakeGenericMethod(requestType);

                    if (addEndpointMethod != null)
                    {
                        await (Task)addEndpointMethod.Invoke(this,
                            [svcServer, serviceType, methodInfo, endpointName, connection, cancellationToken])!;
                    }
                }
                else
                {
                    // Handle parameterless methods (like GetListAsync)
                    await AddParameterlessServiceEndpoint(svcServer, serviceType, methodInfo, endpointName, connection, cancellationToken);
                }
            }

            _logger.LogInformation("Successfully set up NATS service group: '{ServiceName}' with {MethodCount} methods on connection {ConnectionId}",
                serviceName, methods.Count, connection.ServerInfo?.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup NATS service group: '{ServiceName}' on connection {ConnectionId}",
                serviceName, connection.ServerInfo?.ClientId);
            throw;
        }
    }

    public async Task<ServiceShutdownResult> StopAllServicesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping {ServiceCount} NATS service servers", _serviceServers.Count);

        var result = new ServiceShutdownResult();

        foreach (var serviceServer in _serviceServers)
        {
            try
            {
                var serviceName = serviceServer.GetInfo().Name ?? "Unknown";
                _logger.LogDebug("Stopping NATS service server: {ServiceName}", serviceName);
                
                await serviceServer.StopAsync(cancellationToken);
                await serviceServer.DisposeAsync();
                
                result.StoppedServices.Add(serviceName);
            }
            catch (Exception ex)
            {
                var serviceName = serviceServer.GetInfo().Name ?? "Unknown";
                _logger.LogError(ex, "Error stopping/disposing service server {ServiceName}", serviceName);
                result.FailedServices.Add((serviceName, ex.Message));
            }
        }

        _serviceServers.Clear();

        _logger.LogInformation("Service shutdown completed. Stopped {StoppedCount} services, failed {FailedCount}",
            result.StoppedServices.Count, result.FailedServices.Count);

        return result;
    }

    private async Task AddServiceEndpoint<T>(INatsSvcServer svcServer, Type serviceType, NatsMethodInfo methodInfo, string endpointName, INatsConnection connection, CancellationToken cancellationToken) where T : class
    {
        _logger.LogInformation("Adding NATS service endpoint: {EndpointName} for method: {Method} on service: {ServiceType}",
            endpointName, methodInfo.Method.Name, serviceType.Name);

        var channelName = GetChannelName(serviceType, methodInfo);
        var serializerRegistry = GetSerializerForConnection(channelName);
        var subject = GetSubject(methodInfo);

        await svcServer.AddEndpointAsync(
            name: endpointName,
            serializer: serializerRegistry.GetDeserializer<T>(),
            subject: subject,
            handler: async (NatsSvcMsg<T> m) =>
            {
                // Handle exceptions which may occur during message processing
                if (m.Exception != null)
                {
                    await m.ReplyErrorAsync(500, m.Exception.Message);
                    return;
                }

                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var serviceInstance = scope.ServiceProvider.GetService(serviceType);

                    if (serviceInstance != null)
                    {
                        // Extract request data and audit info when EnableAuditWrapper is true
                        object? requestData = m.Data;
                        string? userId = null;
                        string? tenantId = null;
                        string? correlationId = null;
                        Guid reqSeqId = Guid.NewGuid();

                        if (_options.EnableAuditWrapper && m.Data != null)
                        {
                            var dataType = m.Data.GetType();
                            if (dataType.IsGenericType && dataType.GetGenericTypeDefinition() == typeof(RequestDto<>))
                            {
                                // Extract audit info from RequestDto
                                var reqSeqIdProp = dataType.GetProperty("ReqSeqId");
                                var userIdProp = dataType.GetProperty("UserId");
                                var tenantIdProp = dataType.GetProperty("TenantId");
                                var correlationIdProp = dataType.GetProperty("CorrelationId");
                                var dataProp = dataType.GetProperty("Data");

                                reqSeqId = (Guid)(reqSeqIdProp?.GetValue(m.Data) ?? reqSeqId);
                                userId = userIdProp?.GetValue(m.Data) as string;
                                tenantId = tenantIdProp?.GetValue(m.Data) as string;
                                correlationId = correlationIdProp?.GetValue(m.Data) as string;
                                requestData = dataProp?.GetValue(m.Data);
                            }
                        }

                        // Invoke the actual service method
                        var result = methodInfo.Method.Invoke(serviceInstance, [requestData]);

                        // Handle async methods
                        if (result is Task task)
                        {
                            await task;

                            // If the task has a result, get it and reply
                            if (task.GetType().IsGenericType)
                            {
                                var resultValue = task.GetType().GetProperty("Result")?.GetValue(task);
                                if (resultValue != null)
                                {
                                    await ReplyWithAuditWrapper(m, resultValue, reqSeqId, userId, tenantId, correlationId);
                                }
                            }
                        }
                        else
                        {
                            // Handle synchronous methods
                            if (result != null)
                            {
                                await ReplyWithAuditWrapper(m, result, reqSeqId, userId, tenantId, correlationId);
                            }
                        }
                    }
                    else
                    {
                        await m.ReplyErrorAsync(500, $"Service instance not found for type {serviceType.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing request-response for endpoint {EndpointName}: {Message}",
                        endpointName, ex.Message);
                    await m.ReplyErrorAsync(500, ex.Message);
                }
            },
            cancellationToken: cancellationToken);

        _logger.LogInformation("Successfully added NATS service endpoint: {EndpointName} for method: {Method} on service: {ServiceType}",
            endpointName, methodInfo.Method.Name, serviceType.Name);
    }

    private async Task AddParameterlessServiceEndpoint(INatsSvcServer svcServer, Type serviceType, NatsMethodInfo methodInfo, string endpointName, INatsConnection connection, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adding parameterless NATS service endpoint: {EndpointName} for method: {Method} on service: {ServiceType}",
            endpointName, methodInfo.Method.Name, serviceType.Name);

        var channelName = GetChannelName(serviceType, methodInfo);
        var serializerRegistry = GetSerializerForConnection(channelName);
        var subject = GetSubject(methodInfo);
        
        // Get the appropriate adapter for this serializer
        var adapter = _serializerAdapterFactory.GetAdapter(serializerRegistry);
        var endpointConfig = adapter.GetEndpointConfig(serializerRegistry);
        
        _logger.LogDebug("Using serializer adapter: {AdapterType} for endpoint: {EndpointName}", 
            adapter.GetType().Name, endpointName);

        // Use the adapter's configuration to determine the handler type
        var handlerType = endpointConfig.ParameterlessHandlerType;
        
        if (handlerType == typeof(ProtobufEmpty))
        {
            // Use Empty message for serializers that require it (like Protobuf)
            await svcServer.AddEndpointAsync<ProtobufEmpty>(
                name: endpointName,
                serializer: serializerRegistry.GetDeserializer<ProtobufEmpty>(),
                subject: subject,
                handler: async (NatsSvcMsg<ProtobufEmpty> m) =>
                {
                    await HandleParameterlessEndpoint(m, serviceType, methodInfo, endpointName, adapter, isEmptyMessage: true);
                },
                cancellationToken: cancellationToken);
        }
        else
        {
            // Use object type for serializers that can handle it (like JSON)
            await svcServer.AddEndpointAsync<object>(
                name: endpointName,
                serializer: serializerRegistry.GetDeserializer<object>(),
                subject: subject,
                handler: async (NatsSvcMsg<object> m) =>
                {
                    await HandleParameterlessEndpoint(m, serviceType, methodInfo, endpointName, adapter, isEmptyMessage: false);
                },
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation("Successfully added parameterless NATS service endpoint: {EndpointName} for method: {Method} on service: {ServiceType}",
            endpointName, methodInfo.Method.Name, serviceType.Name);
    }

    private async Task HandleParameterlessEndpoint<T>(NatsSvcMsg<T> m, Type serviceType, NatsMethodInfo methodInfo, string endpointName, ISerializerAdapter adapter, bool isEmptyMessage) where T : class
    {
        // Handle exceptions which may occur during message processing
        if (m.Exception != null)
        {
            await m.ReplyErrorAsync(500, m.Exception.Message);
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var serviceInstance = scope.ServiceProvider.GetService(serviceType);

            if (serviceInstance != null)
            {
                // For parameterless methods, we still need to handle audit info
                Guid reqSeqId = Guid.NewGuid();
                string? userId = null;
                string? tenantId = null;
                string? correlationId = null;

                // Invoke the parameterless service method
                var result = methodInfo.Method.Invoke(serviceInstance, []);

                // Handle async methods
                if (result is Task task)
                {
                    await task;

                    // If the task has a result, get it and reply
                    if (task.GetType().IsGenericType)
                    {
                        var resultValue = task.GetType().GetProperty("Result")?.GetValue(task);
                        if (resultValue != null)
                        {
                            if (isEmptyMessage)
                            {
                                await ReplyWithEmptyMessage(m, resultValue, reqSeqId, userId, tenantId, correlationId);
                            }
                            else
                            {
                                await ReplyWithAuditWrapper(m, resultValue, reqSeqId, userId, tenantId, correlationId);
                            }
                        }
                        else if (isEmptyMessage)
                        {
                            await m.ReplyAsync(new EmptyMessage());
                        }
                    }
                    else if (isEmptyMessage)
                    {
                        await m.ReplyAsync(new EmptyMessage());
                    }
                }
                else
                {
                    // Handle synchronous methods
                    if (result != null)
                    {
                        if (isEmptyMessage)
                        {
                            await ReplyWithEmptyMessage(m, result, reqSeqId, userId, tenantId, correlationId);
                        }
                        else
                        {
                            await ReplyWithAuditWrapper(m, result, reqSeqId, userId, tenantId, correlationId);
                        }
                    }
                    else if (isEmptyMessage)
                    {
                        await m.ReplyAsync(new EmptyMessage());
                    }
                }
            }
            else
            {
                await m.ReplyErrorAsync(500, $"Service instance not found for type {serviceType.Name}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing parameterless request-response for endpoint {EndpointName}: {Message}",
                endpointName, ex.Message);
            await m.ReplyErrorAsync(500, ex.Message);
        }
    }

    private async Task ReplyWithAuditWrapper<T>(NatsSvcMsg<T> msg, object response, Guid reqSeqId, string? userId, string? tenantId, string? correlationId) where T : class
    {
        if (!_options.EnableAuditWrapper)
        {
            await msg.ReplyAsync(response);
            return;
        }

        var responseType = response.GetType();
        
        // Check if response is already wrapped in ResponseDto
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(ResponseDto<>))
        {
            // Already wrapped, just add audit info if not present
            var withAuditMethod = responseType.GetMethod("WithAuditInfo");
            if (withAuditMethod != null)
            {
                var wrappedResponse = withAuditMethod.Invoke(response, [userId, tenantId, correlationId]);
                await msg.ReplyAsync(wrappedResponse!);
            }
            else
            {
                await msg.ReplyAsync(response);
            }
        }
        else if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(ErrorOr<>))
        {
            // Handle ErrorOr<T> type
            var valueType = responseType.GetGenericArguments()[0];
            var responseDtoType = typeof(ResponseDto<>).MakeGenericType(valueType);
            
            // Convert ErrorOr<T> to ResponseDto<T>
            var implicitOperator = responseDtoType.GetMethod("op_Implicit", [responseType]);
            if (implicitOperator != null)
            {
                var responseDto = implicitOperator.Invoke(null, [response]);
                
                // Add audit info
                var withAuditMethod = responseDtoType.GetMethod("WithAuditInfo");
                if (withAuditMethod != null && responseDto != null)
                {
                    responseDto = withAuditMethod.Invoke(responseDto, [userId, tenantId, correlationId]);
                }
                
                await msg.ReplyAsync(responseDto!);
            }
            else
            {
                await msg.ReplyAsync(response);
            }
        }
        else
        {
            // Wrap plain response in ResponseDto<T>
            var responseDtoType = typeof(ResponseDto<>).MakeGenericType(responseType);
            var successMethod = responseDtoType.GetMethod("Success", [responseType, typeof(Guid), typeof(string)]);
            
            if (successMethod != null)
            {
                var responseDto = successMethod.Invoke(null, [response, reqSeqId, null]);
                
                // Add audit info
                var withAuditMethod = responseDtoType.GetMethod("WithAuditInfo");
                if (withAuditMethod != null && responseDto != null)
                {
                    responseDto = withAuditMethod.Invoke(responseDto, [userId, tenantId, correlationId]);
                }
                
                await msg.ReplyAsync(responseDto!);
            }
            else
            {
                await msg.ReplyAsync(response);
            }
        }
    }

    private async Task ReplyWithEmptyMessage<T>(NatsSvcMsg<T> msg, object response, Guid reqSeqId, string? userId, string? tenantId, string? correlationId) where T : class
    {
        // For Protobuf compatibility, always reply with EmptyMessage for parameterless methods
        // The actual response data is lost in this case, but it maintains compatibility
        await msg.ReplyAsync(new EmptyMessage());
        
        // Log the original response for debugging
        _logger.LogDebug("Protobuf parameterless method response (data lost due to EmptyMessage compatibility): {Response}", 
            response?.ToString() ?? "null");
    }

    private INatsSerializerRegistry GetSerializerForConnection(string channelName)
    {
        // Find the connection settings for the specific channel
        if (!string.IsNullOrEmpty(channelName) && 
            _serviceFrameworkOptions.Connections.TryGetValue(channelName, out var connectionSettings) &&
            connectionSettings.NatsSerializerRegistry != null)
        {
            return connectionSettings.NatsSerializerRegistry;
        }
        
        // Try default connection if channelName is not found or empty
        if (!string.IsNullOrEmpty(_serviceFrameworkOptions.DefaultConnection) &&
            _serviceFrameworkOptions.Connections.TryGetValue(_serviceFrameworkOptions.DefaultConnection, out var defaultSettings) &&
            defaultSettings.NatsSerializerRegistry != null)
        {
            return defaultSettings.NatsSerializerRegistry;
        }
        
        // Fallback to default serializer registry
        return _serviceFrameworkOptions.DefaultSerializerRegistry;
    }

    private string? GetSubject(NatsMethodInfo methodInfo)
    {
        var attr = methodInfo.Method.GetCustomAttribute<SubjectAttribute>();
        if (attr != null && attr.CustomSubject != null)
        {
            return attr.CustomSubject;
        }
        return null;
    }

    private string GetChannelName(Type serviceType, NatsMethodInfo methodInfo)
    {
        // Priority: method > class > default connection

        // 1. Check method-level Channel attribute
        var methodChannel = methodInfo.Method.GetCustomAttribute<ChannelAttribute>();
        if (methodChannel != null)
        {
            return methodChannel.Name;
        }

        // 2. Check class-level Channel attribute
        var classChannel = serviceType.GetCustomAttribute<ChannelAttribute>();
        if (classChannel != null)
        {
            return classChannel.Name;
        }

        // 3. Use default connection
        return _serviceFrameworkOptions.DefaultConnection ?? string.Empty;
    }

    private (string QueueGroup, string ServiceName, string ServiceVersion) GetServiceInfo(Type serviceType, string fallbackServiceName)
    {
        // Check for ServiceInfo attribute on the class
        var serviceInfo = serviceType.GetCustomAttribute<ServiceInfoAttribute>();
        
        if (serviceInfo != null)
        {
            var serviceName = serviceInfo.ServiceName ?? fallbackServiceName;
            var queueGroup = serviceInfo.QueueGroup ?? serviceName + "_q";
            var serviceVersion = serviceInfo.ServiceVersion ?? "1.0.0";
            return (queueGroup, serviceName, serviceVersion);
        }
        
        // Fallback to original logic
        return (fallbackServiceName + "_q", fallbackServiceName, "1.0.0");
    }
}

/// <summary>
/// Result of service registration operation
/// </summary>
public class ServiceRegistrationResult
{
    public List<(Type ServiceType, List<NatsMethodInfo> Methods, INatsSvcServer ServiceServer)> RegisteredServices { get; set; } = new();
    public List<(Type ServiceType, List<NatsMethodInfo> Methods, string ErrorMessage)> FailedServices { get; set; } = new();
}

/// <summary>
/// Result of service shutdown operation
/// </summary>
public class ServiceShutdownResult
{
    public List<string> StoppedServices { get; set; } = new();
    public List<(string ServiceName, string ErrorMessage)> FailedServices { get; set; } = new();
}