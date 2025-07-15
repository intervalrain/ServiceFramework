using EdgeSync.ServiceFramework.Abstractions;
using EdgeSync.ServiceFramework.Abstractions.JetStream;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EdgeSync.ServiceFramework.Core.JetStream;

/// <summary>
/// Factory for creating JetStream clients
/// </summary>
public class JetStreamClientFactory : IJetStreamClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly INatsConnectionFactory _natsConnectionFactory;
    private readonly ServiceFrameworkOptions _options;

    /// <summary>
    /// Initializes a new instance of the JetStreamClientFactory class
    /// </summary>
    /// <param name="loggerFactory">The logger factory</param>
    /// <param name="natsConnectionFactory">The NATS connection factory</param>
    /// <param name="serviceOptions">Service framework options</param>
    public JetStreamClientFactory(
        ILoggerFactory loggerFactory, 
        INatsConnectionFactory natsConnectionFactory,
        IOptions<ServiceFrameworkOptions> serviceOptions)
    {
        _loggerFactory = loggerFactory;
        _natsConnectionFactory = natsConnectionFactory;
        _options = serviceOptions?.Value ?? new ServiceFrameworkOptions();
        
        _options.Validate();
    }

    /// <summary>
    /// Creates a Message Broker JetStream client (uses "broker" connection)
    /// </summary>
    /// <returns>A new MsgBrokerJetStreamClient instance</returns>
    public IBrokerJetStreamClient CreateMsgBrokerClient()
    {
        var connectionSettings = GetConnectionSettings("broker");
        return new MsgBrokerJetStreamClient(
            _loggerFactory.CreateLogger<JetStreamClient>(), 
            _natsConnectionFactory,
            connectionSettings);
    }

    /// <summary>
    /// Creates a Message Bus JetStream client (uses "bus" connection)
    /// </summary>
    /// <returns>A new MsgBusJetStreamClient instance</returns>
    public IBusJetStreamClient CreateMsgBusClient()
    {
        var connectionSettings = GetConnectionSettings("bus");
        return new MsgBusJetStreamClient(
            _loggerFactory.CreateLogger<JetStreamClient>(), 
            _natsConnectionFactory,
            connectionSettings);
    }

    /// <summary>
    /// Creates a JetStream client using a named connection
    /// </summary>
    /// <param name="name">The name of the connection to use</param>
    /// <returns>A JetStreamClient instance</returns>
    public IJetStreamClient CreateClient(string? name = null)
    {
        var connectionSettings = GetConnectionSettings(name);
        return new JetStreamClient(
            _loggerFactory.CreateLogger<JetStreamClient>(), 
            _natsConnectionFactory,
            connectionSettings);
    }

    private NatsConnectionSettings? GetConnectionSettings(string? connectionName)
    {
        if (string.IsNullOrEmpty(connectionName))
        {
            // Use default connection if no name specified
            if (!string.IsNullOrEmpty(_options.DefaultConnection) &&
                _options.Connections.TryGetValue(_options.DefaultConnection, out var defaultSettings))
            {
                return defaultSettings;
            }
            
            // Fallback to first available connection
            return _options.Connections.Values.FirstOrDefault();
        }

        // Return specific named connection settings
        _options.Connections.TryGetValue(connectionName, out var settings);
        return settings;
    }
}