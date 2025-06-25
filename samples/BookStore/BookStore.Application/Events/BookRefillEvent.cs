namespace BookStore.Application.Events;

public class BookRefillEvent
{
    public Guid BookId { get; set; }
    public int RefillQuantity { get; set; }
    public string? Reason { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = string.Empty;
}