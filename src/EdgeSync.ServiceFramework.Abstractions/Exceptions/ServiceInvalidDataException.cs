using System.ComponentModel.DataAnnotations;

using EdgeSync.ServiceFramework.Enums;

namespace EdgeSync.ServiceFramework.Exceptions;

public class ServiceInvalidDataException : ServiceHandlerException
{
    public ServiceInvalidDataException(
        string message,
        string command = "unknown",
        [Required] string groupId = "unknown",
        [Required] string deviceId = "unknown") : base((int)ServiceResultCode.ServiceResultInvalidSubject, message, command, groupId, deviceId)
    {
    }
}