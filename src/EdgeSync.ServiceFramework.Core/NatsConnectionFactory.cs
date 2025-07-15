using System.Collections.Concurrent;

using EdgeSync.ServiceFramework.Abstractions;

using Microsoft.Extensions.Logging;

using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.Core;

public class NatsConnectionFactory : INatsConnectionFactory
{
    private readonly ILogger<NatsConnectionFactory> _logger;
    private static readonly ConcurrentDictionary<string, INatsConnection> _connections = new();

    public NatsConnectionFactory(ILogger<NatsConnectionFactory> logger)
    {
        _logger = logger;
    }

    public async Task<INatsConnection> CreateConnectionAsync(string url = "", string credFile = "", INatsSerializerRegistry? serializerRegistry = null,
                                                    CancellationToken cancellationToken = default)
    {
        try
        {
            var actualUrl = url == "" ? ServiceConfig.MsgBusUrl : url;
            var actualCredFile = credFile == "" ? ServiceConfig.MsgBusCredFile : credFile;
            var actualRegistry = serializerRegistry ?? NatsDefaultSerializerRegistry.Default;
            
            // Create connection key for pooling
            var connectionKey = $"{actualUrl}|{actualCredFile}|{actualRegistry.GetType().Name}";
            
            if (_connections.TryGetValue(connectionKey, out var existingConnection))
            {
                _logger.LogDebug("Reusing existing NATS connection for key: {ConnectionKey}", connectionKey);
                return existingConnection;
            }

            var natOpts = NatsOpts.Default with
            {
                Url = actualUrl,
                AuthOpts = new NatsAuthOpts
                {
                    CredsFile = actualCredFile
                },
                SerializerRegistry = actualRegistry
            };
            
            var natsConnection = await NatsConnClient.CreateClientConnectionAsync(
                natOpts,
                _logger,
                cancellationToken: cancellationToken);
            
            _connections[connectionKey] = natsConnection;
            _logger.LogInformation("NATS connection established and cached. {natOpts}", natOpts);
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
            var actualUrl = setting.Url ?? ServiceConfig.MsgBusUrl ?? throw new InvalidOperationException("NATS connection url should not be empty.");
            var actualCredFile = setting.CredFile ?? ServiceConfig.MsgBusCredFile;
            var actualRegistry = setting.NatsSerializerRegistry;
            
            // Create connection key for pooling using connection name for named connections
            var connectionKey = $"{setting.Name}|{actualUrl}|{actualCredFile}|{actualRegistry.GetType().Name}";
            
            if (_connections.TryGetValue(connectionKey, out var existingConnection))
            {
                _logger.LogDebug("Reusing existing NATS connection for '{ConnectionName}'", setting.Name);
                return existingConnection;
            }

            var natOpts = NatsOpts.Default with
            {
                Name = setting.Name,
                Url = actualUrl,
                AuthOpts = new NatsAuthOpts
                {
                    CredsFile = actualCredFile
                },
                SerializerRegistry = actualRegistry
            };

            var natsConnection = await NatsConnClient.CreateClientConnectionAsync(
                natOpts,
                _logger,
                cancellationToken: cancellationToken);
            
            _connections[connectionKey] = natsConnection;
            _logger.LogInformation("NATS connection established and cached for '{ConnectionName}'. {natOpts}", setting.Name, natOpts);
            return natsConnection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish NATS connection for '{ConnectionName}'.", setting.Name);
            throw;
        }
    }
}

