using System;
using System.ComponentModel.DataAnnotations;

using ShadowAgent.Infrastructure.Models;

namespace ShadowAgent.Infrastructure.Base;

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

    public ServiceHandlerException(int errorCode, string message, string command = "unknown", [Required]string? groupId = "unknown", [Required]string deviceId = "unknown") : this(errorCode, message, command)
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

public class ServiceInvalidDataException : ServiceHandlerException
{
    public ServiceInvalidDataException(string message, string command = "unknown", [Required]string groupId = "unknown", [Required]string deviceId = "unknown") : base((int)ServiceResultCode.ServiceResultInvalidSubject, message, command, groupId, deviceId)
    {
    }
}


/// <summary>
/// Defines result codes for service operations and error responses.
/// </summary>
public enum ServiceResultCode
{
    // General success code
    Success = 0,

    // Generic HTTP-like status codes (1xx-5xx range)
    Ok = 200,
    Created = 201,
    Accepted = 202,
    NoContent = 204,
    BadRequest = 400,
    Unauthorized = 401,
    Forbidden = 403,
    NotFound = 404,
    Conflict = 409,
    TooManyRequests = 429,
    InternalServerError = 500,
    ServiceUnavailable = 503,
    
    // // Digital Twin Definition Language (DTDL) model result codes (3000-3999)
    // DTDLModelResultCodeSuccess = 3000,
    // DTDLModelResultCodeBadRequest = 3400,
    // DTDLModelResultCodeUnauthorized = 3401,
    // DTDLModelResultCodeForbidden = 3403,
    // DTDLModelResultCodeNotFound = 3404,
    // DTDLModelResultCodeConflict = 3409,
    // DTDLModelResultCodeInvalidFormat = 3422,
    // DTDLModelResultCodeTooManyRequests = 3429,
    // DTDLModelResultCodeInternalError = 3500,
    // DTDLModelResultCodeUnavailable = 3503,
    
    // // Shadow service specific codes (4000-4999)
    // ShadowNotFound = 4001,
    // ShadowVersionConflict = 4002,
    // ShadowSizeLimitExceeded = 4003,
    // ShadowInvalidState = 4004,
    // ShadowUpdateFailed = 4005,
    // ShadowAccessDenied = 4006,
    
    // // Device specific codes (5000-5999)
    // DeviceNotFound = 5001,
    // DeviceOffline = 5002,
    // DeviceInvalidCommand = 5003,
    // DeviceTimeout = 5004,
    // DeviceBusy = 5005,
    
    // Service framework specific codes (60000-60999)
    ServiceResultInvalidSubject = 60001,
    ServiceResultConnectionFailed = 60002,
    ServiceResultTimeout = 60003,
    ServiceResultInvalidOperation = 60004,
    ServiceResultInvalidConfiguration = 60005,
    ServiceResultDuplicateEndpoint = 60006,
    ServiceResultHandlerException = 60007,
    ServiceResultMissingDependency = 60008,
    ServiceResultSerializationError = 60009,
    ServiceResultDeserializationError = 60010,
    ServiceResultMessageValidationError = 60011,
    ServiceResultEndpointNotFound = 60012,
    ServiceResultUnknownError = 60099,
    
    // NATS specific codes (61000-61999)
    NatsResultConnectionFailed = 61001,
    NatsResultJetStreamNotAvailable = 61002,
    NatsResultPublishFailed = 61003,
    NatsResultSubscriptionFailed = 61004,
    NatsResultNoResponse = 61005,
    NatsResultInvalidSubject = 61006,
    NatsResultAckTimeout = 61007,
    NatsResultMaxRetriesExceeded = 61008,
    NatsResultStreamNotFound = 61009,
    NatsResultConsumerNotFound = 61010,
    NatsResultMessageTooLarge = 61011,
    NatsResultUnknownError = 61099,
    
    // Feature specific codes (70000+)
    // Can be expanded as needed for specific features
}