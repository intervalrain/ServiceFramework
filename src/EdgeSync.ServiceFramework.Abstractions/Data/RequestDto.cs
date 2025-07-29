using System.Text.Json.Serialization;

namespace EdgeSync.ServiceFramework.Data;

public record RequestDto<T>
{
    [JsonPropertyName("reqSeqId")]
    public Guid ReqSeqId { get; init; }

    [JsonPropertyName("timestamp")]
    public ulong Timestamp { get; init; }

    [JsonPropertyName("data")]
    public T Data { get; init; }

    [JsonPropertyName("userId")]
    public string? UserId { get; init; }

    [JsonPropertyName("tenantId")]
    public string? TenantId { get; init; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; init; }

    protected RequestDto(T data)
    {
        ReqSeqId = Guid.NewGuid();
        Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Data = data;
    }

    public static RequestDto<T> Create(T data)
    {
        return new RequestDto<T>(data);
    }

    public static RequestDto<T> Create(T data, string? userId, string? tenantId)
    {
        return new RequestDto<T>(data)
        {
            UserId = userId,
            TenantId = tenantId
        };
    }

    public static RequestDto<T> Create(T data, string? userId, string? tenantId, string? correlationId)
    {
        return new RequestDto<T>(data)
        {
            UserId = userId,
            TenantId = tenantId,
            CorrelationId = correlationId
        };
    }

    public RequestDto<T> WithAuditInfo(string? userId, string? tenantId)
    {
        return this with
        {
            UserId = userId,
            TenantId = tenantId
        };
    }

    public RequestDto<T> WithCorrelationId(string correlationId)
    {
        return this with { CorrelationId = correlationId };
    }

    public RequestDto<T> WithMetadata(Dictionary<string, string> metadata)
    {
        return this with { Metadata = metadata };
    }

    public RequestDto<T> AddMetadata(string key, string value)
    {
        var metadata = Metadata ?? new Dictionary<string, string>();
        metadata[key] = value;
        return this with { Metadata = metadata };
    }

    public static implicit operator RequestDto<T>(T value)
    {
        return Create(value);
    }
}

public static class RequestDtoExtensions
{
    public static RequestDto<T> ToRequestDto<T>(this T data, string? userId = null, string? tenantId = null)
    {
        return RequestDto<T>.Create(data, userId, tenantId);
    }

    public static RequestDto<T> ToRequestDto<T>(this T data, string? userId, string? tenantId, string correlationId)
    {
        return RequestDto<T>.Create(data, userId, tenantId).WithCorrelationId(correlationId);
    }
}