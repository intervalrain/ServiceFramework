using EdgeSync.ServiceFramework.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Decisions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;

/// <summary>
/// Service discovery implementation for finding and categorizing NATS services
/// Extracted from ServiceFrameworkBackgroundService to follow SRP
/// </summary>
public class ServiceDiscovery : IServiceDiscovery
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConventionDecisionMaker _decisionMaker;
    private readonly IMethodInfoBuilder _methodInfoBuilder;
    private readonly AutoConventionOptions _options;
    private readonly ILogger<ServiceDiscovery> _logger;

    public ServiceDiscovery(
        IServiceProvider serviceProvider,
        IConventionDecisionMaker decisionMaker,
        IMethodInfoBuilder methodInfoBuilder,
        IOptions<AutoConventionOptions> options,
        ILogger<ServiceDiscovery> logger)
    {
        _serviceProvider = serviceProvider;
        _decisionMaker = decisionMaker;
        _methodInfoBuilder = methodInfoBuilder;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ServiceDiscoveryResult> DiscoverServicesAsync()
    {
        _logger.LogInformation("Starting service discovery for NATS services");

        var result = new ServiceDiscoveryResult();

        try
        {
            // Step 1: Discover all NatsService implementations
            var serviceTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsClass && !type.IsAbstract &&
                               type.IsSubclassOf(typeof(NatsService)))
                .ToList();

            _logger.LogDebug("Found {ServiceCount} NATS service types", serviceTypes.Count);

            // Step 2: Process each service type
            foreach (var serviceType in serviceTypes)
            {
                await ProcessServiceType(serviceType, result);
            }

            _logger.LogInformation("Service discovery completed. Found {PubSubCount} pub-sub services and {ReqRspCount} request-response services",
                result.PubSubServices.Count, result.ReqRspServices.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during service discovery");
            throw;
        }
    }

    private async Task ProcessServiceType(Type serviceType, ServiceDiscoveryResult result)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = GetOrCreateServiceInstance(scope, serviceType);

            if (service != null)
            {
                // Get NATS methods with decision logic
                var natsMethods = _methodInfoBuilder.GetNatsMethodsWithDecision(service);
                
                // Categorize methods by convention mode
                var pubsubMethods = natsMethods.Where(m => !m.IsRequestResponse).ToList();
                var reqrspMethods = natsMethods.Where(m => m.IsRequestResponse).ToList();

                if (pubsubMethods.Any())
                {
                    result.PubSubServices.Add((serviceType, pubsubMethods));
                    _logger.LogDebug("Service {ServiceType} has {MethodCount} pub-sub methods", 
                        serviceType.Name, pubsubMethods.Count);
                }

                if (reqrspMethods.Any())
                {
                    result.ReqRspServices.Add((serviceType, reqrspMethods));
                    _logger.LogDebug("Service {ServiceType} has {MethodCount} request-response methods", 
                        serviceType.Name, reqrspMethods.Count);
                }
            }
            else
            {
                _logger.LogWarning("Could not create instance of service type {ServiceType}", serviceType.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing service type {ServiceType}", serviceType.Name);
            // Continue with other services instead of failing completely
        }
    }

    private NatsService? GetOrCreateServiceInstance(IServiceScope scope, Type serviceType)
    {
        // Try to get from DI container first
        var service = scope.ServiceProvider.GetService(serviceType) as NatsService;

        if (service == null && serviceType.IsSubclassOf(typeof(NatsService)))
        {
            // Try to create instance using constructor injection
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
}

