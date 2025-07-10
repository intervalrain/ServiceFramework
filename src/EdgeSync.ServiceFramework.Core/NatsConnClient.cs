using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.Core;

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
    /// Asynchronously creates a NATS client connection with retry logic (legacy version without logger).
    /// </summary>
    /// <param name="options">NATS connection options.</param>
    /// <param name="reTryCount">Number of retry attempts (default from config).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connected NatsConnection instance.</returns>
    /// <exception cref="Exception">Thrown when connection fails after all retries.</exception>
    public static async Task<NatsConnection> CreateClientConnectionAsync(NatsOpts options, int reTryCount = ServiceConfig.NatsReTryCount, CancellationToken cancellationToken = default)
    {
        return await CreateClientConnectionAsync(options, logger: null, reTryCount, cancellationToken);
    }

    /// <summary>
    /// Asynchronously creates a NATS client connection with retry logic and logger.
    /// </summary>
    /// <param name="options">NATS connection options.</param>
    /// <param name="logger">Logger instance for retry logging (can be null, will fallback to Console).</param>
    /// <param name="reTryCount">Number of retry attempts (default from config).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connected NatsConnection instance.</returns>
    /// <exception cref="Exception">Thrown when connection fails after all retries.</exception>
    public static async Task<NatsConnection> CreateClientConnectionAsync(NatsOpts options, ILogger? logger, int reTryCount = ServiceConfig.NatsReTryCount, CancellationToken cancellationToken = default)
    {
        if (options.Url.Length == 0)
        {
            throw new ArgumentException("NATS URL is empty.");
        }
        logger ??= new NullLogger<NatsConnClient>();

        var opts = ClientOpts(options);

        for (var i = 0; i < reTryCount; i++)
        {
            try
            {
                if (i > 0)
                {
                    logger.LogWarning($"NATS connection retry {i}/{reTryCount} to '{opts.Url}'");
                }
                var nats = new NatsConnection(opts);

                await nats.ConnectAsync();
                await nats.PingAsync(cancellationToken);

                if (i > 0)
                {
                    logger.LogInformation($"NATS connection successfully established to '{opts.Url}' on retry {i}/{reTryCount}");
                }

                return nats;
            }
            catch (Exception ex)
            {
                if (i < reTryCount - 1)
                {
                    var delayMs = ServiceConfig.NatsRetryDelay * (i + 1);
                    logger.LogError($"NATS connection to '{opts.Url}' failed on attempt {i + 1}/{reTryCount}. Retrying in {delayMs / 1000}s...");
                    await Task.Delay(delayMs, cancellationToken);
                    continue;
                }

                logger.LogError(ex, $"NATS connection to '{opts.Url}' failed after all {reTryCount} attempts. Error: {ex.Message}");
                throw;
            }
        }
        throw new Exception("Failed to connect to NATS server.");
    }
}