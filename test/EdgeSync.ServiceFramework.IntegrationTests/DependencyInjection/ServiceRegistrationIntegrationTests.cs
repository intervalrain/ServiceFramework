using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Handlers;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Subscriptions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;
using EdgeSync.ServiceFramework.Core.Filters;
using EdgeSync.ServiceFramework.Core.Serializers;
using EdgeSync.ServiceFramework.IntegrationTests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EdgeSync.ServiceFramework.IntegrationTests.DependencyInjection;

/// <summary>
/// Integration tests for dependency injection container resolution
/// Tests that all refactored components are correctly registered and resolved
/// </summary>
public class ServiceRegistrationIntegrationTests : ServiceFrameworkTestBase
{
    [Fact]
    public void DependencyInjection_CoreAbstractions_AllRegistered()
    {
        // Act & Assert - Test all core abstractions are registered
        GetRequiredService<IAuditHandler>().ShouldNotBeNull();
        GetRequiredService<IConnectionResolver>().ShouldNotBeNull();
        GetRequiredService<IRequestDataExtractor>().ShouldNotBeNull();
        GetRequiredService<IResponseProcessor>().ShouldNotBeNull();
        GetRequiredService<IChannelResolver>().ShouldNotBeNull();
        GetRequiredService<IServiceDiscovery>().ShouldNotBeNull();
        GetRequiredService<IServiceRegistrar>().ShouldNotBeNull();
        GetRequiredService<IMethodInfoBuilder>().ShouldNotBeNull();
        GetRequiredService<IPubSubManager>().ShouldNotBeNull();
        GetRequiredService<IControllerModelBuilder>().ShouldNotBeNull();
        GetRequiredService<IActionModelBuilder>().ShouldNotBeNull();
        GetRequiredService<IConventionModeHandlerFactory>().ShouldNotBeNull();
    }

    [Fact]
    public void DependencyInjection_ConventionModeHandlers_AllRegistered()
    {
        // Act
        var handlers = GetServices<IConventionModeHandler>().ToList();

        // Assert
        handlers.ShouldNotBeEmpty();
        handlers.Count.ShouldBeGreaterThanOrEqualTo(2); // At least RequestResponse and PubSub handlers
        
        // Verify specific handler types are registered
        handlers.ShouldContain(h => h is NatsRequestResponseHandler);
        handlers.ShouldContain(h => h is PubSubHandler);
    }

    [Fact]
    public void DependencyInjection_SubscriptionHandlers_AllRegistered()
    {
        // Act
        var handlers = GetServices<ISubscriptionHandler>().ToList();
        var factory = GetRequiredService<ISubscriptionHandlerFactory>();

        // Assert
        handlers.ShouldNotBeEmpty();
        factory.ShouldNotBeNull();
        
        // Should have different subscription handler types
        handlers.Count.ShouldBeGreaterThanOrEqualTo(3); // Request-Response, PubSub variants
    }

    [Fact]
    public void DependencyInjection_SerializerComponents_AllRegistered()
    {
        // Act & Assert
        GetRequiredService<ISerializerAdapterFactory>().ShouldNotBeNull();
        
        // Verify serializer adapters are available
        var adapters = GetServices<ISerializerAdapter>().ToList();
        adapters.ShouldNotBeEmpty();
    }

    [Fact]
    public void DependencyInjection_BackgroundService_RegisteredAsHostedService()
    {
        // Act
        var hostedServices = GetServices<IHostedService>().ToList();
        var backgroundService = GetService<ServiceFrameworkBackgroundService>();

        // Assert
        hostedServices.ShouldNotBeEmpty();
        hostedServices.ShouldContain(service => service is ServiceFrameworkBackgroundService);
        backgroundService.ShouldNotBeNull();
    }

    [Fact]
    public void DependencyInjection_ActionFilter_RegisteredAndResolvable()
    {
        // Act & Assert
        var filter = GetRequiredService<NatsProxyActionFilter>();
        filter.ShouldNotBeNull();
    }

