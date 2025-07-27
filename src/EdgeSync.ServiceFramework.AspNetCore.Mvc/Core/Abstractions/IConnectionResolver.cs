using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;

/// <summary>
/// Interface for resolving NATS connections and serializers
/// </summary>
public interface IConnectionResolver
{
    /// <summary>
    /// Gets a NATS connection for the specified channel
    /// </summary>
    /// <param name="channelName">The channel name, or null for default connection</param>
    /// <returns>NATS connection or null if connection fails</returns>
    Task<INatsConnection?> GetConnectionAsync(string? channelName);
    
    /// <summary>
    /// Gets the serializer registry for the specified connection
    /// </summary>
    /// <param name="channelName">The channel name, or null for default connection</param>
    /// <returns>NATS serializer registry</returns>
    INatsSerializerRegistry GetSerializerForConnection(string? channelName);
}