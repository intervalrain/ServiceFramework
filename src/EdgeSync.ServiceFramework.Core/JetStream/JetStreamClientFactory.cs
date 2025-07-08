using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.JetStream;

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
        return new MsgBrokerJetStreamClient(
            _loggerFactory.CreateLogger<JetStreamClient>(), 
            GetConnectionFactory("broker"));
    }

    /// <summary>
    /// Creates a Message Bus JetStream client (uses "bus" connection)
    /// </summary>
    /// <returns>A new MsgBusJetStreamClient instance</returns>
    public IBusJetStreamClient CreateMsgBusClient()
    {
        return new MsgBusJetStreamClient(
            _loggerFactory.CreateLogger<JetStreamClient>(), 
            GetConnectionFactory("bus"));
    }

    /// <summary>
    /// Creates a JetStream client using a named connection
    /// </summary>
    /// <param name="name">The name of the connection to use</param>
    /// <returns>A JetStreamClient instance</returns>
    public IJetStreamClient CreateClient(string name)
    {
        return new JetStreamClient(
            _loggerFactory.CreateLogger<JetStreamClient>(), 
            GetConnectionFactory(name));
    }

    private INatsConnectionFactory GetConnectionFactory(string connectionName)
    {
        if (string.IsNullOrEmpty(connectionName) || 
            !_options.Connections.TryGetValue(connectionName, out var connectionSettings))
        {
            return _natsConnectionFactory;
        }

        return new NamedNatsConnectionFactory(
            _loggerFactory.CreateLogger<NamedNatsConnectionFactory>(), 
            connectionSettings);
    }
}

/// <summary>
/// A connection factory that uses specific connection settings
/// </summary>
internal class NamedNatsConnectionFactory : INatsConnectionFactory
{
    private readonly ILogger<NamedNatsConnectionFactory> _logger;
    private readonly NatsConnectionSettings _connectionSettings;

    public NamedNatsConnectionFactory(ILogger<NamedNatsConnectionFactory> logger, NatsConnectionSettings connectionSettings)
    {
        _logger = logger;
        _connectionSettings = connectionSettings;
    }

    public async Task<INatsConnection> CreateConnectionAsync(string url = "", string credFile = "", INatsSerializerRegistry? serializerRegistry = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionUrl = !string.IsNullOrEmpty(url) ? url : _connectionSettings.Url ?? throw new InvalidOperationException("Nats Url should not be empty");
            var connectionCredFile = !string.IsNullOrEmpty(credFile) ? credFile : _connectionSettings.CredFile;
            var registry = serializerRegistry != null ? serializerRegistry : _connectionSettings.NatsSerializerRegistry;

            var natOpts = NatsOpts.Default with
            {
                Name = _connectionSettings.Name,
                Url = connectionUrl,
                AuthOpts = new NatsAuthOpts
                {
                    CredsFile = connectionCredFile
                },
                SerializerRegistry = registry
            };

            var natsConnection = await NatsConnClient.CreateClientConnectionAsync(natOpts, _logger, cancellationToken: cancellationToken);
            _logger.LogInformation("NATS connection established for '{ConnectionName}'. {natOpts}", _connectionSettings.Name, natOpts);
            return natsConnection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish NATS connection for '{ConnectionName}'.", _connectionSettings.Name);
            throw;
        }
    }

    public async Task<INatsConnection> CreateConnectionAsync(NatsConnectionSettings setting, CancellationToken cancellationToken = default)
    {
        try
        {
            var natOpts = NatsOpts.Default with
            {
                Name = _connectionSettings.Name,
                Url = setting.Url ?? throw new InvalidOperationException("Nats Url should not be empty"),
                AuthOpts = new NatsAuthOpts
                {
                    CredsFile = setting.CredFile
                },
                SerializerRegistry = setting.NatsSerializerRegistry
            };

            var natsConnection = await NatsConnClient.CreateClientConnectionAsync(natOpts, _logger, cancellationToken: cancellationToken);
            _logger.LogInformation("NATS connection established for '{ConnectionName}'. {natOpts}", _connectionSettings.Name, natOpts);
            return natsConnection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish NATS connection for '{ConnectionName}'.", _connectionSettings.Name);
            throw;
        }
    }

}