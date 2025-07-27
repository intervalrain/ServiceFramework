using Microsoft.AspNetCore.Mvc.Filters;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;

/// <summary>
/// Interface for extracting request data from action execution context
/// </summary>
public interface IRequestDataExtractor
{
    /// <summary>
    /// Extracts request data from the action execution context
    /// </summary>
    /// <param name="context">The action executing context</param>
    /// <returns>Extracted request data or null if no data</returns>
    object? ExtractRequestData(ActionExecutingContext context);
}