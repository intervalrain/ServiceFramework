using NATS.Client.Core;

namespace EdgeSync.ServiceFramework;

public class NatsConnClient
{
    /// <summary>
    /// Configures NATS client options based on service configuration.
    /// </summary>
    public static NatsOpts ClientOpts(NatsOpts opts)
    {
        return opts with
        {
            LoggerFactory = opts.LoggerFactory,
            TlsOpts = new NatsTlsOpts { Mode = TlsMode.Auto, InsecureSkipVerify = true },
            ConnectTimeout = TimeSpan.FromMilliseconds(ServiceConfig.NatsTimeout),
            RequestTimeout = TimeSpan.FromMilliseconds(ServiceConfig.NatsTimeout)
        };
    }

    /// <summary>
    /// Asynchronously creates a NATS client connection with retry logic.
    /// </summary>
    /// <param name="options">NATS connection options.</param>
    /// <param name="retryCount">Number of retry attempts (default from config).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connected NatsConnection instance.</returns>
    /// <exception cref="Exception">Thrown when connection fails after all retries.</exception>
    public static async Task<NatsConnection> CreateClientConnectionAsync(NatsOpts options, int reTryCount = ServiceConfig.NatsReTryCount, CancellationToken cancellationToken = default)
    {
        if (options.Url.Length == 0)
        {
            throw new ArgumentException("NATS URL is empty.");
        }

        var opts = ClientOpts(options);
        for (var i = 0; i < reTryCount; i++)
        {
            try
            {
                var nats = new NatsConnection(opts);
                await nats.ConnectAsync();
                await nats.PingAsync(cancellationToken);
                return nats;
            }
            catch (Exception)
            {
                if (i < reTryCount - 1)
                {
                    // decayed retry
                    await Task.Delay(ServiceConfig.NatsRetryDelay * (i + 1), cancellationToken);
                    continue;
                }
                throw;
            }
        }
        throw new Exception("Failed to connect to NATS server.");
    }
}
