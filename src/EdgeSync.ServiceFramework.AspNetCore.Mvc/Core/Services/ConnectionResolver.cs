using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;

/// <summary>
/// Implementation of connection resolver for NATS connections and serializers
/// </summary>
public class ConnectionResolver : IConnectionResolver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ConnectionResolver> _logger;
    private readonly EdgeSync.ServiceFramework.Abstractions.INatsConnectionFactory _connectionFactory;

    public ConnectionResolver(
        IServiceProvider serviceProvider,
        ILogger<ConnectionResolver> logger,
        EdgeSync.ServiceFramework.Abstractions.INatsConnectionFactory connectionFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _connectionFactory = connectionFactory;
    }

    public async Task<INatsConnection?> GetConnectionAsync(string? channelName)
    {
        try
        {
            var serviceFrameworkOptions = _serviceProvider.GetService<IOptions<ServiceFrameworkOptions>>()?.Value;

            if (serviceFrameworkOptions == null)
            {
                return await _connectionFactory.CreateConnectionAsync();
            }

            // Priority: specific channel > default connection setting > fallback to default
            if (!string.IsNullOrEmpty(channelName) &&
                serviceFrameworkOptions.Connections.TryGetValue(channelName, out var connectionSettings))
            {
                return await _connectionFactory.CreateConnectionAsync(connectionSettings);
            }
            else if (!string.IsNullOrEmpty(serviceFrameworkOptions.DefaultConnection) &&
                     serviceFrameworkOptions.Connections.TryGetValue(serviceFrameworkOptions.DefaultConnection, out var defaultSettings))
            {
                return await _connectionFactory.CreateConnectionAsync(defaultSettings);
            }
            else
            {
                return await _connectionFactory.CreateConnectionAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create NATS connection for channel '{ChannelName}'", channelName ?? "default");
            return null;
        }
    }

    public INatsSerializerRegistry GetSerializerForConnection(string? channelName)
    {
        var serviceFrameworkOptions = _serviceProvider.GetService<IOptions<ServiceFrameworkOptions>>()?.Value;

        if (serviceFrameworkOptions == null)
        {
            return NatsDefaultSerializerRegistry.Default;
        }

        // Find the connection settings for the specific channel
        if (!string.IsNullOrEmpty(channelName) &&
            serviceFrameworkOptions.Connections.TryGetValue(channelName, out var connectionSettings))
        {
            return connectionSettings.NatsSerializerRegistry;
        }

        // Try default connection if channelName is not found or empty
        if (!string.IsNullOrEmpty(serviceFrameworkOptions.DefaultConnection) &&
            serviceFrameworkOptions.Connections.TryGetValue(serviceFrameworkOptions.DefaultConnection, out var defaultSettings))
        {
            return defaultSettings.NatsSerializerRegistry;
        }

        // Fallback to default serializer registry
        return serviceFrameworkOptions.DefaultSerializerRegistry;
    }
}