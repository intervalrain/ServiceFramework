using EdgeSync.ServiceFramework.Abstractions.JetStream;


using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.Core.HealthChecks;

public class NatsBusConnectionHealthCheck : NatsConnectionHealthCheck
{
    public NatsBusConnectionHealthCheck(ILogger<NatsBusConnectionHealthCheck> logger, IJetStreamClientFactory factory)
        : base(logger, factory.CreateMsgBusClient())
    {
    }
}