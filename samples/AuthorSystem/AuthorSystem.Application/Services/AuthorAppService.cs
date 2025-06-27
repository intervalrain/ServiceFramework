using AutoMapper;
using AuthorSystem.Application.Dtos;
using AuthorSystem.Domain.Entities;
using AuthorSystem.Domain.Errors;
using AuthorSystem.Domain.Repositories;
using ErrorOr;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;
using Microsoft.Extensions.Logging;
using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.Abstractions.Attributes;

namespace AuthorSystem.Application.Services;

public class AuthorAppService : NatsService, IAuthorAppService
{
    private readonly IAuthorRepository _authorRepository;
    private readonly IMapper _mapper;

    public AuthorAppService(ILogger<AuthorAppService> logger, IAuthorRepository authorRepository, IMapper mapper)
        : base(logger)
    {
        _authorRepository = authorRepository;
        _mapper = mapper;
    }

    /// <summary>
    /// Get author by ID
    /// </summary>
    /// <param name="id">The unique identifier of the author</param>
    /// <returns>Author details if found, otherwise error</returns>
    [Subject("get-author", "authorsys.authors.*.get")]
    public async Task<ErrorOr<AuthorDto>> GetAsync(Guid id)
    {
        var author = await _authorRepository.GetAsync(id);
        if (author is null)
        {
            return AuthorErrors.NotFound;
        }

        return _mapper.Map<AuthorDto>(author);
    }

    /// <summary>
    /// Get all authors
    /// </summary>
    /// <returns>List of all authors</returns>
    [Subject("get-authors", "authorsys.authors.get")]
    public async Task<ErrorOr<List<AuthorDto>>> GetListAsync()
    {
        var authors = await _authorRepository.GetListAsync();
        return _mapper.Map<List<AuthorDto>>(authors);
    }

    /// <summary>
    /// Create a new author
    /// </summary>
    /// <param name="input">The author information to create</param>
    /// <returns>Created author details if successful, otherwise error</returns>
    [Subject("create-author", "authorsys.authors.post")]
    public async Task<ErrorOr<AuthorDto>> CreateAsync(CreateAuthorDto input)
    {
        var existingAuthor = await _authorRepository.GetByEmailAsync(input.Email);
        if (existingAuthor is not null)
        {
            return AuthorErrors.EmailAlreadyExists;
        }

        var result = Author.Create(input.Name, input.Email, input.Biography, input.BirthDate);
        if (result.IsError)
        {
            return result.Errors;
        }

        var author = result.Value;
        var createdAuthor = await _authorRepository.InsertAsync(author);
        return _mapper.Map<AuthorDto>(createdAuthor);
    }

    /// <summary>
    /// Update an existing author
    /// </summary>
    /// <param name="id">The unique identifier of the author to update</param>
    /// <param name="input">The updated author information</param>
    /// <returns>Updated author details if successful, otherwise error</returns>
    [Subject("update-author", "authorsys.authors.*.put")]
    public async Task<ErrorOr<AuthorDto>> UpdateAsync(Guid id, UpdateAuthorDto input)
    {
        var existingAuthor = await _authorRepository.GetAsync(id);
        if (existingAuthor is null)
        {
            return AuthorErrors.NotFound;
        }

        var authorWithSameEmail = await _authorRepository.GetByEmailAsync(input.Email);
        if (authorWithSameEmail is not null && authorWithSameEmail.Id != id)
        {
            return AuthorErrors.EmailAlreadyExists;
        }

        var validationResult = existingAuthor.Update(input.Name, input.Email, input.Biography, input.BirthDate);
        if (validationResult.IsError)
        {
            return validationResult.Errors;
        }

        var updatedAuthor = await _authorRepository.UpdateAsync(validationResult.Value);
        return _mapper.Map<Author, AuthorDto>(updatedAuthor);
    }

