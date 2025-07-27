using NATS.Client.Services;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;

/// <summary>
/// Interface for handling NATS service replies with audit information
/// </summary>
public interface IReplyHandler
{
    /// <summary>
    /// Replies with audit wrapper information
    /// </summary>
    /// <typeparam name="T">Message type</typeparam>
    /// <param name="msg">NATS service message</param>
    /// <param name="response">Response object</param>
    /// <param name="reqSeqId">Request sequence ID</param>
    /// <param name="userId">User ID</param>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <returns>Task representing the reply operation</returns>
    Task ReplyWithAuditWrapperAsync<T>(NatsSvcMsg<T> msg, object response, 
        Guid reqSeqId, string? userId, string? tenantId, string? correlationId) where T : class;
        
    /// <summary>
    /// Replies with empty message for parameterless methods
    /// </summary>
    /// <typeparam name="T">Message type</typeparam>
    /// <param name="msg">NATS service message</param>
    /// <param name="response">Response object</param>
    /// <param name="reqSeqId">Request sequence ID</param>
    /// <param name="userId">User ID</param>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <returns>Task representing the reply operation</returns>
    Task ReplyWithEmptyMessageAsync<T>(NatsSvcMsg<T> msg, object response,
        Guid reqSeqId, string? userId, string? tenantId, string? correlationId) where T : class;
}