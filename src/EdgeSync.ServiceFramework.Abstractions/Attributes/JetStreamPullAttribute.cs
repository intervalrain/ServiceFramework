using EdgeSync.ServiceFramework.Enums;

namespace EdgeSync.ServiceFramework.Abstractions.Attributes;

/// <summary>
/// Attribute to specify that a method should use JetStream Pull mode
/// This attribute forces the method to use Pull mode with JetStream enabled
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class JetStreamPullAttribute : Attribute
{
    /// <summary>
    /// Type of pull operation to use
    /// Default is Consume
    /// </summary>
    public PullType PullType { get; set; } = PullType.Consume;
    
    /// <summary>
    /// Consumer name for the pull subscription
    /// If not specified, will be auto-generated
    /// </summary>
    public string? ConsumerName { get; set; }
    
    /// <summary>
    /// Consumer group for the pull subscription
    /// If not specified, will use default group
    /// </summary>
    public string? ConsumerGroup { get; set; }
    
    /// <summary>
    /// Maximum number of messages to pull at once
    /// Default is 1
    /// </summary>
    public int MaxMessages { get; set; } = 10;
    
    /// <summary>
    /// Acknowledge policy for the consumer
    /// Default is Explicit
    /// </summary>
    public string AckPolicy { get; set; } = "Explicit";
    
    /// <summary>
    /// Whether to create the consumer if it doesn't exist
    /// Default is true
    /// </summary>
    public bool CreateConsumerIfNotExists { get; set; } = true;
}