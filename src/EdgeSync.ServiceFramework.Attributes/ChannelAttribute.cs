namespace EdgeSync.ServiceFramework.Attributes;

/// <summary>
/// Attribute to specify the NATS channel for a service or method
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class ChannelAttribute : Attribute
{
    /// <summary>
    /// Channel name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the ChannelAttribute
    /// </summary>
    /// <param name="name">Channel name</param>
    public ChannelAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}