using NATS.Client.Services;

namespace ShadowAgent.Infrastructure.Nats.Models;

public class ServiceMsgContex<T>
{
    // public Type MsgType { get; set; }
    // public Object ServiceMsg { get; set; }
    public NatsSvcMsg<T> ServiceMsg { get; set; }
    public string Subject => ServiceMsg.Subject;
}