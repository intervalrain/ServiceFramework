using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EmailSystem.Application.Contracts.Dtos;

/// <summary>
/// DTO for sending email with complete content
/// </summary>
public class SendEmailDto
{
    /// <summary>
    /// Email subject line
    /// </summary>
    [Required]
    [MaxLength(200)]
    [DefaultValue("Welcome to EmailSystem")]
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;
    
    /// <summary>
    /// Recipient email address
    /// </summary>
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    [DefaultValue("rain.hu@advantech.com")]
    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;
    
    /// <summary>
    /// Sender email address
    /// </summary>
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    [DefaultValue("noreply@advantech.com")]
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;
    
    /// <summary>
    /// HTML content of the email
    /// </summary>
    [MaxLength(65535)]
    [DefaultValue("<html><body><h1>Welcome!</h1><p>This is a test email from EmailSystem.</p></body></html>")]
    [JsonPropertyName("htmlContent")]
    public string HtmlContent { get; set; } = string.Empty;
    
    /// <summary>
    /// Plain text content of the email
    /// </summary>
    [MaxLength(65535)]
    [DefaultValue("Welcome!\n\nThis is a test email from EmailSystem.")]
    [JsonPropertyName("textContent")]
    public string TextContent { get; set; } = string.Empty;
    
    /// <summary>
    /// Additional data associated with the email
    /// </summary>
    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new();
}