using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EmailSystem.Application.Contracts.Dtos;

/// <summary>
/// DTO for processing email events - converts subject and data to email format
/// </summary>
public class EmailEventDto
{
    /// <summary>
    /// Email subject line
    /// </summary>
    [Required]
    [MaxLength(200)]
    [DefaultValue("System Notification")]
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;
    
    /// <summary>
    /// Data to be converted to email content. Should include 'to' and 'from' fields for email addresses.
    /// All other fields will be formatted into the email body.
    /// </summary>
    [Required]
    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>
    {
        ["to"] = "rain.hu@advantech.com",
        ["from"] = "system@advantech.com",
        ["userName"] = "Rain Hu",
        ["eventType"] = "SystemAlert",
        ["message"] = "Your system monitoring alert has been triggered.",
        ["timestamp"] = "2025-01-08T10:30:00Z"
    };
}