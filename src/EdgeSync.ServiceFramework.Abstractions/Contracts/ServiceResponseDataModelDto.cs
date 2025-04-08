using EdgeSync.ServiceFramework.Enums;

namespace EdgeSync.ServiceFramework.Contracts;

public class ServiceResponseDataModelDto
{
    public string? GroupID { get; set; }
    public string? DeviceUID { get; set; }
    public string? Target { get; set; }
    public required ServiceResultModelDto Result { get; set; }

    public static ServiceResponseDataModelDto Create(string groupId, string deviceUid, string target, ServiceResultCode code, string? message = null)
    {
        return new ServiceResponseDataModelDto
        {
            GroupID = groupId,
            DeviceUID = deviceUid,
            Target = target,
            Result = new ServiceResultModelDto
            {
                Code = (int)code,
                Message = message ?? GetCodeMessage(code)
            }
        };
    }

    private static string GetCodeMessage(ServiceResultCode code)
    {
        return code switch
        {
            _ => code.ToString(),
        };
    }
}