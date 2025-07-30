namespace EdgeSync.ServiceFramework.Enums;


/// <summary>
/// Defines the different types of pull operations for JetStream consumers
/// </summary>
public enum PullType
{
    /// <summary>
    /// Consume messages continuously with automatic acknowledgment
    /// </summary>
    Consume,

    /// <summary>
    /// Fetch a batch of messages with timeout
    /// </summary>
    Fetch,

    /// <summary>
    /// Fetch available messages without waiting
    /// </summary>
    FetchNoWait,

    /// <summary>
    /// Get the next single message
    /// </summary>
    Next
}