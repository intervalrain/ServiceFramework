using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;

/// <summary>
/// Factory interface for convention mode handlers
/// </summary>
public interface IConventionModeHandlerFactory
{
    /// <summary>
    /// Gets the appropriate handler for the specified convention mode
    /// </summary>
    /// <param name="mode">The convention mode</param>
    /// <returns>Convention mode handler</returns>
    IConventionModeHandler GetHandler(ConventionMode mode);
    
    /// <summary>
    /// Registers a handler for a specific convention mode
    /// </summary>
    /// <param name="handler">The handler to register</param>
    void RegisterHandler(IConventionModeHandler handler);
}