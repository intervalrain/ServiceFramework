using System.ComponentModel.DataAnnotations;

using EdgeSync.ServiceFramework.Contracts;

namespace EdgeSync.ServiceFramework.Exceptions;

/// <summary>
/// Exception thrown by service handlers to format errors consistently for clients.
/// </summary>
public class ServiceHandlerException : Exception
{
    /// <summary>
    /// Gets the error code to return to the client.
    /// </summary>
    public int ErrorCode { get; }

    /// <summary>
    /// Gets the command associated with the error.
    /// </summary>
    public string Command { get; }

    /// <summary>
    /// Gets the group ID associated with the error, if any.
    /// </summary>
    public string GroupId { get; }

    /// <summary>
    /// Gets the device ID associated with the error, if any.
    /// </summary>
    public string DeviceId { get; }

    /// <summary>
    /// Gets the target associated with the error.
    /// </summary>
    public string Target { get; }

    /// <summary>
    /// Gets or sets the sequence ID for the request.
    /// </summary>
    public ulong SeqId { get; set; }

    /// <summary>
    /// Gets or sets the request sequence ID.
    /// </summary>
    public string ReqSeqId { get; set; }

    /// <summary>
    /// Initializes a new instance of the ServiceHandlerException class.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="command">The command associated with the error.</param>
    public ServiceHandlerException(int errorCode, string message, string command = "unknown")
        : base(message)
    {
        ErrorCode = errorCode;
        Command = command;
        Target = "dm";
        GroupId = string.Empty;
        DeviceId = string.Empty;
        ReqSeqId = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the ServiceHandlerException class.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="command">The command associated with the error.</param>
    /// <param name="groupId">The group ID associated with the error.</param>
    /// <param name="deviceId">The device ID associated with the error.</param>
    /// <param name="target">The target associated with the error.</param>
    public ServiceHandlerException(int errorCode, string message, string command,
                            string groupId, string deviceId, string target = "dm")
        : base(message)
    {
        ErrorCode = errorCode;
        Command = command;
        GroupId = groupId;
        DeviceId = deviceId;
        Target = target;
        ReqSeqId = string.Empty;
    }

    public ServiceHandlerException(int errorCode, string message, string command = "unknown", [Required] string? groupId = "unknown", [Required] string? deviceId = "unknown") : this(errorCode, message, command)
    {
    }

    /// <summary>
    /// Creates a ResponseModelDto from this exception.
    /// </summary>
    /// <returns>A ResponseModelDto containing error details.</returns>
    public ServiceResponseModelDto ToResponseModel()
    {
        // TODO: ResponseModelDto is the application's response model, which is NOT defined in this infra.
        // We should define it in infra service framework, or using a custom response model.
        return new ServiceResponseModelDto
        {
            Cmd = Command,
            SeqId = SeqId,
            ReqSeqId = ReqSeqId,
            RspSeqId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            data = new ServiceResponseDataModelDto
            {
                GroupID = GroupId,
                DeviceUID = DeviceId,
                Target = Target,
                Result = new ServiceResultModelDto
                {
                    Code = ErrorCode,
                    Message = Message
                }
            }
        };
    }
}