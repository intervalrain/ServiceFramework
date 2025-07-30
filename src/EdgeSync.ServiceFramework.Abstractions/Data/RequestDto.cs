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

    [JsonPropertyName("issuer")]
    public string? Issuer { get; init; } = "Unknown";

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; init; } = [];

    [JsonConstructor]
    [Obsolete("This constructor is for serialization only. Use Create methods instead.")]
    public RequestDto()
    {
        ReqSeqId = Guid.NewGuid();
        Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    protected RequestDto(T data, string? issuer)
    {
        ReqSeqId = Guid.NewGuid();
        Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Data = data;
        Issuer = issuer;
    }

    public static RequestDto<T> Create(T data)
    {
        return new RequestDto<T>(data, null);
    }

    public static RequestDto<T> Create(T data, string? issuer)
    {
        return new RequestDto<T>(data, issuer);
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
    public static RequestDto<T> ToRequestDto<T>(this T data, string? issuer )
    {
        return RequestDto<T>.Create(data, issuer);
    }
}