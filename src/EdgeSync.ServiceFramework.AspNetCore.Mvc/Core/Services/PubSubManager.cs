using System.Reflection;
using EdgeSync.ServiceFramework.Abstractions.Attributes;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Subscriptions;
using EdgeSync.ServiceFramework.Core.Abstractions;

using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;

/// <summary>
/// Pub-sub subscription manager implementation
/// Extracted from ServiceFrameworkBackgroundService to follow SRP
/// Handles pub-sub subscription lifecycle management
/// </summary>
public class PubSubManager : IPubSubManager
{
    private readonly IConnectionResolver _connectionResolver;
    private readonly ISubscriptionHandlerFactory _subscriptionHandlerFactory;
    private readonly ILogger<PubSubManager> _logger;

    public PubSubManager(
        IConnectionResolver connectionResolver,
        ISubscriptionHandlerFactory subscriptionHandlerFactory,
        ILogger<PubSubManager> logger)
    {
        _connectionResolver = connectionResolver;
        _subscriptionHandlerFactory = subscriptionHandlerFactory;
        _logger = logger;
    }

    public async Task<PubSubSubscriptionResult> StartSubscriptionsAsync(
        List<(Type ServiceType, List<NatsMethodInfo> Methods)> pubsubServices,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting pub-sub subscriptions for {ServiceCount} services", pubsubServices.Count);

        var result = new PubSubSubscriptionResult();
        var subscriptionTasks = new List<Task>();

        foreach (var (serviceType, methods) in pubsubServices)
        {
            foreach (var method in methods)
            {
                var task = Task.Run(async () =>
                {
                    try
                    {
                        var channelName = GetChannelName(serviceType, method);
                        var connection = await _connectionResolver.GetConnectionAsync(channelName);
                        
                        if (connection == null)
                        {
                            var errorMessage = $"Connection unavailable for channel {channelName}";
                            result.FailedSubscriptions.Add((serviceType, method, errorMessage));
                            _logger.LogWarning("Cannot subscribe to method {MethodName} on service {ServiceType} - {ErrorMessage}",
                                method.Method.Name, serviceType.Name, errorMessage);
                            return;
                        }

                        await SubscribeToMethod(connection, serviceType, method, cancellationToken);
                        result.SuccessfulSubscriptions.Add((serviceType, method));
                        
                        _logger.LogDebug("Successfully subscribed to method {MethodName} on service {ServiceType}",
                            method.Method.Name, serviceType.Name);
                    }
                    catch (Exception ex)
                    {
                        result.FailedSubscriptions.Add((serviceType, method, ex.Message));
                        _logger.LogError(ex, "Failed to subscribe to method {MethodName} on service {ServiceType}",
                            method.Method.Name, serviceType.Name);
                    }
                }, cancellationToken);

                subscriptionTasks.Add(task);
            }
        }

        await Task.WhenAll(subscriptionTasks);

        _logger.LogInformation("Pub-sub subscription completed. Successful: {SuccessCount}, Failed: {FailCount}",
            result.SuccessfulSubscriptions.Count, result.FailedSubscriptions.Count);

        return result;
    }

    public async Task<PubSubUnsubscriptionResult> StopAllSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping pub-sub subscriptions");

        var result = new PubSubUnsubscriptionResult();

        // Note: In the current architecture, subscription handlers manage their own lifecycle
        // The subscriptions are tied to the NATS connections which are managed elsewhere
        // This method serves as a placeholder for future subscription management enhancements
        
        _logger.LogInformation("Pub-sub subscription stop completed");

        return result;
    }

    private async Task SubscribeToMethod(INatsConnection connection, Type serviceType, NatsMethodInfo methodInfo, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Subscribing to subject: {Subject} for method: {Method} with mode: {Mode} on connection {ConnectionId}",
            methodInfo.SubjectName, methodInfo.Method.Name, methodInfo.ConventionMode, connection.ServerInfo?.ClientId);

        try
        {
            var handler = _subscriptionHandlerFactory.GetHandler(methodInfo.ConventionMode);
            await handler.SubscribeAsync(connection, serviceType, methodInfo, cancellationToken);

            _logger.LogInformation("Successfully subscribed to subject: {Subject} with mode: {Mode} on connection {ConnectionId}",
                methodInfo.SubjectName, methodInfo.ConventionMode, connection.ServerInfo?.ClientId);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Unsupported convention mode: {Mode} for method: {Method}", methodInfo.ConventionMode, methodInfo.Method.Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to subject: {Subject} with mode: {Mode} on connection {ConnectionId}",
                methodInfo.SubjectName, methodInfo.ConventionMode, connection.ServerInfo?.ClientId);
            throw;
        }
    }

    private string GetChannelName(Type serviceType, NatsMethodInfo methodInfo)
    {
        // Priority: method > class > default connection
        // This logic should be consistent with ServiceRegistrar
        
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

        // 3. Use default connection (empty string means default)
        return string.Empty;
    }
}