namespace EdgeSync.ServiceFramework;

/// <summary>
/// Configuration options for NATS API connections and services
/// </summary>
public class NatsApiOptions
{
    /// <summary>
    /// URL for the message broker NATS connection
    /// </summary>
    public string? MsgBrokerUrl { get; set; } = null;
    
    /// <summary>
    /// URL for the message bus NATS connection
    /// </summary>
    public string? MsgBusUrl { get; set; } = null;
    
    /// <summary>
    /// Path to the credential file for message broker authentication
    /// </summary>
    public string? MsgBrokerCredFile { get; set; } = null;
    
    /// <summary>
    /// Path to the credential file for message bus authentication
    /// </summary>
    public string? MsgBusCredFile { get; set; } = null;
}