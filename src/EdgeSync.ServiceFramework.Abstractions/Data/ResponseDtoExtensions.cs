using ErrorOr;

namespace EdgeSync.ServiceFramework.Data;

public static class ResponseDtoExtensions
{
    public static ResponseDto<T> ToResponseDto<T>(this ErrorOr<T> errorOr, Guid reqSeqId, string? userId = null, string? tenantId = null)
    {
        var response = errorOr.IsError
            ? ResponseDto<T>.Failure(errorOr.Errors, reqSeqId)
            : ResponseDto<T>.Success(errorOr.Value, reqSeqId);

        return response.WithAuditInfo(userId, tenantId);
    }

    public static ResponseDto<T> ToResponseDto<T>(this T value, Guid reqSeqId, string? userId = null, string? tenantId = null)
    {
        return ResponseDto<T>.Success(value, reqSeqId).WithAuditInfo(userId, tenantId);
    }

    public static ResponseDto<T> ToFailureResponseDto<T>(this Error error, Guid reqSeqId, string? userId = null, string? tenantId = null)
    {
        return ResponseDto<T>.Failure(error, reqSeqId).WithAuditInfo(userId, tenantId);
    }

    public static ResponseDto<T> ToFailureResponseDto<T>(this List<Error> errors, Guid reqSeqId, string? userId = null, string? tenantId = null)
    {
        return ResponseDto<T>.Failure(errors, reqSeqId).WithAuditInfo(userId, tenantId);
    }
}