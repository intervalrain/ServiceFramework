using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using EmailSystem.Domain.Entities;

namespace EmailSystem.Application.Dtos;

/// <summary>
/// DTO representing an email with its status and metadata
/// </summary>
public class EmailDto
{
    /// <summary>
    /// Unique identifier for the email
    /// </summary>
    [Required]
    public Guid Id { get; set; }
    
    /// <summary>
    /// Email subject line
    /// </summary>
    [Required]
    [MaxLength(200)]
    [DefaultValue("Welcome to EmailSystem")]
    public string Subject { get; set; } = string.Empty;
    
    /// <summary>
    /// Recipient email address
    /// </summary>
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    [DefaultValue("rain.hu@advantech.com")]
    public string To { get; set; } = string.Empty;
    
    /// <summary>
    /// Sender email address
    /// </summary>
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    [DefaultValue("noreply@advantech.com")]
    public string From { get; set; } = string.Empty;
    
    /// <summary>
    /// HTML content of the email
    /// </summary>
    [MaxLength(65535)]
    [DefaultValue("<html><body><h1>Welcome!</h1><p>This is a test email.</p></body></html>")]
    public string HtmlContent { get; set; } = string.Empty;
    
    /// <summary>
    /// Plain text content of the email
    /// </summary>
    [MaxLength(65535)]
    [DefaultValue("Welcome!\n\nThis is a test email.")]
    public string TextContent { get; set; } = string.Empty;
    
    /// <summary>
    /// When the email was created
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When the email was sent (null if not sent yet)
    /// </summary>
    public DateTime? SentAt { get; set; }
    
    /// <summary>
    /// Current status of the email
    /// </summary>
    [Required]
    [DefaultValue(EmailStatus.Pending)]
    public EmailStatus Status { get; set; }
    
    /// <summary>
    /// Additional data associated with the email
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();
}