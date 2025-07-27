using System.Reflection;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;

/// <summary>
/// Interface for resolving channel names from service types and methods
/// </summary>
public interface IChannelResolver
{
    /// <summary>
    /// Gets the channel name for a service type and method
    /// Priority: method > class > default connection
    /// </summary>
    /// <param name="serviceType">Service type</param>
    /// <param name="method">Method information</param>
    /// <returns>Channel name</returns>
    string GetChannelName(Type serviceType, MethodInfo method);
}