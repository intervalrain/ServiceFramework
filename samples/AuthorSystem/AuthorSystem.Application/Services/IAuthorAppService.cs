using AuthorSystem.Application.Dtos;

using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;

using ErrorOr;

namespace AuthorSystem.Application.Services;

public interface IAuthorAppService : INatsService
{
    Task<ErrorOr<AuthorDto>> GetAsync(Guid id);
    Task<ErrorOr<List<AuthorDto>>> GetListAsync();
    Task<ErrorOr<AuthorDto>> CreateAsync(CreateAuthorDto input);
    Task<ErrorOr<AuthorDto>> UpdateAsync(Guid id, UpdateAuthorDto input);
    Task<ErrorOr<Deleted>> DeleteAsync(Guid id);
}