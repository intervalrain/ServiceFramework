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
        ServiceName = actionDescriptor.Properties.TryGetValue(nameof(ServiceName), out var serviceName) 
            ? serviceName as string ?? "Unknown" 
            : "Unknown";
            
        MethodName = actionDescriptor.Properties.TryGetValue(nameof(MethodName), out var methodName) 
            ? methodName as string ?? "Unknown" 
            : "Unknown";
            
        Subject = actionDescriptor.Properties.TryGetValue(nameof(Subject), out var subject) 
            ? subject as string ?? string.Empty 
            : string.Empty;
        
        // Safely get ConventionMode with proper fallback
        if (actionDescriptor.Properties.TryGetValue(nameof(ConventionMode), out var conventionModeValue) && 
            conventionModeValue is ConventionMode mode)
        {
            ConventionMode = mode;
        }
        else
        {
            ConventionMode = ConventionMode.RequestResponse;
        }
        
        ChannelName = actionDescriptor.Properties.TryGetValue(nameof(ChannelName), out var channelName) 
            ? channelName as string ?? string.Empty 
            : string.Empty;
    }
}