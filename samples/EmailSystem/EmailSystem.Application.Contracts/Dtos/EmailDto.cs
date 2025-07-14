using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using EmailSystem.Domain.Entities;

namespace EmailSystem.Application.Contracts.Dtos;

/// <summary>
/// DTO representing an email with its status and metadata
/// </summary>
public class EmailDto
{
    /// <summary>
    /// Unique identifier for the email
    /// </summary>
    [Required]
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    
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
    [DefaultValue("<html><body><h1>Welcome!</h1><p>This is a test email.</p></body></html>")]
    [JsonPropertyName("htmlContent")]
    public string HtmlContent { get; set; } = string.Empty;
    
    /// <summary>
    /// Plain text content of the email
    /// </summary>
    [MaxLength(65535)]
    [DefaultValue("Welcome!\n\nThis is a test email.")]
    [JsonPropertyName("textContent")]
    public string TextContent { get; set; } = string.Empty;
    
    /// <summary>
    /// When the email was created
    /// </summary>
    [Required]
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When the email was sent (null if not sent yet)
    /// </summary>
    [JsonPropertyName("sentAt")]
    public DateTime? SentAt { get; set; }
    
    /// <summary>
    /// Current status of the email
    /// </summary>
    [Required]
    [DefaultValue(EmailStatus.Pending)]
    [JsonPropertyName("status")]
    public EmailStatus Status { get; set; }
    
    /// <summary>
    /// Additional data associated with the email
    /// </summary>
    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new();
}