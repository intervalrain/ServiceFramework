using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Abstractions;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Handlers;

/// <summary>
/// Factory for creating convention handlers
/// </summary>
public class ConventionHandlerFactory
{
    private readonly Dictionary<ConventionMode, IConventionHandler> _handlers;

    public ConventionHandlerFactory(IAutoConventionRouteBuilder routeBuilder)
    {
        _handlers = new Dictionary<ConventionMode, IConventionHandler>
        {
            { ConventionMode.RequestResponse, new RequestResponseHandler(routeBuilder) },
            { ConventionMode.PubSubPushJetStream, new PubSubPushJetStreamHandler(routeBuilder) },
            { ConventionMode.PubSubPullJetStream, new PubSubPullJetStreamHandler(routeBuilder) },
            { ConventionMode.PubSubPushClassic, new PubSubPushClassicHandler(routeBuilder) },
        };
    }

    /// <summary>
    /// Get handler for specific convention mode
    /// </summary>
    /// <param name="mode">Convention mode</param>
    /// <returns>Convention handler</returns>
    /// <exception cref="ArgumentException">When mode is not supported</exception>
    public IConventionHandler GetHandler(ConventionMode mode)
    {
        if (_handlers.TryGetValue(mode, out var handler))
        {
            return handler;
        }

        throw new ArgumentException($"Unsupported convention mode: {mode}", nameof(mode));
    }

    /// <summary>
    /// Get all available handlers
    /// </summary>
    /// <returns>All handlers</returns>
    public IEnumerable<IConventionHandler> GetAllHandlers()
    {
        return _handlers.Values;
    }
}