    /// <summary>
    /// Delete an author
    /// </summary>
    /// <param name="id">The unique identifier of the author to delete</param>
    /// <returns>Success status if deleted, otherwise error</returns>
    [Subject("delete-author", "authorsys.authors.*.delete")]
    public async Task<ErrorOr<Deleted>> DeleteAsync(Guid id)
    {
        var author = await _authorRepository.GetAsync(id);
        if (author is null)
        {
            return AuthorErrors.NotFound;
        }

        await _authorRepository.DeleteAsync(id);
        return Result.Deleted;
    }

    [Subject("vote-author", "authorsys.authors.*.vote")]
    public async Task VoteAsync(Guid id)
    {
        var author = await _authorRepository.GetAsync(id);
        if (author is null)
        {
            return;
        }
        var result = author.AddVote();

        if (result.IsError)
        {
            return;
        }
        await _authorRepository.UpdateAsync(author);
    }

    // =====================================================
    // 四種模式演示區域
    // =====================================================

    #region Mode 1: Request/Response (已包含上面的 CRUD 方法)
    // 上面的 GetAsync, CreateAsync, UpdateAsync, DeleteAsync 都是 Request/Response 模式
    // 特徵: 有回傳值 (ErrorOr<T>)
    // 決策樹路徑: (1)有ReturnType -> (2)Request/Response Mode
    #endregion

    #region Mode 2: Pub/Sub Push Mode with JetStream (已包含上面的 VoteAsync)
    // VoteAsync 是 Pub/Sub Push with JetStream 模式
    // 特徵: 無回傳值, 未明確禁用 JetStream, DefaultJetStreamEnable=true
    // 決策樹路徑: (1)無ReturnType -> (3)否Collection -> (5)否JetStreamPullAttribute -> (7)否JetStreamSubject -> (11)DefaultStreamEnabled=true -> (16)Pub/Sub Push Mode with JetStream

    /// <summary>
    /// 明確使用 JetStream Push Mode 的事件發布
    /// 決策樹路徑: (1)無ReturnType -> (3)否Collection -> (5)否JetStreamPullAttribute -> (7)有JetStreamSubject -> (10)Pub/Sub Pull Mode with JetStream
    /// </summary>
    [Subject("author-created-event", "authorsys.events.author.created")]
    [JetStream(true)] // 明確啟用 JetStream
    public Task PublishAuthorCreatedEvent(AuthorEventDto eventData)
    {
        Logger.LogInformation("Publishing author created event for author {AuthorId}", eventData.AuthorId);
        // 這會使用 JetStream Push Mode 發布事件
        // 適用於需要持久化、重播能力的事件
        return Task.CompletedTask;
    }
    #endregion

