using NATS.Client.Core;

namespace EdgeSync.ServiceFramework;

public interface INatsConnectionFactory
{
    Task<INatsConnection> CreateConnectionAsync(string url ="", string credFile ="",
                                        CancellationToken cancellationToken = default);
}