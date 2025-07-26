using ErrorOr;

namespace EdgeSync.ServiceFramework.Data;

public record ResponseDto<T>
{
    public ulong Timestamp { get; init; }
    public Guid ReqSeqId { get; init; }
    public Guid RspSeqId { get; init; }
    public T? Data { get; init; }
    public List<Error> Errors { get; init; } = [];
    public string Message { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public string? TenantId { get; init; }
    public string? CorrelationId { get; init; }

    public bool IsError => Errors.Any();
    public bool IsSuccess => !IsError;
    public Error? FirstError => Errors.FirstOrDefault();

    protected ResponseDto(T? data, Guid reqSeqId)
    {
        Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ReqSeqId = reqSeqId;
        RspSeqId = Guid.NewGuid();
        Data = data;
    }

    protected ResponseDto(List<Error> errors, Guid reqSeqId, string? message = null)
    {
        Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ReqSeqId = reqSeqId;
        RspSeqId = Guid.NewGuid();
        Errors = errors;
        Message = message ?? errors.FirstOrDefault().Description ?? "An error occurred";
    }

    public static ResponseDto<T> Success(T data, Guid reqSeqId, string? message = null)
    {
        return new ResponseDto<T>(data, reqSeqId)
        {
            Message = message ?? "Success"
        };
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

    public ResponseDto<T> WithAuditInfo(string? userId, string? tenantId, string? correlationId = null)
    {
        return this with
        {
            UserId = userId,
            TenantId = tenantId,
            CorrelationId = correlationId
        };
    }

    public ResponseDto<T> WithCorrelationId(string correlationId)
    {
        return this with { CorrelationId = correlationId };
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