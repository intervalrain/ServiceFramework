using AuthorSystem.Domain.Entities;

namespace AuthorSystem.Domain.Repositories;

public interface IAuthorRepository
{
    Task<Author?> GetAsync(Guid id);
    Task<Author?> GetByEmailAsync(string email);
    Task<List<Author>> GetListAsync();
    Task<Author> InsertAsync(Author author);
    Task<Author> UpdateAsync(Author author);
    Task DeleteAsync(Guid id);
}