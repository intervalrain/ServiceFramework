using AuthorSystem.Application.Dtos;

using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;

using ErrorOr;

namespace AuthorSystem.Application.Services;

public interface IAuthorAppService : INatsService
{
    Task<ErrorOr<AuthorDto>> GetAsync(GetAuthorDto input);
    Task<ErrorOr<List<AuthorDto>>> GetListAsync();
    Task<ErrorOr<AuthorDto>> CreateAsync(CreateAuthorDto input);
    Task<ErrorOr<AuthorDto>> UpdateAsync(UpdateAuthorDto input);
    Task<ErrorOr<Deleted>> DeleteAsync(GetAuthorDto input);


    Task VoteAsync(GetAuthorDto input);
    Task PublishAuthorCreatedEvent(AuthorEventDto eventData);
    Task HandleBatchAuthorUpdate(List<BatchAuthorOperationDto> operations);
    Task SynchronizeAuthorStats(List<AuthorStatsUpdateDto> statsUpdates);
    Task HandlePriorityNotification(AuthorNotificationDto notification);
    Task HandleCriticalAuthorEvent(AuthorEventDto eventData);
    Task HandleBatchMaintenance(BatchAuthorOperationDto operation);
}