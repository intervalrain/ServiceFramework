using System.Text.Json;

namespace EdgeSync.ServiceFramework.Contracts;

public class ServiceResponseModelDto
{
    public required string Cmd { get; set; }
    public ulong SeqId { get; set; }
    public string? ReqSeqId { get; set; }
    public string? RspSeqId { get; set; }
    public long Timestamp { get; set; }
    public required ServiceResponseDataModelDto data { get; set; }

    public void SetResponseResult(int code, string message)
    {
        data.Result.Code = code;
        data.Result.Message = message;
    }

    public static TValue? Deserialize<TValue>(string json)
    {
        return json is null ? throw new ArgumentNullException(nameof(json)) : JsonSerializer.Deserialize<TValue>(json);
    }

    public static string Serialize<TValue>(TValue value)
    {
        return JsonSerializer.Serialize(value);
    }

    public override string ToString()
    {
        return Serialize(this);
    }
}