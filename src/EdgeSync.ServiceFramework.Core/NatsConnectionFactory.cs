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