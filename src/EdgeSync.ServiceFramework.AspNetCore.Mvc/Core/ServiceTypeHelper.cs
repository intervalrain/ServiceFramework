namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;

/// <summary>
/// Helper class for service type operations
/// </summary>
public static class ServiceTypeHelper
{
    /// <summary>
    /// Gets all service interfaces for a given service type, excluding INatsService
    /// </summary>
    /// <param name="serviceType">The service type to analyze</param>
    /// <returns>List of service interfaces</returns>
    public static List<Type> GetServiceInterfaces(Type serviceType)
    {
        return serviceType.GetInterfaces()
            .Where(i => i.Name.StartsWith("I") &&
                       i.Name.EndsWith("Service") &&
                       i != typeof(INatsService))
            .ToList();
    }
}