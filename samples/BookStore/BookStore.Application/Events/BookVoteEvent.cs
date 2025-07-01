namespace BookStore.Application.Events;

public class BookVoteEvent
{
    public Guid BookId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}