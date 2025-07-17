using System.Globalization;
using System.Reflection;

using EdgeSync.ServiceFramework.Abstractions;

using EdgeSync.ServiceFramework.Core.HealthChecks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace EdgeSync.ServiceFramework.Extensions;

/// <summary>
/// Extension methods for adding NATS health checks
/// </summary>
public static class HealthChecksBuilderExtensions
{
    /// <summary>
    /// Adds health checks for all registered NATS connections
    /// </summary>
    /// <param name="builder">The health checks builder</param>
    /// <returns>The health checks builder for chaining</returns>
    public static IHealthChecksBuilder AddNatsCheck(this IHealthChecksBuilder builder)
    {
        // Add a health check that will dynamically discover connections at runtime
        var options = builder.Services.BuildServiceProvider().GetService<IOptions<ServiceFrameworkOptions>>()?.Value ??
            throw new Exception($"{nameof(ServiceFrameworkOptions)} not definded");

        builder.AddNatsCheckInternal(options.Connections.Select(kvp => kvp.Key).ToArray());

        return builder;
    }

    /// <summary>
    /// Adds health checks for specific named NATS connections
    /// </summary>
    /// <param name="builder">The health checks builder</param>
    /// <param name="connectionNames">The names of the connections to check</param>
    /// <returns>The health checks builder for chaining</returns>
    public static IHealthChecksBuilder AddNatsCheck(this IHealthChecksBuilder builder, params string[] connectionNames)
    {
        if (connectionNames == null || connectionNames.Length == 0)
        {
            return AddNatsCheck(builder);
        }

        return AddNatsCheckInternal(builder, connectionNames);
    }

    private static IHealthChecksBuilder AddNatsCheckInternal(this IHealthChecksBuilder builder, string[] connectionNames)
    {
        var ns = GetNamespacePrefix();
        foreach (var connectionName in connectionNames)
        {
            if (string.IsNullOrWhiteSpace(connectionName))
                throw new ArgumentNullException(nameof(connectionName));

            builder.AddTypeActivatedCheck<NatsConnectionHealthCheck>(
                name: $"{ns} {ToPascalCase(connectionName)} NatsConnection Check",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["nats", $"nats-{connectionName}", connectionName],
                args: [connectionName]);
        }

        return builder;
    }

    private static string GetNamespacePrefix()
    {
        var assembly = Assembly.GetEntryAssembly();

        var ns = assembly?.EntryPoint?.DeclaringType?.Namespace;
        if (string.IsNullOrWhiteSpace(ns))
            return "EdgeSync";
        
        var segments = ns.Split('.');
        if (segments.Length >= 2)
            return $"{segments[0]}.{segments[1]}";

        return segments[0];
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Trim());
    }
}