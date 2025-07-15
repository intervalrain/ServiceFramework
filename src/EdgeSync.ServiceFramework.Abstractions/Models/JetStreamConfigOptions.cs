using NATS.Client.JetStream.Models;

namespace EdgeSync.ServiceFramework.Abstractions.Models;

public record JetStreamConfigOptions : StreamConfig
{
    public static JetStreamConfigOptions Default => new JetStreamConfigOptions();
    public JetStreamConfigOptions()
    {
        Description = "EdgeSyncAPI Service Stream";
        Retention = StreamConfigRetention.Limits;
        MaxAge = TimeSpan.FromDays(7);
        Storage = StreamConfigStorage.File;
        NumReplicas = 1;
    }
}
