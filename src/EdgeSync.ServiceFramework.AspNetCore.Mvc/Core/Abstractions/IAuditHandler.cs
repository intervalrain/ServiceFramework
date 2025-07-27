using Microsoft.AspNetCore.Http;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;

/// <summary>
/// Interface for handling audit information wrapping and extraction
/// </summary>
public interface IAuditHandler
{
    /// <summary>
    /// Wraps request data with audit information
    /// </summary>
    /// <param name="requestData">The original request data</param>
    /// <param name="httpContext">HTTP context containing user information</param>
    /// <returns>Wrapped request with audit information</returns>
    object? WrapRequestWithAudit(object? requestData, HttpContext httpContext);
    
    /// <summary>
    /// Extracts audit information from wrapped request
    /// </summary>
    /// <param name="wrappedRequest">The wrapped request object</param>
    /// <returns>Audit information tuple or null if not available</returns>
    (string ReqSeqId, string Timestamp)? ExtractAuditInfo(object? wrappedRequest);
    
    /// <summary>
    /// Extracts audit information from response
    /// </summary>
    /// <param name="response">The response object</param>
    /// <returns>Response audit information tuple or null if not available</returns>
    (string ReqSeqId, string RspSeqId, string Timestamp)? ExtractResponseAuditInfo(object? response);
}