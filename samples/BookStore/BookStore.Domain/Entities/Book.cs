using BookStore.Domain.Errors;
using BookStore.Domain.Specifications;

using ErrorOr;

namespace BookStore.Domain.Entities;

public class Book
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int Vote { get; set; }
    public DateTime CreatedAt { get; set; }

    private Book(Guid id, string title, string author, string isbn, decimal price, int stock)
    {
        Id = id;
        Title = title;
        Author = author;
        ISBN = isbn;
        Price = price;
        Stock = stock;
        Vote = 0;
        CreatedAt = DateTime.UtcNow;
    }

    public static ErrorOr<Book> Create(string title, string author, string isbn, decimal price, int stock)
    {
        var errors = new List<Error>();

        if (!BookSpecifications.HasValidTitle(title))
            errors.Add(BookErrors.InvalidTitle);

        if (!BookSpecifications.HasValidAuthor(author))
            errors.Add(BookErrors.InvalidAuthor);

        if (!BookSpecifications.HasValidISBN(isbn))
            errors.Add(BookErrors.InvalidISBN);

        if (!BookSpecifications.HasValidPrice(price))
            errors.Add(BookErrors.InvalidPrice);

        if (!BookSpecifications.HasValidStock(stock))
            errors.Add(BookErrors.InvalidStock);

        return errors.Count > 0 ? errors : new Book(Guid.NewGuid(), title, author, isbn, price, stock);
    }

    public ErrorOr<Book> Update(string title, string author, decimal price, int stock)
    {
        var errors = new List<Error>();

        if (!BookSpecifications.HasValidTitle(title))
            errors.Add(BookErrors.InvalidTitle);

        if (!BookSpecifications.HasValidAuthor(author))
            errors.Add(BookErrors.InvalidAuthor);

        if (!BookSpecifications.HasValidPrice(price))
            errors.Add(BookErrors.InvalidPrice);

        if (!BookSpecifications.HasValidStock(stock))
            errors.Add(BookErrors.InvalidStock);

        Title = title;
        Author = author;
        Price = price;
        Stock = stock;

        return errors.Count > 0 ? errors : this;
    }

    public ErrorOr<Book> RefillStock(int quantity)
    {
        if (quantity <= 0)
        {
            return BookErrors.InvalidStock;
        }

        Stock += quantity;
        return this;
    }

    public ErrorOr<Book> VoteAsync()
    {
        Vote++;
        return this;
    }
}