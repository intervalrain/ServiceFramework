using AuthorSystem.Application.Protos;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;
using ErrorOr;

namespace AuthorSystem.Application.Services;

public interface IBookAppService : INatsService
{
    Task<ErrorOr<BookDto>> GetAsync(GetBookDto input);
    Task<ErrorOr<List<BookDto>>> GetListAsync();
    Task<ErrorOr<BookDto>> CreateAsync(CreateBookDto input);
    Task<ErrorOr<BookDto>> UpdateAsync(UpdateBookDto input);
    Task<ErrorOr<Deleted>> DeleteAsync(GetBookDto input);
}