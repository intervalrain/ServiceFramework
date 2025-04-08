namespace EdgeSync.ServiceFramework;

public class ServiceConfig
{
    public static string MsgBrokerUrl
    {
        get => Environment.GetEnvironmentVariable("MSG_BROKER_URL")
                                        ?? "nats://172.17.20.184:4222";
    }
    public static string MsgBusUrl
    {
        get => Environment.GetEnvironmentVariable("MSG_BUS_URL")
                                        ?? "nats://172.17.20.184:4222";
    }
    public static string MsgBrokerCredFile { get => Environment.GetEnvironmentVariable("MSG_BROKER_CRED") ?? "/workspace/ShadowAgent/etc/nats/shadowagent_dev.creds"; }
    public static string MsgBusCredFile { get => Environment.GetEnvironmentVariable("MSG_BUS_CRED") ?? "/workspace/ShadowAgent/etc/nats/shadowagent_dev.creds"; }

    public const int NatsReTryCount = 10;
    public const int NatsRetryDelay = 1000;
    public const int NatsPingInterval = 1000;
    public const int NatsPingTimeout = 1000;
    public const int NatsMaxPingsOut = 3;
    public const int NatsMaxReconnects = 10;
    public const int NatsReconnectWait = 1000;
    public const int NatsTimeout = 10000; // 10 seconds
    public const int NatsMaxPayload = 1048576;
    public const int NatsMaxPending = 65536;
    public const int NatsMaxPendingBytes = 1048576;
    public const int NatsMaxSubscriptions = 65536;
    public const int NatsJetStreamConsumerFetch = 100;

    public const string DeviceSensorKVStore = "dtp_sensorResourceID";
}