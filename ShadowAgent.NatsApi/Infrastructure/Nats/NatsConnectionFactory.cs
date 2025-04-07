using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NATS.Client.Core;

using ShadowAgent.Infrastructure.Interfaces;
using ShadowAgent.Configuration;

namespace ShadowAgent.Infrastructure.Nats;

public class NatsConnectionFactory : INatsConnectionFactory
{
    private readonly ILogger<NatsConnectionFactory> _logger;

    public NatsConnectionFactory(ILogger<NatsConnectionFactory> logger)
    {
        _logger = logger;
    }

    public async Task<INatsConnection> CreateConnectionAsync(string url ="", string credFile ="",
                                                    CancellationToken cancellationToken = default)
    {
        try
        {
            var natOpts = NatsOpts.Default with
            {
                Url = (url == "") ? ServiceConfig.MsgBusUrl : url,
                AuthOpts = new NatsAuthOpts
                {
                    CredsFile = (credFile == "") ? ServiceConfig.MsgBusCredFile : credFile
                }
            };
            var natsConnection = await NatsConnClient.CreateClientConnectionAsync(
                natOpts,
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