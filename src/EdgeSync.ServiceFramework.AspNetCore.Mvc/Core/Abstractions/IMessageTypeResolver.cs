using System.Reflection;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;

/// <summary>
/// Interface for resolving message types from method signatures
/// </summary>
public interface IMessageTypeResolver
{
    /// <summary>
    /// Resolves the message type for subscription based on method parameters
    /// </summary>
    /// <param name="method">The method to analyze</param>
    /// <param name="enableAuditWrapper">Whether audit wrapper is enabled</param>
    /// <returns>The resolved message type for subscription</returns>
    Type ResolveMessageType(MethodInfo method, bool enableAuditWrapper);
    
    /// <summary>
    /// Resolves the response type for request-response pattern
    /// </summary>
    /// <param name="method">The method to analyze</param>
    /// <param name="enableAuditWrapper">Whether audit wrapper is enabled</param>
    /// <returns>The resolved response type</returns>
    Type ResolveResponseType(MethodInfo method, bool enableAuditWrapper);
    
    /// <summary>
    /// Determines if the method expects a collection parameter
    /// </summary>
    /// <param name="method">The method to analyze</param>
    /// <returns>True if the method expects collection parameters</returns>
    bool IsCollectionParameter(MethodInfo method);
}