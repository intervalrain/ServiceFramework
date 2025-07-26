using AuthorSystem.Domain.Entities;
using AuthorSystem.Domain.Repositories;
using System.Collections.Concurrent;

namespace AuthorSystem.Infrastructure.Repositories;

public class InMemoryBookRepository : IBookRepository
{
    private readonly static ConcurrentDictionary<string, Book> _books = new();

    public InMemoryBookRepository()
    {
        SeedData();
    }

    public Task<Book?> GetAsync(string id)
    {
        _books.TryGetValue(id, out var book);
        return Task.FromResult(book);
    }

    public Task<Book?> GetByIsbnAsync(string isbn)
    {
        var book = _books.Values.FirstOrDefault(b => b.Isbn.Equals(isbn, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(book);
    }

    public Task<List<Book>> GetListAsync()
    {
        return Task.FromResult(_books.Values.OrderBy(b => b.Title).ToList());
    }

    public Task<Book> InsertAsync(Book book)
    {
        book.Id = Guid.NewGuid().ToString();
        book.CreatedAt = DateTime.UtcNow;
        _books.TryAdd(book.Id, book);
        return Task.FromResult(book);
    }

    public Task<Book> UpdateAsync(Book book)
    {
        _books.TryGetValue(book.Id, out var existingBook);
        if (existingBook != null)
        {
            existingBook.Title = book.Title;
            existingBook.AuthorId = book.AuthorId;
            existingBook.Isbn = book.Isbn;
            existingBook.Description = book.Description;
            existingBook.PublicationYear = book.PublicationYear;
            existingBook.Price = book.Price;
            existingBook.IsAvailable = book.IsAvailable;
            existingBook.UpdatedAt = DateTime.UtcNow;
            _books.TryUpdate(book.Id, existingBook, existingBook);
        }
        return Task.FromResult(existingBook ?? book);
    }

    public Task DeleteAsync(string id)
    {
        _books.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    private void SeedData()
    {
        // Note: In a real application, these author IDs would come from the actual Author entities
        var authorBobMartin = "00000000-0000-0000-0000-000000000001";
        var authorEricEvans = "00000000-0000-0000-0000-000000000002";
        var authorMartinFowler = "00000000-0000-0000-0000-000000000003";

        var books = new List<Book>
        {
            Book.Create(
                title: "Clean Architecture: A Craftsman's Guide to Software Structure and Design",
                authorId: authorBobMartin,
                isbn: "9780134494166",
                description: "Building upon the success of Clean Code and The Clean Coder, legendary software craftsman Robert C. Martin shows how to bring greater professionalism and discipline to application architecture and design.",
                publicationYear: 2017,
                price: 45.99
            ),
            Book.Create(
                title: "Clean Code: A Handbook of Agile Software Craftsmanship",
                authorId: authorBobMartin,
                isbn: "9780132350884",
                description: "Even bad code can function. But if code isn't clean, it can bring a development organization to its knees. This book is a must for any developer, software engineer, project manager, team lead, or systems analyst with an interest in producing better code.",
                publicationYear: 2008,
                price: 43.99
            ),
            Book.Create(
                title: "Domain-Driven Design: Tackling Complexity in the Heart of Software",
                authorId: authorEricEvans,
                isbn: "9780321125217",
                description: "Eric Evans' seminal work on how domain modeling can be used to make complex software more manageable. This book provides a broad framework for making design decisions and a vocabulary for discussing domain design.",
                publicationYear: 2003,
                price: 52.99
            ),
            Book.Create(
                title: "Patterns of Enterprise Application Architecture",
                authorId: authorMartinFowler,
                isbn: "9780321127426",
                description: "This book is a comprehensive guide to the patterns used in enterprise application development. It provides patterns for layering, domain logic, web presentation, concurrency, and more.",
                publicationYear: 2002,
                price: 49.99
            ),
            Book.Create(
                title: "Refactoring: Improving the Design of Existing Code",
                authorId: authorMartinFowler,
                isbn: "9780134757599",
                description: "The definitive guide to refactoring by the world's leading agile software development authority. This book shows you how to improve the design of existing code bases and enhance software maintainability.",
                publicationYear: 2018,
                price: 47.99
            )
        };

        foreach (var book in books)
        {
            book.Id = Guid.NewGuid().ToString();
            book.CreatedAt = DateTime.UtcNow;
            _books.TryAdd(book.Id, book);
        }
    }
}