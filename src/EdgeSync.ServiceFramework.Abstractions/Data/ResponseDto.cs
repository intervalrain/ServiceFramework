using System.Text.Json.Serialization;

using EdgeSync.ServiceFramework.Data.Json;

using ErrorOr;

namespace EdgeSync.ServiceFramework.Data;

public record ResponseDto<T>
{
    [JsonPropertyName("timestamp")]
    public ulong Timestamp { get; init; }

    [JsonPropertyName("reqSeqId")]
    public Guid ReqSeqId { get; init; }

    [JsonPropertyName("rspSeqId")]
    public Guid RspSeqId { get; init; }

    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("errors")]
    [JsonConverter(typeof(ErrorListJsonConverter))]
    public List<Error> Errors { get; init; } = [];

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("issuer")]
    public string? Issuer { get; init; } = "Unknown";

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; init; }

    [JsonPropertyName("isError")]
    public bool IsError => Errors.Any();

    [JsonPropertyName("isSuccess")]
    public bool IsSuccess => !IsError;

    [JsonPropertyName("firstError")]
    [JsonConverter(typeof(ErrorJsonConverter))]
    public Error? FirstError => Errors.FirstOrDefault();

    protected ResponseDto(T? data, Guid reqSeqId)
    {
        Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ReqSeqId = reqSeqId;
        RspSeqId = Guid.NewGuid();
        Data = data;
    }

    protected ResponseDto(List<Error> errors, string? message = null)
    {
        Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ReqSeqId = Guid.NewGuid();
        RspSeqId = Guid.NewGuid();
        Errors = errors;
        Message = message ?? errors.FirstOrDefault().Description ?? "An error occurred";
    }

    protected ResponseDto(List<Error> errors, Guid reqSeqId, string? message = null)
    {
        Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ReqSeqId = reqSeqId;
        RspSeqId = Guid.NewGuid();
        Errors = errors;
        Message = message ?? errors.FirstOrDefault().Description ?? "An error occurred";
    }

    public static ResponseDto<T> Success(T data, string? message = null)
    {
        return new ResponseDto<T>(data)
        {
            Message = message ?? "Success"
        };
    }

    public static ResponseDto<T> Success(T data, Guid reqSeqId, string? message = null)
    {
        return new ResponseDto<T>(data, reqSeqId)
        {
            Message = message ?? "Success"
        };
    }

    public static ResponseDto<T> Failure(Error error)
    {
        return new ResponseDto<T>([error]);
    }

    public static ResponseDto<T> Failure(Error error, Guid reqSeqId)
    {
        return new ResponseDto<T>([error], reqSeqId);
    }

    public static ResponseDto<T> Failure(List<Error> errors, Guid reqSeqId)
    {
        return new ResponseDto<T>(errors, reqSeqId);
    }

    public static implicit operator ResponseDto<T>(T value)
    {
        return Success(value, Guid.NewGuid());
    }

    public static implicit operator ResponseDto<T>(Error error)
    {
        return Failure(error, Guid.NewGuid());
    }

    public static implicit operator ResponseDto<T>(List<Error> errors)
    {
        return Failure(errors, Guid.NewGuid());
    }

    public static implicit operator ResponseDto<T>(ErrorOr<T> errorOr)
    {
        return errorOr.IsError
            ? Failure(errorOr.Errors, Guid.NewGuid())
            : Success(errorOr.Value, Guid.NewGuid());
    }

    public ResponseDto<T> EnrichWith<TRequest>(RequestDto<TRequest> input)
    {
        return this with
        {
            ReqSeqId = input.ReqSeqId,
            Issuer = input.Issuer,
            Metadata = input.Metadata
        };
    }

        public ResponseDto<T> EnrichWith(string? issuer, Dictionary<string, string>? metadata = null)
    {
        return this with
        {
            Issuer = issuer,
            Metadata = metadata
        };
    }

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<List<Error>, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(Data!) : onFailure(Errors);
    }

    public async Task<TResult> MatchAsync<TResult>(
        Func<T, Task<TResult>> onSuccess,
        Func<List<Error>, Task<TResult>> onFailure)
    {
        return IsSuccess ? await onSuccess(Data!) : await onFailure(Errors);
    }
}