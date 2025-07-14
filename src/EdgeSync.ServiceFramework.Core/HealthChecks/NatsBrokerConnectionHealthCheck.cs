using EdgeSync.ServiceFramework.JetStream;
using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.Core.HealthChecks;

public class NatsBrokerConnectionHealthCheck : NatsConnectionHealthCheck
{
    public NatsBrokerConnectionHealthCheck(ILogger<NatsBrokerConnectionHealthCheck> logger, IJetStreamClientFactory factory)
        : base(logger, factory.CreateMsgBrokerClient())
    {
    }
}