    #region Mode 3: Classic Pub/Sub (without JetStream)
    /// <summary>
    /// Classic Pub/Sub Push Mode 範例 - 簡單的狀態更新
    /// 決策樹路徑: (1)無ReturnType -> (3)是Collection -> (4)否JetStreamPullAttribute -> (6)否JetStreamSubject -> 明確禁用JetStream -> (13)Pub/Sub Push Mode classic
    /// </summary>
    [Subject("batch-author-update", "authorsys.classic.batch.update")]
    [JetStream(false)] // 明確禁用 JetStream，使用 Classic Mode
    public Task HandleBatchAuthorUpdate(List<BatchAuthorOperationDto> operations)
    {
        Logger.LogInformation("Processing {Count} batch author operations in classic mode", operations.Count);

        foreach (var operation in operations)
        {
            Logger.LogInformation("Processing operation {Type} for {Count} authors",
                operation.OperationType, operation.AuthorIds.Count);

            // 處理批次操作
            // Classic mode 適用於簡單、無需持久化的批次操作
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 另一個 Classic Pub/Sub 範例 - 快速狀態同步
    /// </summary>
    [Subject("author-stats-sync", "authorsys.classic.stats.sync")]
    [JetStream(false)] // Classic mode
    public Task SynchronizeAuthorStats(List<AuthorStatsUpdateDto> statsUpdates)
    {
        Logger.LogInformation("Synchronizing stats for {Count} authors in classic mode", statsUpdates.Count);

        // Classic mode 適用於高頻率、低延遲的狀態同步
        // 不需要持久化或重播能力
        return Task.CompletedTask;
    }
    #endregion

    #region Mode 4: Pull Mode with JetStream
    /// <summary>
    /// JetStream Pull Mode 範例 - 處理高優先級通知
    /// 決策樹路徑: (1)無ReturnType -> (3)否Collection -> (5)有JetStreamPullAttribute -> (15)Pub/Sub Pull Mode with JetStream
    /// </summary>
    [Subject("priority-author-notification", "authorsys.pull.priority.notification")]
    [JetStreamPull(ConsumerName = "PriorityNotificationConsumer", MaxMessages = 5)]
    public async Task HandlePriorityNotification(AuthorNotificationDto notification)
    {
        Logger.LogInformation("Handling priority notification for author {AuthorId} in pull mode", 
            notification.AuthorId);
        
        // Pull mode 適用於需要控制消費速度的高優先級處理
        // 可以控制一次拉取的消息數量，適合重要業務邏輯
        
        // 模擬重要業務處理
        await ProcessHighPriorityNotification(notification);
    }

    /// <summary>
    /// 另一個 Pull Mode 範例 - 處理重要的作者事件
    /// </summary>
    [Subject("critical-author-event", "authorsys.pull.critical.event")]
    [JetStreamPull(
        ConsumerName = "CriticalEventConsumer", 
        ConsumerGroup = "AuthorServiceGroup",
        MaxMessages = 3,
        AckPolicy = "Explicit"
    )]
    public async Task HandleCriticalAuthorEvent(AuthorEventDto eventData)
    {
        Logger.LogInformation("Handling critical event {EventType} for author {AuthorId}", 
            eventData.EventType, eventData.AuthorId);
        
        // Pull mode 提供更精確的控制：
        // - 可以控制消費速度
        // - 支援手動確認（ACK）
        // - 適用於需要確保處理成功的關鍵業務
        
        try
        {
            await ProcessCriticalEvent(eventData);
            // 在實際實作中，這裡會有 ACK 確認邏輯
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process critical event for author {AuthorId}", eventData.AuthorId);
            // 在實際實作中，這裡會有 NACK 或重試邏輯
        }
    }

    /// <summary>
    /// Pull Mode 範例 - 批次處理低優先級任務
    /// </summary>
    [Subject("batch-maintenance", "authorsys.pull.batch.maintenance")]
    [JetStreamPull(
        ConsumerName = "MaintenanceConsumer",
        MaxMessages = 10, // 批次處理更多消息
        AckPolicy = "Explicit"
    )]
    public async Task HandleBatchMaintenance(BatchAuthorOperationDto operation)
    {
        Logger.LogInformation("Handling maintenance operation {Type} for {Count} authors", 
            operation.OperationType, operation.AuthorIds.Count);
        
        // Pull mode 適用於批次維護任務：
        // - 可以控制處理批次大小
        // - 在系統負載低時處理更多消息
        // - 確保任務完成後再確認
        
        await ProcessMaintenanceOperation(operation);
    }
    #endregion

    #region Helper Methods
    private async Task ProcessHighPriorityNotification(AuthorNotificationDto notification)
    {
        // 模擬高優先級通知處理
        Logger.LogInformation("Processing high priority notification: {Message}", notification.Message);
        
        // 可能包括：
        // - 發送即時通知
        // - 更新緊急狀態
        // - 觸發相關業務流程
        
        await Task.Delay(100); // 模擬處理時間
    }

    private async Task ProcessCriticalEvent(AuthorEventDto eventData)
    {
        // 模擬關鍵事件處理
        Logger.LogInformation("Processing critical event: {EventType}", eventData.EventType);
        
        // 可能包括：
        // - 更新關鍵業務數據
        // - 發送重要警報
        // - 執行業務規則驗證
        
        await Task.Delay(200); // 模擬處理時間
    }

    private async Task ProcessMaintenanceOperation(BatchAuthorOperationDto operation)
    {
        // 模擬維護操作處理
        Logger.LogInformation("Processing maintenance operation: {Type}", operation.OperationType);
        
        // 可能包括：
        // - 數據清理
        // - 統計計算
        // - 索引重建
        
        await Task.Delay(500); // 模擬較長的處理時間
    }
    #endregion
}