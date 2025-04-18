using Microsoft.Extensions.Options;

namespace EdgeSync.ServiceFramework;

public class ServiceConfig
{
    private static NatsApiOptions _options = new NatsApiOptions();

    public static void Initialize(NatsApiOptions? options)
    {
        if (options != null)
        {
            _options = options;
        }
    }

    public static string MsgBrokerUrl => string.IsNullOrEmpty(_options.MsgBrokerUrl)
                                        ? Environment.GetEnvironmentVariable("MSG_BROKER_URL") ?? ""
                                        : _options.MsgBrokerUrl;
    
    public static string MsgBusUrl => string.IsNullOrEmpty(_options.MsgBusUrl)
                                        ? Environment.GetEnvironmentVariable("MSG_BUS_URL") ?? ""
                                        : _options.MsgBusUrl;
    
    public static string MsgBrokerCredFile => string.IsNullOrEmpty(_options.MsgBrokerCredFile)
                                        ? Environment.GetEnvironmentVariable("MSG_BROKER_CRED") ?? ""
                                        : _options.MsgBrokerCredFile;
    
    public static string MsgBusCredFile => string.IsNullOrEmpty(_options.MsgBusCredFile)
                                        ? Environment.GetEnvironmentVariable("MSG_BUS_CRED") ?? ""
                                        : _options.MsgBusCredFile;

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
    public const string DeviceDtdlKVStore = "dtp_deviceResourceID";
}