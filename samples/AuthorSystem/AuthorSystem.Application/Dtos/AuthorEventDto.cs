namespace AuthorSystem.Application.Dtos;

/// <summary>
/// DTO for author-related events
/// </summary>
public class AuthorEventDto
{
    public string EventType { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime EventTime { get; set; } = DateTime.UtcNow;
    public string? AdditionalData { get; set; }
}

/// <summary>
/// DTO for author notification events
/// </summary>
public class AuthorNotificationDto
{
    public Guid AuthorId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO for batch author operations
/// </summary>
public class BatchAuthorOperationDto
{
    public List<Guid> AuthorIds { get; set; } = new();
    public string OperationType { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// DTO for author statistics updates
/// </summary>
public class AuthorStatsUpdateDto
{
    public Guid AuthorId { get; set; }
    public int ViewCount { get; set; }
    public int VoteCount { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}