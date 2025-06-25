using BookStore.Domain.Entities;

namespace BookStore.Domain.Repositories;

public interface IBookRepository
{
    Task<Book?> GetAsync(Guid id);
    Task<List<Book>> GetListAsync();
    Task<Book> InsertAsync(Book book);
    Task<Book> UpdateAsync(Book book);
    Task DeleteAsync(Guid id);
}