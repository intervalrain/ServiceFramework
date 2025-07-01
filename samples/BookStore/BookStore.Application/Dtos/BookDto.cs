namespace BookStore.Application.Dtos;

public class BookDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int Vote { get; set; }
    public DateTime CreatedAt { get; set; }
}