using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;
using NATS.Client.Core;
using NATS.Client.Services;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;

/// <summary>
/// Interface for registering NATS services and handling service lifecycle
/// </summary>
public interface IServiceRegistrar
{
    /// <summary>
    /// Registers request-response services with NATS
    /// </summary>
    Task<ServiceRegistrationResult> RegisterRequestResponseServicesAsync(List<(Type ServiceType, List<NatsMethodInfo> Methods)> reqrspServices, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a NATS service server for the given service configuration
    /// </summary>
    Task<INatsSvcServer> CreateSvcServer(string serviceName, List<NatsMethodInfo> methods, INatsConnection connection, CancellationToken cancellationToken);
    
    /// <summary>
    /// Sets up request-response service group endpoints
    /// </summary>
    Task SetupRequestResponseServiceGroup(INatsConnection connection, INatsSvcServer svcServer, Type serviceType, string serviceName, List<NatsMethodInfo> methods, CancellationToken cancellationToken);
    
    /// <summary>
    /// Stops all registered services
    /// </summary>
    Task<ServiceShutdownResult> StopAllServicesAsync(CancellationToken cancellationToken = default);
}