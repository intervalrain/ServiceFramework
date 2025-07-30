using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;

namespace EdgeSync.ServiceFramework.Core.Abstractions;

/// <summary>
/// Interface for building NATS method information with decision logic
/// </summary>
public interface IMethodInfoBuilder
{
    /// <summary>
    /// Gets NATS methods with convention decision applied
    /// </summary>
    /// <param name="service">NATS service instance</param>
    /// <returns>Collection of NATS method information</returns>
    IEnumerable<NatsMethodInfo> GetNatsMethodsWithDecision(NatsService service);
}