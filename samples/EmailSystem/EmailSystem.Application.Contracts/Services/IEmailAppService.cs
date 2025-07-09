using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;

using EmailSystem.Application.Contracts.Dtos;
using ErrorOr;

namespace EmailSystem.Application.Contracts.Services;

public interface IEmailAppService : INatsService
{
    Task<ErrorOr<EmailDto>> GetAsync(Guid id);
    Task<ErrorOr<List<EmailDto>>> GetListAsync();
    Task<ErrorOr<EmailDto>> SendEmailAsync(SendEmailDto input);
    Task<ErrorOr<EmailDto>> ProcessEmailEventAsync(EmailEventDto input);
}