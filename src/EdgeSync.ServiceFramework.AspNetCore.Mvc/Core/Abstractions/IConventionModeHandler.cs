using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Core.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;

/// <summary>
/// Interface for handling specific convention modes
/// </summary>
public interface IConventionModeHandler
{
    /// <summary>
    /// The convention mode supported by this handler
    /// </summary>
    ConventionMode SupportedMode { get; }
    
    /// <summary>
    /// Handles the request for the specific convention mode
    /// </summary>
    /// <param name="context">Action execution context</param>
    /// <param name="connection">NATS connection</param>
    /// <param name="metadata">Action context metadata</param>
    /// <param name="wrappedRequest">The wrapped request data</param>
    /// <returns>Action result</returns>
    Task<IActionResult> HandleAsync(ActionExecutingContext context, INatsConnection connection, 
        ActionContextMetadata metadata, object? wrappedRequest);
}