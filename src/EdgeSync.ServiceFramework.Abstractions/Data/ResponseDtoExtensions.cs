using ErrorOr;

namespace EdgeSync.ServiceFramework.Data;

public static class ResponseDtoExtensions
{
    public static ResponseDto<T> ToResponseDto<T>(this ErrorOr<T> errorOr, Guid reqSeqId, string? issuer = null, Dictionary<string, string>? metadata = null)
    {
        return errorOr.IsError
            ? ResponseDto<T>.Failure(errorOr.Errors, reqSeqId).EnrichWith(issuer, metadata)
            : ResponseDto<T>.Success(errorOr.Value, reqSeqId).EnrichWith(issuer, metadata);
    }

    public static ResponseDto<T> ToResponseDto<T>(this T value, Guid reqSeqId, string? issuer = null, Dictionary<string, string>? metadata = null)
    {
        return ResponseDto<T>.Success(value, reqSeqId).EnrichWith(issuer, metadata);
    }

    public static ResponseDto<T> ToFailureResponseDto<T>(this Error error, Guid reqSeqId, string? issuer = null, Dictionary<string, string>? metadata = null)
    {
        return ResponseDto<T>.Failure(error, reqSeqId).EnrichWith(issuer, metadata);
    }

    public static ResponseDto<T> ToFailureResponseDto<T>(this List<Error> errors, Guid reqSeqId, string? issuer = null, Dictionary<string, string>? metadata = null)
    {
        return ResponseDto<T>.Failure(errors, reqSeqId).EnrichWith(issuer, metadata);
    }
}