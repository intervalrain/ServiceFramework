using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Subscriptions;

/// <summary>
/// Interface for handling different subscription modes
/// </summary>
public interface ISubscriptionHandler
{
    /// <summary>
    /// The convention mode this handler supports
    /// </summary>
    ConventionMode SupportedMode { get; }

    /// <summary>
    /// Subscribe to the NATS subject with the appropriate mode
    /// </summary>
    /// <param name="connection">NATS connection</param>
    /// <param name="serviceType">Service type</param>
    /// <param name="methodInfo">Method information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SubscribeAsync(INatsConnection connection, Type serviceType, NatsMethodInfo methodInfo, CancellationToken cancellationToken);
}