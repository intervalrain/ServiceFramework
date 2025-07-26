using AuthorSystem.Domain.Entities;

namespace AuthorSystem.Domain.Repositories;

public interface IBookRepository
{
    Task<Book?> GetAsync(string id);
    Task<Book?> GetByIsbnAsync(string isbn);
    Task<List<Book>> GetListAsync();
    Task<Book> InsertAsync(Book book);
    Task<Book> UpdateAsync(Book book);
    Task DeleteAsync(string id);
}