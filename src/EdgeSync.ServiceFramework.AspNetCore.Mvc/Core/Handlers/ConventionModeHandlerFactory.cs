using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Handlers;

/// <summary>
/// Factory for creating and managing convention mode handlers
/// </summary>
public class ConventionModeHandlerFactory : IConventionModeHandlerFactory
{
    private readonly Dictionary<ConventionMode, IConventionModeHandler> _handlers;
    private readonly ILogger<ConventionModeHandlerFactory> _logger;

    public ConventionModeHandlerFactory(
        IEnumerable<IConventionModeHandler> handlers,
        ILogger<ConventionModeHandlerFactory> logger)
    {
        _logger = logger;
        _handlers = new Dictionary<ConventionMode, IConventionModeHandler>();
        
        // Register all provided handlers
        foreach (var handler in handlers)
        {
            RegisterHandler(handler);
        }
        
        _logger.LogInformation("ConventionModeHandlerFactory initialized with {HandlerCount} handlers", _handlers.Count);
    }

    public IConventionModeHandler GetHandler(ConventionMode mode)
    {
        // Handle flag-based modes by checking if any registered handler supports this mode
        foreach (var kvp in _handlers)
        {
            var supportedMode = kvp.Key;
            var handler = kvp.Value;
            
            // Check if the handler supports this specific mode (for flag-based enums)
            if ((supportedMode & mode) == mode)
            {
                _logger.LogDebug("Found handler {HandlerType} for convention mode {Mode}", 
                    handler.GetType().Name, mode);
                return handler;
            }
        }

        var supportedModes = string.Join(", ", _handlers.Keys);
        var errorMessage = $"No handler found for convention mode: {mode}. Supported modes: {supportedModes}";
        
        _logger.LogError(errorMessage);
        throw new NotSupportedException(errorMessage);
    }

    public void RegisterHandler(IConventionModeHandler handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var supportedMode = handler.SupportedMode;
        
        // For flag-based enums, we might have overlapping modes - handle this gracefully
        if (_handlers.ContainsKey(supportedMode))
        {
            _logger.LogWarning("Handler for convention mode {Mode} is being replaced. Old: {OldHandler}, New: {NewHandler}",
                supportedMode, _handlers[supportedMode].GetType().Name, handler.GetType().Name);
        }

        _handlers[supportedMode] = handler;
        
        _logger.LogDebug("Registered convention mode handler {HandlerType} for mode {Mode}",
            handler.GetType().Name, supportedMode);
    }

    /// <summary>
    /// Gets all registered handlers for debugging/monitoring purposes
    /// </summary>
    public IReadOnlyDictionary<ConventionMode, IConventionModeHandler> GetAllHandlers()
    {
        return _handlers.AsReadOnly();
    }

    /// <summary>
    /// Checks if a handler is registered for the specified mode
    /// </summary>
    public bool IsHandlerRegistered(ConventionMode mode)
    {
        return _handlers.Keys.Any(supportedMode => (supportedMode & mode) == mode);
    }
}