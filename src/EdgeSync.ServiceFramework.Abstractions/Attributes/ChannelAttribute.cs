namespace EdgeSync.ServiceFramework.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ChannelAttribute(string name) : Attribute
{
    public string Name { get; } = name;
} 