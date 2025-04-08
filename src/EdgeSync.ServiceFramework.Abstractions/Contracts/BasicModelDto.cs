using System;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace ShadowAgent.Infrastructure.Models;

public interface IBasicModelDto
{
    string GroupID { get; set; }
    string DeviceID { get; set; }
    // string Proto { get; set; }
}

public abstract class BasicModelDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GroupID { get; set; } = null;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeviceID { get; set; } = null;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Proto { get; set; } = null;

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
        return JsonSerializer.Serialize(value);
    }

    public override string ToString()
    {
        return Serialize(this);
    }
}