using AuthorSystem.Domain.Entities;
using AuthorSystem.Domain.Repositories;
using System.Collections.Concurrent;

namespace AuthorSystem.Infrastructure.Repositories;

public class InMemoryAuthorRepository : IAuthorRepository
{
    private readonly static ConcurrentDictionary<Guid, Author> _authors = new();

    public InMemoryAuthorRepository()
    {
        SeedData();
    }

    public Task<Author?> GetAsync(Guid id)
    {
        _authors.TryGetValue(id, out var author);
        return Task.FromResult(author);
    }

    public Task<Author?> GetByEmailAsync(string email)
    {
        var author = _authors.Values.FirstOrDefault(a => a.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(author);
    }

    public Task<List<Author>> GetListAsync()
    {
        return Task.FromResult(_authors.Values.OrderBy(a => a.Name).ToList());
    }

    public Task<Author> InsertAsync(Author author)
    {
        author.Id = Guid.NewGuid();
        author.CreatedAt = DateTime.UtcNow;
        _authors.TryAdd(author.Id, author);
        return Task.FromResult(author);
    }

    public Task<Author> UpdateAsync(Author author)
    {
        _authors.TryGetValue(author.Id, out var existingAuthor);
        if (existingAuthor != null)
        {
            existingAuthor.Name = author.Name;
            existingAuthor.Email = author.Email;
            existingAuthor.Biography = author.Biography;
            existingAuthor.BirthDate = author.BirthDate;
            existingAuthor.UpdatedAt = DateTime.UtcNow;
            _authors.TryUpdate(author.Id, existingAuthor, existingAuthor);
        }
        return Task.FromResult(existingAuthor ?? author);
    }

    public Task DeleteAsync(Guid id)
    {
        _authors.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    private void SeedData()
    {
        var authors = new List<Author>
        {
            Author.Create(
                name: "Robert C. Martin",
                email: "uncle.bob@cleancode.com",
                biography: "Software engineer and author known for his advocacy of software design principles and practices.",
                birthDate: new DateTime(1952, 12, 5)
            ).Value,
            Author.Create(
                name: "Eric Evans",
                email: "eric.evans@domainlanguage.com",
                biography: "Software engineer and author of Domain-Driven Design, a seminal work on object-oriented software design.",
                birthDate: new DateTime(1962, 3, 15)
            ).Value,
            Author.Create(
                name: "Martin Fowler",
                email: "martin@martinfowler.com",
                biography: "British software engineer, author and international speaker on software development.",
                birthDate: new DateTime(1963, 12, 18)
            ).Value
        };

        foreach (var author in authors)
        {
            _authors.TryAdd(author.Id, author);
        }
    }
}