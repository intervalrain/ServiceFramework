using NATS.Client.JetStream.Models;

namespace EdgeSync.ServiceFramework.Abstractions.Models;

public record ConsumerConfigOptions : ConsumerConfig
{
    public static ConsumerConfigOptions Default => new ConsumerConfigOptions();
    public ConsumerConfigOptions()
    {
        AckPolicy = ConsumerConfigAckPolicy.Explicit;
        // ReplayPolicy = ConsumerConfigReplayPolicy.Instant;
        MaxAckPending = -1;
    }
}