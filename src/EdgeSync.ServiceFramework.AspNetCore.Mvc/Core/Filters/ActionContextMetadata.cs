using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

using Microsoft.AspNetCore.Mvc.Abstractions;

namespace EdgeSync.ServiceFramework.Core.Filters;

public class ActionContextMetadata
{
    public string ServiceName { get; init; }
    public string MethodName { get; init; }
    public string Subject { get; init; }
    public ConventionMode ConventionMode { get; init; }
    public string ChannelName { get; init; }

    public ActionContextMetadata(ActionDescriptor actionDescriptor)
    {
        ServiceName = actionDescriptor.Properties[nameof(ServiceName)] as string ?? "Unknown";
        MethodName = actionDescriptor.Properties[nameof(MethodName)] as string ?? "Unknown";
        Subject = actionDescriptor.Properties[nameof(Subject)] as string ?? string.Empty;
        ConventionMode = (ConventionMode)(actionDescriptor.Properties[nameof(ConventionMode)] ?? ConventionMode.RequestResponse);
        ChannelName = actionDescriptor.Properties[nameof(ChannelName)] as string ?? string.Empty;
    }
}