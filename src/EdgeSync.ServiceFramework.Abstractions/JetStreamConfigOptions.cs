using NATS.Client.JetStream.Models;


namespace EdgeSync.ServiceFramework
{

    public record JetStreamConfigOptions : StreamConfig
    {
        public JetStreamConfigOptions()
        {
            Description = "EdgeSyncAPI Service Stream";
            Retention = StreamConfigRetention.Limits;
            MaxAge = TimeSpan.FromDays(7);
            Storage = StreamConfigStorage.File;
            NumReplicas = 1;
        }
    }

    public record ConsumerConfigOptions : ConsumerConfig
    {
        public ConsumerConfigOptions()
        {
            AckPolicy = ConsumerConfigAckPolicy.Explicit;
            // ReplayPolicy = ConsumerConfigReplayPolicy.Instant;
            MaxAckPending = -1;
        }
    }
}
