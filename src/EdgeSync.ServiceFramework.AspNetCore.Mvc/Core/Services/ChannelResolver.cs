using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.Abstractions;
using EdgeSync.ServiceFramework.Abstractions.Attributes;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;

/// <summary>
/// Implementation of channel resolver for determining channel names from attributes
/// </summary>
public class ChannelResolver : IChannelResolver
{
    private readonly ServiceFrameworkOptions _serviceFrameworkOptions;

    public ChannelResolver(IOptions<ServiceFrameworkOptions> serviceFrameworkOptions)
    {
        _serviceFrameworkOptions = serviceFrameworkOptions.Value;
    }

    public string GetChannelName(Type serviceType, MethodInfo method)
    {
        // Priority: method > class > default connection

        // 1. Check method-level Channel attribute
        var methodChannel = method.GetCustomAttribute<ChannelAttribute>();
        if (methodChannel != null)
        {
            return methodChannel.Name;
        }

        // 2. Check class-level Channel attribute  
        var classChannel = serviceType.GetCustomAttribute<ChannelAttribute>();
        if (classChannel != null)
        {
            return classChannel.Name;
        }

        // 3. Use empty string for default connection
        return _serviceFrameworkOptions.DefaultConnection ?? string.Empty;
    }
}