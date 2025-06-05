using System.Text.Json;

namespace EdgeSync.ServiceFramework.Contracts;

// Define the error code and message for the response model
public enum ResultCode
{
    DTDLModelResultCodeOK = 60000,
    DTDLModelResultCodeInvalidFormat = 60001,
    DTDLModelResultCodeInvalidSchema = 60002,
    DTDLModelResultCodeInvalidSchemaType = 60003,
    DTDLModelResultCodeInvalidSchemaProperty = 60004,
    DTDLModelResultCodeInvalidSchemaPropertyType = 60005,
    DTDLModelResultCodeInternalError = 50001,
}

// Define the DTOs for the response model

public class ResultModelDto
{
    public required int Code { get; set; }
    public required string Message { get; set; }
}

public class DataModelDto
{
    public string? GroupID { get; set; }
    public string? DeviceUID { get; set; }
    public string? Target { get; set; }
    public required ResultModelDto Result { get; set; }
}

public class ResponseModelDto
{
    public required string Cmd { get; set; }
    public  ulong SeqId { get; set; }
    public string? ReqSeqId { get; set; }
    public string? RspSeqId { get; set; }
    public long Timestamp { get; set; }
    public required DataModelDto data { get; set; }

    public void SetResponseResult( ResultCode code, string message)
    {
        data.Result.Code = (int)code;
        data.Result.Message = message;
    }

        public static TValue? Deserialize<TValue>(string json)
    {
        if (json is null)
        {
            throw new ArgumentNullException(nameof(json));
        }

        return JsonSerializer.Deserialize<TValue>(json);
    }

    public static string Serialize<TValue>(TValue value)
    {
        return JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public override string ToString()
    {
        return Serialize(this);
    }
}

