using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;

/// <summary>
/// Interface for discovering and categorizing NATS services
/// </summary>
public interface IServiceDiscovery
{
    /// <summary>
    /// Discovers all NATS services and categorizes them by convention mode
    /// </summary>
    /// <returns>Service discovery result containing categorized services</returns>
    Task<ServiceDiscoveryResult> DiscoverServicesAsync();
}

/// <summary>
/// Result of service discovery containing categorized services
/// </summary>
public class ServiceDiscoveryResult
{
    /// <summary>
    /// Services that use pub-sub patterns
    /// </summary>
    public List<(Type ServiceType, List<NatsMethodInfo> Methods)> PubSubServices { get; set; } = new();
    
    /// <summary>
    /// Services that use request-response patterns
    /// </summary>
    public List<(Type ServiceType, List<NatsMethodInfo> Methods)> ReqRspServices { get; set; } = new();
}