namespace EdgeSync.ServiceFramework.Enums;

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