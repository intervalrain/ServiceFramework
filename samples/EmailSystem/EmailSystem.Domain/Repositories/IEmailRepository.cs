using EmailSystem.Domain.Entities;

namespace EmailSystem.Domain.Repositories;

public interface IEmailRepository
{
    Task<Email?> GetByIdAsync(Guid id);
    Task<List<Email>> GetAllAsync();
    Task<Email> AddAsync(Email email);
    Task<Email> UpdateAsync(Email email);
    Task DeleteAsync(Guid id);
}