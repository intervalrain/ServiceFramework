using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;

/// <summary>
/// Service framework background service coordinator
/// Refactored to use the coordinator pattern with separated responsibilities
/// </summary>
public class ServiceFrameworkBackgroundService : BackgroundService
{
    private readonly IServiceDiscovery _serviceDiscovery;
    private readonly IServiceRegistrar _serviceRegistrar;
    private readonly IPubSubManager _pubSubManager;
    private readonly ILogger<ServiceFrameworkBackgroundService> _logger;

    // Cached discovery results
    private ServiceDiscoveryResult? _discoveryResult;

    public ServiceFrameworkBackgroundService(
        IServiceDiscovery serviceDiscovery,
        IServiceRegistrar serviceRegistrar,
        IPubSubManager pubSubManager,
        ILogger<ServiceFrameworkBackgroundService> logger)
    {
        _serviceDiscovery = serviceDiscovery;
        _serviceRegistrar = serviceRegistrar;
        _pubSubManager = pubSubManager;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Service Framework Background Service");

        try
        {
            // Step 1: Discover all NATS services
            _discoveryResult = await _serviceDiscovery.DiscoverServicesAsync();

            // Step 2: Register all request-response services immediately
            var registrationResult = await _serviceRegistrar.RegisterRequestResponseServicesAsync(
                _discoveryResult.ReqRspServices, cancellationToken);

            _logger.LogInformation("Service registration completed during startup. " +
                                   "Registered: {RegisteredCount}, Failed: {FailedCount}",
                registrationResult.RegisteredServices.Count, registrationResult.FailedServices.Count);

            await base.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Service Framework Background Service");
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting pub-sub subscriptions");

        try
        {
            if (_discoveryResult == null)
            {
                _logger.LogWarning("No discovery result available - skipping pub-sub subscriptions");
                return;
            }

            // Step 3: Set up pub-sub subscriptions
            var subscriptionResult = await _pubSubManager.StartSubscriptionsAsync(
                _discoveryResult.PubSubServices, stoppingToken);

            _logger.LogInformation("Pub-sub subscription setup completed. " +
                                   "Successful: {SuccessCount}, Failed: {FailCount}",
                subscriptionResult.SuccessfulSubscriptions.Count, subscriptionResult.FailedSubscriptions.Count);

            // Keep the background service running to maintain subscriptions
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
            _logger.LogInformation("Pub-sub subscription management was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in pub-sub subscription management");
            throw;
        }
    }


    public Task ExecuteForTestingAsync(CancellationToken stoppingToken) => ExecuteAsync(stoppingToken);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Service Framework Background Service");

        try
        {
            // Stop all request-response services
            var serviceShutdownResult = await _serviceRegistrar.StopAllServicesAsync(cancellationToken);
            
            _logger.LogInformation("Service shutdown completed. " +
                                   "Stopped: {StoppedCount}, Failed: {FailedCount}",
                serviceShutdownResult.StoppedServices.Count, serviceShutdownResult.FailedServices.Count);

            // Stop all pub-sub subscriptions
            var unsubscriptionResult = await _pubSubManager.StopAllSubscriptionsAsync(cancellationToken);
            
            _logger.LogInformation("Subscription shutdown completed. " +
                                   "Stopped: {StoppedCount}, Failed: {FailedCount}",
                unsubscriptionResult.SuccessfulUnsubscriptions, unsubscriptionResult.FailedUnsubscriptions.Count);

            await base.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Service Framework Background Service");
            throw;
        }
    }
}