namespace EdgeSync.ServiceFramework.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Interface)]
public class ServiceInfoAttribute : Attribute
{
    public string? ServiceName { get; set; }
    public string? ServiceVersion { get; set; }
    public string? QueueGroup { get; set; }
    public string? Description { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = [];
}