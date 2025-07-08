using EmailSystem.Domain.Entities;
using EmailSystem.Domain.Repositories;

namespace EmailSystem.Infrastructure.Repositories;

public class InMemoryEmailRepository : IEmailRepository
{
    private readonly List<Email> _emails = new();

    public Task<Email?> GetByIdAsync(Guid id)
    {
        var email = _emails.FirstOrDefault(e => e.Id == id);
        return Task.FromResult(email);
    }

    public Task<List<Email>> GetAllAsync()
    {
        return Task.FromResult(_emails.ToList());
    }

    public Task<Email> AddAsync(Email email)
    {
        _emails.Add(email);
        return Task.FromResult(email);
    }

    public Task<Email> UpdateAsync(Email email)
    {
        var existingEmail = _emails.FirstOrDefault(e => e.Id == email.Id);
        if (existingEmail != null)
        {
            var index = _emails.IndexOf(existingEmail);
            _emails[index] = email;
        }
        return Task.FromResult(email);
    }

    public Task DeleteAsync(Guid id)
    {
        var email = _emails.FirstOrDefault(e => e.Id == id);
        if (email != null)
        {
            _emails.Remove(email);
        }
        return Task.CompletedTask;
    }
}