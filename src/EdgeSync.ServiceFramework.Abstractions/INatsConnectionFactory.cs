using NATS.Client.Core;

namespace EdgeSync.ServiceFramework;

public interface INatsConnectionFactory
{
    Task<INatsConnection> CreateConnectionAsync(string url = "", string credFile = "", INatsSerializerRegistry? serializerRegistry = null,
                                        CancellationToken cancellationToken = default);

    Task<INatsConnection> CreateConnectionAsync(NatsConnectionSettings setting, CancellationToken cancellationToken = default);
}