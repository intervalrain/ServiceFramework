using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;

/// <summary>
/// Interface for managing pub-sub subscriptions
/// </summary>
public interface IPubSubManager
{
    /// <summary>
    /// Starts all pub-sub subscriptions for the given services
    /// </summary>
    /// <param name="pubsubServices">List of pub-sub services and their methods</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of subscription operation</returns>
    Task<PubSubSubscriptionResult> StartSubscriptionsAsync(List<(Type ServiceType, List<NatsMethodInfo> Methods)> pubsubServices, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stops all active subscriptions
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of unsubscription operation</returns>
    Task<PubSubUnsubscriptionResult> StopAllSubscriptionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of pub-sub subscription operation
/// </summary>
public class PubSubSubscriptionResult
{
    public List<(Type ServiceType, NatsMethodInfo Method)> SuccessfulSubscriptions { get; set; } = new();
    public List<(Type ServiceType, NatsMethodInfo Method, string ErrorMessage)> FailedSubscriptions { get; set; } = new();
}

/// <summary>
/// Result of pub-sub unsubscription operation  
/// </summary>
public class PubSubUnsubscriptionResult
{
    public int SuccessfulUnsubscriptions { get; set; }
    public List<string> FailedUnsubscriptions { get; set; } = new();
}