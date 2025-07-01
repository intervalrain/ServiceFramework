using BookStore.Application.Dtos;
using ErrorOr;

namespace BookStore.Application.Services;

public interface IBookAppService
{
    Task<ErrorOr<BookDto>> GetAsync(Guid id);
    Task<ErrorOr<List<BookDto>>> GetListAsync();
    Task<ErrorOr<BookDto>> CreateAsync(CreateBookDto createDto);
    Task<ErrorOr<BookDto>> UpdateAsync(Guid id, UpdateBookDto updateDto);
    Task<ErrorOr<Deleted>> DeleteAsync(Guid id);
    Task<ErrorOr<BookDto>> RefillStockAsync(Guid id, int quantity);
    Task<ErrorOr<BookDto>> VoteAsync(Guid id);
}