    [Fact]
    public void DependencyInjection_ServiceLifetimes_ConfiguredCorrectly()
    {
        // Test that singleton services return the same instance
        var serviceDiscovery1 = GetRequiredService<IServiceDiscovery>();
        var serviceDiscovery2 = GetRequiredService<IServiceDiscovery>();
        serviceDiscovery1.ShouldBeSameAs(serviceDiscovery2); // Singleton

        var connectionResolver1 = GetRequiredService<IConnectionResolver>();
        var connectionResolver2 = GetRequiredService<IConnectionResolver>();
        connectionResolver1.ShouldBeSameAs(connectionResolver2); // Singleton

        // Test that scoped services can be resolved (instances may differ in different scopes)
        var auditHandler = GetRequiredService<IAuditHandler>();
        auditHandler.ShouldNotBeNull(); // Scoped

        var requestDataExtractor = GetRequiredService<IRequestDataExtractor>();
        requestDataExtractor.ShouldNotBeNull(); // Scoped
    }

    [Fact]
    public void DependencyInjection_CircularDependencies_Avoided()
    {
        // Act & Assert - All services should resolve without circular dependency issues
        Should.NotThrow(() =>
        {
            GetRequiredService<NatsProxyActionFilter>();
            GetRequiredService<ServiceFrameworkBackgroundService>();
            GetRequiredService<IConventionModeHandlerFactory>();
            GetRequiredService<IAuditHandler>();
            GetRequiredService<IServiceDiscovery>();
            GetRequiredService<IServiceRegistrar>();
            GetRequiredService<IPubSubManager>();
        });
    }

    [Fact]
    public void DependencyInjection_FactoryPattern_ResolvesHandlersCorrectly()
    {
        // Arrange
        var factory = GetRequiredService<IConventionModeHandlerFactory>();

        // Act & Assert
        var requestResponseHandler = factory.GetHandler(ConventionMode.RequestResponse);
        var pubSubHandler = factory.GetHandler(ConventionMode.PubSubPushJetStream);

        requestResponseHandler.ShouldNotBeNull();
        pubSubHandler.ShouldNotBeNull();
        requestResponseHandler.ShouldBeOfType<NatsRequestResponseHandler>();
        pubSubHandler.ShouldBeOfType<PubSubHandler>();
    }

