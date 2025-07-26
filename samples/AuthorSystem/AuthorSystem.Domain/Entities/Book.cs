namespace AuthorSystem.Domain.Entities;

public class Book
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string Isbn { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PublicationYear { get; set; }
    public double Price { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static Book Create(
        string title,
        string authorId,
        string isbn,
        string description,
        int publicationYear,
        double price)
    {
        return new Book
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            AuthorId = authorId,
            Isbn = isbn,
            Description = description,
            PublicationYear = publicationYear,
            Price = price,
            IsAvailable = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}