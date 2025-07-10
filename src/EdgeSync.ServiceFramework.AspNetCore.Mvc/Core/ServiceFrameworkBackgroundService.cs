using System.Reflection;
using System.Text.Json;

using EdgeSync.ServiceFramework.Abstractions.Attributes;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Decisions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Subscriptions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Extensions;
using EdgeSync.ServiceFramework.Attributes;

using ErrorOr;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;

public class ServiceFrameworkBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServiceFrameworkBackgroundService> _logger;
    private readonly IConventionDecisionMaker _decisionMaker;
    private readonly ISubscriptionHandlerFactory _subscriptionHandlerFactory;
    private readonly AutoConventionOptions _options;
    private readonly List<IAsyncDisposable> _subscriptions = [];

    public ServiceFrameworkBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ServiceFrameworkBackgroundService> logger,
        IConventionDecisionMaker decisionMaker,
        ISubscriptionHandlerFactory subscriptionHandlerFactory,
        IOptions<AutoConventionOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _decisionMaker = decisionMaker;
        _subscriptionHandlerFactory = subscriptionHandlerFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Discover all NatsService implementations
        var serviceTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && !type.IsAbstract &&
                           type.IsSubclassOf(typeof(NatsService)))
            .ToList();

        var subscriptionTasks = new List<Task>();

        foreach (var serviceType in serviceTypes)
        {
            var channelAttribute = serviceType.GetCustomAttribute<ChannelAttribute>();
            var channelName = channelAttribute?.Name;

            using var scope = _serviceProvider.CreateScope();
            INatsConnection connection;
            try
            {
                if (channelName != null)
                {
                    connection = scope.ServiceProvider.GetRequiredKeyedService<INatsConnection>(channelName);
                    _logger.LogInformation("Service {ServiceType} using NATS connection '{ChannelName}'", serviceType.Name, channelName);
                }
                else
                {
                    connection = scope.ServiceProvider.GetRequiredService<INatsConnection>();
                    _logger.LogInformation("Service {ServiceType} using default NATS connection", serviceType.Name);
                }

                await connection.ConnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get NATS connection for service {ServiceType} with channel '{ChannelName}'", serviceType.Name, channelName ?? "default");
                continue;
            }

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

            if (service != null)
            {
                var natsMethods = GetNatsMethodsWithDecision(service);

                foreach (var natsMethod in natsMethods)
                {
                    if (natsMethod.IsRequestResponse)
                    {
                        
                    }
                    else
                    {
                        var task = Task.Run(async () =>
                        {
                            
                            await SubscribeToMethod(connection, serviceType, natsMethod, stoppingToken);
                        }, stoppingToken);

                        subscriptionTasks.Add(task);
                    }
                }
            }
        }

        _logger.LogInformation("All NATS subscriptions started via reflection");
        await Task.WhenAll(subscriptionTasks);
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
                Method = method,
                SubjectName = subjectAttr?.CustomSubject ?? $"{service.GetSubjectPrefix()}.{method.Name.ToLower().Replace("async", "")}",
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

        await base.StopAsync(cancellationToken);
    }
}