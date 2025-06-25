using BookStore.Domain.Entities;
using BookStore.Domain.Repositories;
using System.Collections.Concurrent;

namespace BookStore.Infrastructure.Repositories;

public class InMemoryBookRepository : IBookRepository
{
    private readonly static ConcurrentDictionary<Guid, Book> _books = new();

    public InMemoryBookRepository()
    {
        SeedData();
    }

    public Task<Book?> GetAsync(Guid id)
    {
        _books.TryGetValue(id, out var book);
        return Task.FromResult(book);
    }

    public Task<List<Book>> GetListAsync()
    {
        return Task.FromResult(_books.Values.OrderBy(b => b.Title).ToList());
    }

    public Task<Book> InsertAsync(Book book)
    {
        book.Id = Guid.NewGuid();
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
            existingBook.Author = book.Author;
            existingBook.Price = book.Price;
            existingBook.Stock = book.Stock;
            _books.TryUpdate(book.Id, existingBook, existingBook);
        }
        return Task.FromResult(existingBook ?? book);
    }

    public Task DeleteAsync(Guid id)
    {
        _books.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    private void SeedData()
    {
        var books = new List<Book>
        {
            Book.Create(
                title: "Clean Architecture",
                author: "Robert C. Martin",
                isbn: "9780134494166",
                price: 45.99m,
                stock: 10
            ).Value,
            Book.Create(
                title: "Domain-Driven Design",
                author: "Eric Evans",
                isbn: "9780321125217",
                price: 52.99m,
                stock: 5
            ).Value,
            Book.Create(
                title: "Microservices Patterns",
                author: "Chris Richardson",
                isbn: "9781617294549",
                price: 48.99m,
                stock: 8
            ).Value
        };

        foreach (var book in books)
        {
            _books.TryAdd(book.Id, book);
        }
    }
}