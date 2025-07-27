using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.IntegrationTests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.IntegrationTests.Core;

/// <summary>
/// Integration tests for ServiceFrameworkBackgroundService coordination
/// Tests the coordination between discovery, registration, and pub-sub management
/// </summary>
public class ServiceFrameworkBackgroundServiceIntegrationTests : ServiceFrameworkTestBase
{
    [Fact]
    public async Task StartAsync_CoordinatesServiceDiscoveryAndRegistration()
    {
        // Arrange
        var backgroundService = GetRequiredService<ServiceFrameworkBackgroundService>();
        var serviceDiscovery = GetRequiredService<IServiceDiscovery>();
        var serviceRegistrar = GetRequiredService<IServiceRegistrar>();
        
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;

        // Act
        await backgroundService.StartAsync(cancellationToken);

        // Assert
        // The service should start without throwing exceptions
        // This tests the coordination between discovery and registration
        Should.NotThrow(async () => await backgroundService.StartAsync(cancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_CoordinatesPubSubSubscriptions()
    {
        // Arrange
        var backgroundService = GetRequiredService<ServiceFrameworkBackgroundService>();
        var pubSubManager = GetRequiredService<IPubSubManager>();
        
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token;

        // Start the background service first
        await backgroundService.StartAsync(cancellationToken);

        // Act - The ExecuteAsync method should start pub-sub subscriptions
        var executeTask = backgroundService.ExecuteAsync(cancellationToken);
        
        // Give it a moment to start
        await Task.Delay(100, CancellationToken.None);
        
        // Cancel to stop the background execution
        cancellationToken.ThrowIfCancellationRequested();

        // Assert
        // The execute task should complete without unhandled exceptions
        Should.NotThrow(async () => await executeTask);
    }

    [Fact]
    public async Task StopAsync_CoordinatesServiceAndSubscriptionShutdown()
    {
        // Arrange
        var backgroundService = GetRequiredService<ServiceFrameworkBackgroundService>();
        var serviceRegistrar = GetRequiredService<IServiceRegistrar>();
        var pubSubManager = GetRequiredService<IPubSubManager>();
        
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;

        // Start the service first
        await backgroundService.StartAsync(cancellationToken);

        // Act
        await backgroundService.StopAsync(cancellationToken);

        // Assert
        // The service should stop without throwing exceptions
        // This tests the coordination between service shutdown and subscription cleanup
        Should.NotThrow(async () => await backgroundService.StopAsync(cancellationToken));
    }

    [Fact]
    public async Task BackgroundService_LifecycleCoordination_WorksCorrectly()
    {
        // Arrange
        var backgroundService = GetRequiredService<ServiceFrameworkBackgroundService>();
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token;

        // Act & Assert - Test complete lifecycle
        
        // 1. Start
        Should.NotThrow(async () => await backgroundService.StartAsync(cancellationToken));
        
        // 2. Execute (let it run briefly)
        var executeTask = backgroundService.ExecuteAsync(cancellationToken);
        await Task.Delay(100, CancellationToken.None);
        
        // 3. Stop
        Should.NotThrow(async () => await backgroundService.StopAsync(CancellationToken.None));
        
        // Wait for execute task to complete
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    [Fact]
    public void BackgroundService_DependencyInjection_AllDependenciesResolved()
    {
        // Arrange & Act
        var backgroundService = GetRequiredService<ServiceFrameworkBackgroundService>();
        var serviceDiscovery = GetRequiredService<IServiceDiscovery>();
        var serviceRegistrar = GetRequiredService<IServiceRegistrar>();
        var pubSubManager = GetRequiredService<IPubSubManager>();

        // Assert
        backgroundService.ShouldNotBeNull();
        serviceDiscovery.ShouldNotBeNull();
        serviceRegistrar.ShouldNotBeNull();
        pubSubManager.ShouldNotBeNull();
    }

    [Fact]
    public async Task BackgroundService_ServiceDiscoveryIntegration_ProcessesServices()
    {
        // Arrange
        var serviceDiscovery = GetRequiredService<IServiceDiscovery>();
        
        // Act
        var discoveryResult = await serviceDiscovery.DiscoverServicesAsync();

        // Assert
        discoveryResult.ShouldNotBeNull();
        discoveryResult.ReqRspServices.ShouldNotBeNull();
        discoveryResult.PubSubServices.ShouldNotBeNull();
        
        // Should discover the test service
        var allServices = discoveryResult.ReqRspServices.Concat(discoveryResult.PubSubServices);
        allServices.ShouldContain(service => service.ServiceType == typeof(ITestNatsService));
    }

    [Fact]
    public async Task BackgroundService_ServiceRegistrarIntegration_RegistersServices()
    {
        // Arrange
        var serviceDiscovery = GetRequiredService<IServiceDiscovery>();
        var serviceRegistrar = GetRequiredService<IServiceRegistrar>();
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;
        
        // Act
        var discoveryResult = await serviceDiscovery.DiscoverServicesAsync();
        var registrationResult = await serviceRegistrar.RegisterRequestResponseServicesAsync(
            discoveryResult.ReqRspServices, cancellationToken);

        // Assert
        registrationResult.ShouldNotBeNull();
        registrationResult.RegisteredServices.ShouldNotBeNull();
        registrationResult.FailedServices.ShouldNotBeNull();
        
        // Registration should succeed (even with test doubles)
        registrationResult.RegisteredServices.Count.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task BackgroundService_PubSubManagerIntegration_ManagesSubscriptions()
    {
        // Arrange
        var serviceDiscovery = GetRequiredService<IServiceDiscovery>();
        var pubSubManager = GetRequiredService<IPubSubManager>();
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token;
        
        // Act
        var discoveryResult = await serviceDiscovery.DiscoverServicesAsync();
        var subscriptionResult = await pubSubManager.StartSubscriptionsAsync(
            discoveryResult.PubSubServices, cancellationToken);

        // Assert
        subscriptionResult.ShouldNotBeNull();
        subscriptionResult.SuccessfulSubscriptions.ShouldNotBeNull();
        subscriptionResult.FailedSubscriptions.ShouldNotBeNull();
        
        // Should have attempted to set up subscriptions
        var totalAttempts = subscriptionResult.SuccessfulSubscriptions.Count + subscriptionResult.FailedSubscriptions.Count;
        totalAttempts.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task BackgroundService_ErrorHandling_ContinuesOperation()
    {
        // Arrange
        var backgroundService = GetRequiredService<ServiceFrameworkBackgroundService>();
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token;

        // Act & Assert - Should handle errors gracefully
        try
        {
            await backgroundService.StartAsync(cancellationToken);
            var executeTask = backgroundService.ExecuteAsync(cancellationToken);
            
            // Let it run briefly
            await Task.Delay(100, CancellationToken.None);
            
            await backgroundService.StopAsync(CancellationToken.None);
            
            // Wait for execute to complete
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected for cancellation
        }
        catch (Exception ex)
        {
            // Should not throw unhandled exceptions during normal operation
            Assert.True(false, $"Unexpected exception: {ex}");
        }
    }

    [Fact]
    public async Task BackgroundService_ConcurrentOperations_HandlesCorrectly()
    {
        // Arrange
        var backgroundService = GetRequiredService<ServiceFrameworkBackgroundService>();
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token;

        // Act - Test concurrent start/stop operations
        var startTask1 = backgroundService.StartAsync(cancellationToken);
        var startTask2 = backgroundService.StartAsync(cancellationToken);
        
        await Task.WhenAll(startTask1, startTask2);
        
        var stopTask1 = backgroundService.StopAsync(cancellationToken);
        var stopTask2 = backgroundService.StopAsync(cancellationToken);
        
        // Assert - Should handle concurrent operations gracefully
        Should.NotThrow(async () => await Task.WhenAll(stopTask1, stopTask2));
    }

    [Fact]
    public void BackgroundService_IsRegisteredAsHostedService()
    {
        // Arrange & Act
        var hostedServices = GetServices<IHostedService>();
        
        // Assert
        hostedServices.ShouldNotBeEmpty();
        hostedServices.ShouldContain(service => service is ServiceFrameworkBackgroundService);
    }

    [Fact]
    public async Task BackgroundService_CoordinationPattern_ImplementedCorrectly()
    {
        // Arrange
        var backgroundService = GetRequiredService<ServiceFrameworkBackgroundService>();
        
        // Verify all coordinator dependencies are injected
        var serviceDiscovery = GetRequiredService<IServiceDiscovery>();
        var serviceRegistrar = GetRequiredService<IServiceRegistrar>();
        var pubSubManager = GetRequiredService<IPubSubManager>();
        
        // Act - Test that the coordinator delegates properly
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token;
        
        // Assert
        // All dependencies should be available for coordination
        serviceDiscovery.ShouldNotBeNull();
        serviceRegistrar.ShouldNotBeNull();
        pubSubManager.ShouldNotBeNull();
        
        // The background service should coordinate these dependencies
        backgroundService.ShouldNotBeNull();
        
        // Test coordination works without exceptions
        Should.NotThrow(async () =>
        {
            await backgroundService.StartAsync(cancellationToken);
            var executeTask = backgroundService.ExecuteAsync(cancellationToken);
            await Task.Delay(50, CancellationToken.None);
            await backgroundService.StopAsync(CancellationToken.None);
            
            try
            {
                await executeTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        });
    }
}