using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Subscriptions;

/// <summary>
/// Factory for creating subscription handlers based on convention mode
/// </summary>
public interface ISubscriptionHandlerFactory
{
    /// <summary>
    /// Get the appropriate subscription handler for the given convention mode
    /// </summary>
    /// <param name="mode">Convention mode</param>
    /// <returns>Subscription handler</returns>
    /// <exception cref="NotSupportedException">When the mode is not supported</exception>
    ISubscriptionHandler GetHandler(ConventionMode mode);

    /// <summary>
    /// Register a new subscription handler
    /// </summary>
    /// <param name="handler">Subscription handler to register</param>
    void RegisterHandler(ISubscriptionHandler handler);
}