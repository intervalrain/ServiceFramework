using System.Text.Json.Serialization;

namespace AuthorSystem.Application.Dtos;

/// <summary>
/// DTO for author-related events
/// </summary>
public class AuthorEventDto
{
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;
    
    [JsonPropertyName("authorId")]
    public Guid AuthorId { get; set; }
    
    [JsonPropertyName("authorName")]
    public string AuthorName { get; set; } = string.Empty;
    
    [JsonPropertyName("eventTime")]
    public DateTime EventTime { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("additionalData")]
    public string? AdditionalData { get; set; }
}

/// <summary>
/// DTO for author notification events
/// </summary>
public class AuthorNotificationDto
{
    [JsonPropertyName("authorId")]
    public Guid AuthorId { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("notificationType")]
    public string NotificationType { get; set; } = string.Empty;
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO for batch author operations
/// </summary>
public class BatchAuthorOperationDto
{
    [JsonPropertyName("authorIds")]
    public List<Guid> AuthorIds { get; set; } = new();
    
    [JsonPropertyName("operationType")]
    public string OperationType { get; set; } = string.Empty;
    
    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// DTO for author statistics updates
/// </summary>
public class AuthorStatsUpdateDto
{
    [JsonPropertyName("authorId")]
    public Guid AuthorId { get; set; }
    
    [JsonPropertyName("viewCount")]
    public int ViewCount { get; set; }
    
    [JsonPropertyName("voteCount")]
    public int VoteCount { get; set; }
    
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}