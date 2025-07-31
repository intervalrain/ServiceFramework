using ErrorOr;

namespace EdgeSync.ServiceFramework.Data;

public static class ResponseDtoExtensions
{
    public static ResponseDto<T> ToResponseDto<T>(this ErrorOr<T> errorOr, Guid reqSeqId, Dictionary<string, string>? metadata = null)
    {
        return errorOr.IsError
            ? ResponseDto<T>.Failure(errorOr.Errors, reqSeqId).EnrichWith(metadata)
            : ResponseDto<T>.Success(errorOr.Value, reqSeqId).EnrichWith(metadata);
    }

    public static ResponseDto<T> ToResponseDto<T>(this T value, Guid reqSeqId, Dictionary<string, string>? metadata = null)
    {
        return ResponseDto<T>.Success(value, reqSeqId).EnrichWith(metadata);
    }

    public static ResponseDto<T> ToFailureResponseDto<T>(this Error error, Guid reqSeqId, Dictionary<string, string>? metadata = null)
    {
        return ResponseDto<T>.Failure(error, reqSeqId).EnrichWith(metadata);
    }

    public static ResponseDto<T> ToFailureResponseDto<T>(this List<Error> errors, Guid reqSeqId, Dictionary<string, string>? metadata = null)
    {
        return ResponseDto<T>.Failure(errors, reqSeqId).EnrichWith(metadata);
    }
}