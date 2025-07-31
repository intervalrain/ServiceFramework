using System.Text.Json.Serialization;

namespace AuthorSystem.Client.Models;

public class NumberInput
{
    [JsonPropertyName("number")]
    public int Number { get; set; }
};