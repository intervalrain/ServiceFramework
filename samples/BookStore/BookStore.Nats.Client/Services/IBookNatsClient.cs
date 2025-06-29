using BookStore.Application.Dtos;
using ErrorOr;

namespace BookStore.Nats.Client.Services;

public interface IBookNatsClient
{
    Task<ErrorOr<BookDto>> GetBookAsync(Guid id);
    Task<ErrorOr<List<BookDto>>> GetBooksAsync();
    Task<ErrorOr<BookDto>> CreateBookAsync(CreateBookDto createDto);
    Task<ErrorOr<BookDto>> UpdateBookAsync(Guid id, UpdateBookDto updateDto);
    Task<ErrorOr<Deleted>> DeleteBookAsync(Guid id);
    Task<ErrorOr<BookDto>> RefillStockAsync(Guid id, int quantity);
    Task<ErrorOr<BookDto>> VoteAsync(Guid id);
}