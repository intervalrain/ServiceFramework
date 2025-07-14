using EmailSystem.Domain.Entities;

using ErrorOr;

namespace EmailSystem.Application.Contracts.Services;

public interface IEmailSender
{
    Task<ErrorOr<bool>> SendEmailAsync(Email email);
}