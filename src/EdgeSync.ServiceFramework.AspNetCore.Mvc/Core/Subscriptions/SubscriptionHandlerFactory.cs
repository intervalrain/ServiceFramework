using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Subscriptions;

/// <summary>
/// Factory implementation for creating subscription handlers
/// </summary>
public class SubscriptionHandlerFactory : ISubscriptionHandlerFactory
{
    private readonly Dictionary<ConventionMode, ISubscriptionHandler> _handlers;

    public SubscriptionHandlerFactory(IEnumerable<ISubscriptionHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.SupportedMode, h => h);
    }

    public ISubscriptionHandler GetHandler(ConventionMode mode)
    {
        if (_handlers.TryGetValue(mode, out var handler))
        {
            return handler;
        }

        throw new NotSupportedException($"No subscription handler registered for convention mode: {mode}");
    }

    public void RegisterHandler(ISubscriptionHandler handler)
    {
        _handlers[handler.SupportedMode] = handler;
    }
}