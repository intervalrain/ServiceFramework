using System.Reflection;

using EdgeSync.ServiceFramework.Abstractions;
using EdgeSync.ServiceFramework.Abstractions.Attributes;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Decisions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Subscriptions;
using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.Data;
using EdgeSync.ServiceFramework.Extensions;
using ErrorOr;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NATS.Client.Core;
using NATS.Client.Services;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;

public class ServiceFrameworkBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServiceFrameworkBackgroundService> _logger;
    private readonly IConventionDecisionMaker _decisionMaker;
    private readonly ISubscriptionHandlerFactory _subscriptionHandlerFactory;
    private readonly AutoConventionOptions _options;
    private readonly ServiceFrameworkOptions _serviceFrameworkOptions;
    private readonly List<IAsyncDisposable> _subscriptions = [];
    private readonly List<INatsSvcServer> _serviceServers = [];
    private readonly List<(Type ServiceType, List<NatsMethodInfo> Methods)> _pubsubServices = [];
    private readonly List<(Type ServiceType, List<NatsMethodInfo> Methods)> _reqrspServices = [];

    public ServiceFrameworkBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ServiceFrameworkBackgroundService> logger,
        IConventionDecisionMaker decisionMaker,
        ISubscriptionHandlerFactory subscriptionHandlerFactory,
        IOptions<AutoConventionOptions> options,
        IOptions<ServiceFrameworkOptions> serviceFrameworkOptions)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _decisionMaker = decisionMaker;
        _subscriptionHandlerFactory = subscriptionHandlerFactory;
        _options = options.Value;
        _serviceFrameworkOptions = serviceFrameworkOptions.Value;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Step 1: Discover all NatsService implementations and categorize them
        var serviceTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && !type.IsAbstract &&
                           type.IsSubclassOf(typeof(NatsService)))
            .ToList();

        foreach (var serviceType in serviceTypes)
        {
            using var scope = _serviceProvider.CreateScope();
            var service = GetOrCreateServiceInstance(scope, serviceType);

            if (service != null)
            {
                var natsMethods = GetNatsMethodsWithDecision(service);
                var pubsubMethods = natsMethods.Where(m => !m.IsRequestResponse).ToList();
                var reqrspMethods = natsMethods.Where(m => m.IsRequestResponse).ToList();

                if (pubsubMethods.Any())
                {
                    _pubsubServices.Add((serviceType, pubsubMethods));
                }

                if (reqrspMethods.Any())
                {
                    _reqrspServices.Add((serviceType, reqrspMethods));
                }
            }
        }

        // Step 2: Register all request-response services immediately
        foreach (var (serviceType, methods) in _reqrspServices)
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

                var connection = await GetConnectionAsync(scope, channelName, serviceType.Name);
                if (connection == null) continue;

                var svcServer = await CreateSvcServer(serviceName, groupMethods, connection, cancellationToken);

                // Setup request-response service endpoints
                await SetupRequestResponseServiceGroup(connection, svcServer, serviceType, serviceName, groupMethods, cancellationToken);
                
                _logger.LogInformation("Registered request-response service '{ServiceName}' with {MethodCount} methods during startup",
                    serviceName, groupMethods.Count);
            }
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Step 3: Set up pub-sub subscriptions
        var subscriptionTasks = new List<Task>();

        foreach (var (serviceType, methods) in _pubsubServices)
        {
            foreach (var method in methods)
            {
                using var scope = _serviceProvider.CreateScope();
                var channelName = GetChannelName(serviceType, method);
                var connection = await GetConnectionAsync(scope, channelName, serviceType.Name);
                if (connection == null) continue;

                var task = Task.Run(async () =>
                {
                    await SubscribeToMethod(connection, serviceType, method, stoppingToken);
                }, stoppingToken);

                subscriptionTasks.Add(task);
            }
        }

        _logger.LogInformation("All NATS pub-sub subscriptions started");
        await Task.WhenAll(subscriptionTasks);
    }

    private NatsService? GetOrCreateServiceInstance(IServiceScope scope, Type serviceType)
    {
        var service = scope.ServiceProvider.GetService(serviceType) as NatsService;

        if (service == null && serviceType.IsSubclassOf(typeof(NatsService)))
        {
            var constructors = serviceType.GetConstructors();
            foreach (var constructor in constructors)
            {
                var parameters = constructor.GetParameters();
                var args = new object[parameters.Length];
                var canCreate = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    try
                    {
                        args[i] = scope.ServiceProvider.GetRequiredService(parameters[i].ParameterType);
                    }
                    catch
                    {
                        canCreate = false;
                        break;
                    }
                }

                if (canCreate)
                {
                    service = (NatsService)Activator.CreateInstance(serviceType, args)!;
                    break;
                }
            }
        }

        return service;
    }

    private async Task<INatsSvcServer> CreateSvcServer(string serviceName, List<NatsMethodInfo> methods, INatsConnection connection, CancellationToken cancellationToken)
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

    private async Task SetupRequestResponseServiceGroup(INatsConnection connection, INatsSvcServer svcServer, Type serviceType, string serviceName, List<NatsMethodInfo> methods, CancellationToken cancellationToken)
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
        }
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

        await svcServer.AddEndpointAsync<object>(
            name: endpointName,
            serializer: serializerRegistry.GetDeserializer<object>(),
            subject: subject,
            handler: async (NatsSvcMsg<object> m) =>
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
                    _logger.LogError(ex, "Error processing parameterless request-response for endpoint {EndpointName}: {Message}",
                        endpointName, ex.Message);
                    await m.ReplyErrorAsync(500, ex.Message);
                }
            },
            cancellationToken: cancellationToken);

        _logger.LogInformation("Successfully added parameterless NATS service endpoint: {EndpointName} for method: {Method} on service: {ServiceType}",
            endpointName, methodInfo.Method.Name, serviceType.Name);
    }

    private async Task SubscribeToMethod(INatsConnection connection, Type serviceType, NatsMethodInfo methodInfo, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Subscribing to subject: {Subject} for method: {Method} with mode: {Mode} on connection {ConnectionId}",
                methodInfo.SubjectName, methodInfo.Method.Name, methodInfo.ConventionMode, connection.ServerInfo?.ClientId);

            // Get appropriate subscription handler and execute
            try
            {
                var handler = _subscriptionHandlerFactory.GetHandler(methodInfo.ConventionMode);
                await handler.SubscribeAsync(connection, serviceType, methodInfo, cancellationToken);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogError(ex, "Unsupported convention mode: {Mode} for method: {Method}", methodInfo.ConventionMode, methodInfo.Method.Name);
            }

            _logger.LogInformation("Successfully subscribed to subject: {Subject} with mode: {Mode} on connection {ConnectionId}",
                methodInfo.SubjectName, methodInfo.ConventionMode, connection.ServerInfo?.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to subject: {Subject} with mode: {Mode} on connection {ConnectionId}",
                methodInfo.SubjectName, methodInfo.ConventionMode, connection.ServerInfo?.ClientId);
        }
    }

    private IEnumerable<NatsMethodInfo> GetNatsMethodsWithDecision(NatsService service)
    {
        var serviceType = service.GetType();
        var methods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.EndsWith("Async") && m.ReturnType.IsGenericType);

        foreach (var method in methods)
        {
            var subjectAttr = method.GetCustomAttribute<SubjectAttribute>();
            var jetStreamAttr = method.GetCustomAttribute<JetStreamAttribute>();
            var jetStreamPullAttr = method.GetCustomAttribute<JetStreamPullAttribute>();

            var natsMethodInfo = new NatsMethodInfo
            {
                ServiceName = service.ServiceName,
                Method = method,
                SubjectName = subjectAttr?.CustomSubject ?? throw new Exception("Subject is must-be property for SubjectAttribute."),
                ServiceMethod = method,
                Endpoint = subjectAttr?.GetEndpoint(method.Name),
                SubjectAttribute = subjectAttr,
                JetStreamAttribute = jetStreamAttr,
                JetStreamPullAttribute = jetStreamPullAttr
            };

            // Use IConventionDecisionMaker to determine the mode
            var context = ConventionDecisionContextBuilder.Build(method, _options);
            var decisionResult = _decisionMaker.MakeDecision(context);

            if (decisionResult.IsError)
            {
                _logger.LogError("Convention decision error for method {MethodName} on service {ServiceType}: {ErrorMessage}",
                    method.Name, serviceType.Name, decisionResult.ErrorMessage);
                throw new InvalidOperationException($"Convention decision error for method {method.Name}: {decisionResult.ErrorMessage}");
            }

            natsMethodInfo.ConventionMode = decisionResult.Mode;

            yield return natsMethodInfo;
        }
    }


    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping NATS reflection background service");

        // Stop and dispose service servers first (this will also stop their endpoints)
        foreach (var serviceServer in _serviceServers)
        {
            try
            {
                _logger.LogDebug("Stopping NATS service server: {ServiceName}", serviceServer.GetInfo().Name ?? "Unknown");
                await serviceServer.StopAsync(cancellationToken);
                await serviceServer.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping/disposing service server");
            }
        }

        // Then dispose pub-sub subscriptions
        foreach (var subscription in _subscriptions)
        {
            try
            {
                await subscription.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing subscription");
            }
        }

        _serviceServers.Clear();
        _subscriptions.Clear();

        await base.StopAsync(cancellationToken);
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

    private async Task<INatsConnection?> GetConnectionAsync(IServiceScope scope, string channelName, string serviceTypeName)
    {
        try
        {
            var factory = scope.ServiceProvider.GetRequiredService<INatsConnectionFactory>();
            INatsConnection connection;
            
            // Priority: specific channel > default connection setting > fallback to default
            if (!string.IsNullOrEmpty(channelName) && 
                _serviceFrameworkOptions.Connections.TryGetValue(channelName, out var connectionSettings))
            {
                // Use factory to create connection with specific named settings (uses connection pool)
                connection = await factory.CreateConnectionAsync(connectionSettings);
                _logger.LogDebug("Service {ServiceType} using NATS connection '{ChannelName}' via factory (pooled)", serviceTypeName, channelName);
            }
            else if (!string.IsNullOrEmpty(_serviceFrameworkOptions.DefaultConnection) &&
                     _serviceFrameworkOptions.Connections.TryGetValue(_serviceFrameworkOptions.DefaultConnection, out var defaultSettings))
            {
                // Use default connection from ServiceFrameworkOptions
                connection = await factory.CreateConnectionAsync(defaultSettings);
                _logger.LogDebug("Service {ServiceType} using default NATS connection '{DefaultConnection}' via factory (pooled)", serviceTypeName, _serviceFrameworkOptions.DefaultConnection);
            }
            else
            {
                // Fallback to legacy configuration (uses connection pool)
                connection = await factory.CreateConnectionAsync();
                _logger.LogDebug("Service {ServiceType} using legacy default NATS connection via factory (pooled)", serviceTypeName);
            }

            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create NATS connection for service {ServiceType} with channel '{ChannelName}' via factory", serviceTypeName, channelName ?? "default");
            return null;
        }
    }

    /// <summary>
    /// Check if type is a primitive type that needs to be wrapped in NatsRequest
    /// </summary>
    private static bool IsPrimitiveType(Type type)
    {
        return type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               type.IsEnum ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                IsPrimitiveType(type.GetGenericArguments()[0]));
    }
}