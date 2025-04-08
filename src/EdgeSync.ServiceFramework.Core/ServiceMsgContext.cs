using NATS.Client.Services;

namespace EdgeSync.ServiceFramework;

public class ServiceMsgContext<T>
{
    public NatsSvcMsg<T> ServiceMsg { get; set; }
    public string Subject => ServiceMsg.Subject;
}