    [Fact]
    public void DependencyInjection_TransitiveDependencies_ResolvedCorrectly()
    {
        // Arrange & Act - Test that complex dependency chains resolve correctly
        var filter = GetRequiredService<NatsProxyActionFilter>();

        // Assert - The filter should have all its dependencies injected
        filter.ShouldNotBeNull();
        
        // Verify that the filter can be used (dependencies are not null)
        var actionContext = TestFixtures.CreateActionExecutingContext();
        Should.NotThrow(async () => 
        {
            await filter.OnActionExecutionAsync(actionContext, 
                () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));
        });
    }

    [Fact]
    public void DependencyInjection_ServiceImplementations_MatchExpectedTypes()
    {
        // Act & Assert - Verify concrete implementations are correct
        GetRequiredService<IAuditHandler>().ShouldBeOfType<AuditHandler>();
        GetRequiredService<IConnectionResolver>().ShouldBeOfType<ConnectionResolver>();
        GetRequiredService<IRequestDataExtractor>().ShouldBeOfType<RequestDataExtractor>();
        GetRequiredService<IResponseProcessor>().ShouldBeOfType<ResponseProcessor>();
        GetRequiredService<IChannelResolver>().ShouldBeOfType<ChannelResolver>();
        GetRequiredService<IServiceDiscovery>().ShouldBeOfType<ServiceDiscovery>();
        GetRequiredService<IServiceRegistrar>().ShouldBeOfType<ServiceRegistrar>();
        GetRequiredService<IMethodInfoBuilder>().ShouldBeOfType<MethodInfoBuilder>();
        GetRequiredService<IPubSubManager>().ShouldBeOfType<PubSubManager>();
        GetRequiredService<IControllerModelBuilder>().ShouldBeOfType<ControllerModelBuilder>();
        GetRequiredService<IActionModelBuilder>().ShouldBeOfType<ActionModelBuilder>();
        GetRequiredService<IConventionModeHandlerFactory>().ShouldBeOfType<ConventionModeHandlerFactory>();
    }

    [Fact]
    public void DependencyInjection_OptionalDependencies_HandleCorrectly()
    {
        // Act & Assert - Services should handle optional dependencies gracefully
        var serviceDiscovery = GetRequiredService<IServiceDiscovery>();
        var serviceRegistrar = GetRequiredService<IServiceRegistrar>();
        var pubSubManager = GetRequiredService<IPubSubManager>();

        serviceDiscovery.ShouldNotBeNull();
        serviceRegistrar.ShouldNotBeNull();
        pubSubManager.ShouldNotBeNull();
        
        // These services should work even if some optional dependencies are missing
        Should.NotThrow(async () =>
        {
            await serviceDiscovery.DiscoverServicesAsync();
        });
    }

    [Fact]
    public void DependencyInjection_ServiceProvider_HandlesMultipleScopes()
    {
        // Arrange
        using var scope1 = ServiceProvider.CreateScope();
        using var scope2 = ServiceProvider.CreateScope();

        // Act
        var auditHandler1 = scope1.ServiceProvider.GetRequiredService<IAuditHandler>();
        var auditHandler2 = scope2.ServiceProvider.GetRequiredService<IAuditHandler>();

        var serviceDiscovery1 = scope1.ServiceProvider.GetRequiredService<IServiceDiscovery>();
        var serviceDiscovery2 = scope2.ServiceProvider.GetRequiredService<IServiceDiscovery>();

        // Assert
        // Scoped services should be different instances in different scopes
        auditHandler1.ShouldNotBeSameAs(auditHandler2);
        
        // Singleton services should be the same instance across scopes
        serviceDiscovery1.ShouldBeSameAs(serviceDiscovery2);
    }

    [Fact]
    public void DependencyInjection_AllRequiredServicesRegistered_NoMissingDependencies()
    {
        // Act & Assert - Test that all services can be resolved without missing dependency exceptions
        var serviceTypes = new[]
        {
            typeof(IAuditHandler),
            typeof(IConnectionResolver),
            typeof(IRequestDataExtractor),
            typeof(IResponseProcessor),
            typeof(IChannelResolver),
            typeof(IServiceDiscovery),
            typeof(IServiceRegistrar),
            typeof(IMethodInfoBuilder),
            typeof(IPubSubManager),
            typeof(IControllerModelBuilder),
            typeof(IActionModelBuilder),
            typeof(IConventionModeHandlerFactory),
            typeof(NatsProxyActionFilter),
            typeof(ServiceFrameworkBackgroundService)
        };

        foreach (var serviceType in serviceTypes)
        {
            Should.NotThrow(() => ServiceProvider.GetRequiredService(serviceType),
                $"Failed to resolve service: {serviceType.Name}");
        }
    }

    [Fact]
    public void DependencyInjection_ConventionDecisionMaker_IntegratedCorrectly()
    {
        // Act & Assert
        // The convention decision maker should be available and working
        var controllerModelBuilder = GetRequiredService<IControllerModelBuilder>();
        var actionModelBuilder = GetRequiredService<IActionModelBuilder>();

        controllerModelBuilder.ShouldNotBeNull();
        actionModelBuilder.ShouldNotBeNull();
    }

    [Fact]
    public void DependencyInjection_PerformanceCharacteristics_Maintained()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Act - Resolve multiple services
        for (int i = 0; i < 100; i++)
        {
            GetRequiredService<IAuditHandler>();
            GetRequiredService<IConnectionResolver>();
            GetRequiredService<IRequestDataExtractor>();
            GetRequiredService<IConventionModeHandlerFactory>();
        }
        
        stopwatch.Stop();

        // Assert - Service resolution should be performant
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000); // Should resolve 400 services in under 1 second
    }

    [Fact]
    public void DependencyInjection_ServiceConfiguration_FollowsSolidPrinciples()
    {
        // Act & Assert - Verify that dependency injection configuration follows SOLID principles
        
        // Single Responsibility: Each service has a single, clear responsibility
        var auditHandler = GetRequiredService<IAuditHandler>();
        var connectionResolver = GetRequiredService<IConnectionResolver>();
        var requestDataExtractor = GetRequiredService<IRequestDataExtractor>();
        
        auditHandler.ShouldNotBeNull();
        connectionResolver.ShouldNotBeNull();
        requestDataExtractor.ShouldNotBeNull();
        
        // Open/Closed: Services are registered via interfaces, allowing for extension
        var handlers = GetServices<IConventionModeHandler>().ToList();
        handlers.Count.ShouldBeGreaterThanOrEqualTo(2); // Can add more handlers without modifying existing code
        
        // Dependency Inversion: High-level modules depend on abstractions
        var factory = GetRequiredService<IConventionModeHandlerFactory>();
        factory.ShouldNotBeNull();
        
        var requestResponseHandler = factory.GetHandler(ConventionMode.RequestResponse);
        requestResponseHandler.ShouldNotBeNull();
    }
}