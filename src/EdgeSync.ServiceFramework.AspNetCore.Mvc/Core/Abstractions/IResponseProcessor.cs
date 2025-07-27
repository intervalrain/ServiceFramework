using Microsoft.AspNetCore.Mvc;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;

/// <summary>
/// Interface for processing and unwrapping responses
/// </summary>
public interface IResponseProcessor
{
    /// <summary>
    /// Processes response based on exception handler configuration
    /// </summary>
    /// <param name="response">The response object</param>
    /// <param name="useExceptionHandler">Whether to use exception handler mode</param>
    /// <returns>Processed action result</returns>
    IActionResult ProcessResponse(object? response, bool useExceptionHandler);
    
    /// <summary>
    /// Gets response with appropriate HTTP status code
    /// </summary>
    /// <param name="response">The response object</param>
    /// <returns>Action result with status code</returns>
    IActionResult GetResponseWithStatus(object? response);
    
    /// <summary>
    /// Unwraps response from wrapper types (ResponseDto, ErrorOr)
    /// </summary>
    /// <param name="response">The response object</param>
    /// <returns>Unwrapped action result</returns>
    IActionResult UnwrapResponse(object? response);
}