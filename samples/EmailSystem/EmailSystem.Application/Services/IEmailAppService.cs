using EmailSystem.Application.Dtos;
using ErrorOr;

namespace EmailSystem.Application.Services;

public interface IEmailAppService
{
    Task<ErrorOr<EmailDto>> GetAsync(Guid id);
    Task<ErrorOr<List<EmailDto>>> GetListAsync();
    Task<ErrorOr<EmailDto>> SendEmailAsync(SendEmailDto input);
    Task<ErrorOr<EmailDto>> ProcessEmailEventAsync(EmailEventDto input);
}