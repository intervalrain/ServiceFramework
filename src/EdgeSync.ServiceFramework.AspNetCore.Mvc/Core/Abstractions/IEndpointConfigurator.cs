using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;
using NATS.Client.Core;
using NATS.Client.Services;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;

/// <summary>
/// Interface for configuring NATS service endpoints
/// </summary>
public interface IEndpointConfigurator
{
    /// <summary>
    /// Configures a typed endpoint for a NATS service
    /// </summary>
    /// <typeparam name="T">Request type</typeparam>
    /// <param name="svcServer">NATS service server</param>
    /// <param name="serviceType">Service type</param>
    /// <param name="methodInfo">Method information</param>
    /// <param name="endpointName">Endpoint name</param>
    /// <param name="connection">NATS connection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the configuration operation</returns>
    Task ConfigureEndpointAsync<T>(INatsSvcServer svcServer, Type serviceType, 
        NatsMethodInfo methodInfo, string endpointName, 
        INatsConnection connection, CancellationToken cancellationToken) where T : class;
        
    /// <summary>
    /// Configures a parameterless endpoint for a NATS service
    /// </summary>
    /// <param name="svcServer">NATS service server</param>
    /// <param name="serviceType">Service type</param>
    /// <param name="methodInfo">Method information</param>
    /// <param name="endpointName">Endpoint name</param>
    /// <param name="connection">NATS connection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the configuration operation</returns>
    Task ConfigureParameterlessEndpointAsync(INatsSvcServer svcServer, Type serviceType,
        NatsMethodInfo methodInfo, string endpointName,
        INatsConnection connection, CancellationToken cancellationToken);
}