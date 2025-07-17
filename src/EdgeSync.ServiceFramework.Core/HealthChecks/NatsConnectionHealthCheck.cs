using EdgeSync.ServiceFramework.Abstractions.JetStream;


using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.Core.HealthChecks;

public class NatsConnectionHealthCheck : IHealthCheck
{
    private readonly IJetStreamClient _client;
    private readonly ILogger<NatsConnectionHealthCheck> _logger;

    protected NatsConnectionHealthCheck(ILogger<NatsConnectionHealthCheck> logger, IJetStreamClient client)
    {
        _client = client;
        _logger = logger;
    }

    public NatsConnectionHealthCheck(ILogger<NatsConnectionHealthCheck> logger, IJetStreamClientFactory factory, string name)
        : this(logger, factory.CreateClient(name))
    {
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.TryConnectAsync();
            return _client.IsConnected()
                ? HealthCheckResult.Healthy("Bus: OK", data: new Dictionary<string, object> { ["is_connected"] = _client.IsConnected() })
                : HealthCheckResult.Unhealthy("Bus: Connection lost", data: new Dictionary<string, object> { ["is_connected"] = _client.IsConnected() });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NATS connection check failed.");
            return HealthCheckResult.Unhealthy("Bus: Connection check failed", data: new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });
        }
    }
}