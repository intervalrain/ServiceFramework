using System.Collections.Concurrent;

using EdgeSync.ServiceFramework.Core;

using Microsoft.Extensions.Logging;

using NATS.Client.Core;

namespace EdgeSync.ServiceFramework;

public class NatsConnectionFactory : INatsConnectionFactory
{
    private readonly ILogger<NatsConnectionFactory> _logger;

    public NatsConnectionFactory(ILogger<NatsConnectionFactory> logger)
    {
        _logger = logger;
    }

    public async Task<INatsConnection> CreateConnectionAsync(string url = "", string credFile = "", INatsSerializerRegistry? serializerRegistry = null,
                                                    CancellationToken cancellationToken = default)
    {
        try
        {
            var natOpts = NatsOpts.Default with
            {
                Url = url == "" ? ServiceConfig.MsgBusUrl : url,
                AuthOpts = new NatsAuthOpts
                {
                    CredsFile = credFile == "" ? ServiceConfig.MsgBusCredFile : credFile
                },
                SerializerRegistry = serializerRegistry ?? NatsDefaultSerializerRegistry.Default
            };
            var natsConnection = await NatsConnClient.CreateClientConnectionAsync(
                natOpts,
                _logger,
                cancellationToken: cancellationToken);
            _logger.LogInformation("NATS connection established. {natOpts}", natOpts);
            return natsConnection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish NATS connection.");
            throw;
        }
    }

    public async Task<INatsConnection> CreateConnectionAsync(NatsConnectionSettings setting, CancellationToken cancellationToken = default)
    {
        try
        {
            var natOpts = NatsOpts.Default with
            {
                Name = setting.Name,
                Url = setting.Url ?? ServiceConfig.MsgBusUrl ?? throw new InvalidOperationException("NATS connection url should not be empty."),
                AuthOpts = new NatsAuthOpts
                {
                    CredsFile = setting.CredFile ?? ServiceConfig.MsgBusCredFile
                },
                SerializerRegistry = setting.NatsSerializerRegistry
            };

            var natsConnection = await NatsConnClient.CreateClientConnectionAsync(
                natOpts,
                _logger,
                cancellationToken: cancellationToken);
            _logger.LogInformation("NATS connection established. {natOpts}", natOpts);
            return natsConnection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish NATS connection.");
            throw;
        }
    }
}

/// <summary>
/// A connection factory that uses specific connection settings
/// </summary>
internal class NamedNatsConnectionFactory : INatsConnectionFactory
{
    private readonly ILogger<NamedNatsConnectionFactory> _logger;
    private readonly NatsConnectionSettings _connectionSettings;
    public static readonly ConcurrentDictionary<string, INatsConnection> Connections = [];    

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

            INatsConnection natsConnection;

            if (Connections.ContainsKey(natOpts.Name))
            {
                natsConnection = Connections[natOpts.Name];
            }
            else
            {
                natsConnection = await NatsConnClient.CreateClientConnectionAsync(natOpts, _logger, cancellationToken: cancellationToken);
                Connections[natOpts.Name] = natsConnection;
            }

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

            INatsConnection natsConnection;

            if (Connections.ContainsKey(natOpts.Name))
            {
                natsConnection = Connections[natOpts.Name];
            }
            else
            {
                natsConnection = await NatsConnClient.CreateClientConnectionAsync(natOpts, _logger, cancellationToken: cancellationToken);
                Connections[natOpts.Name] = natsConnection;
            }
            
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