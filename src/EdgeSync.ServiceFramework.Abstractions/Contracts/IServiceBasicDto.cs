namespace EdgeSync.ServiceFramework.Contracts;

public interface IServiceBasicDto
{
    string ProtoVer { get; set; }
    string GroupId { get; set; }
    string DeviceId { get; set; }
    ulong SeqId { get; set; }
    string ReqSeqId { get; set; }
}