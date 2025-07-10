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
    // Four modes demo area
    // =====================================================

    #region Mode 1: Request/Response (Including CRUD methods above)
    // The GetAsync, CreateAsync, UpdateAsync, DeleteAsync methods above are Request/Response mode
    // Features: Has return value (ErrorOr<T>)
    // Decision tree path: (1)Has ReturnType -> (2)Request/Response Mode
    #endregion

    #region Mode 2: Pub/Sub Push Mode with JetStream (Including VoteAsync)
    // VoteAsync is Pub/Sub Push with JetStream mode
    // Features: No return value, JetStream not explicitly disabled, DefaultJetStreamEnable=true
    // Decision tree path: (1)No ReturnType -> (3)No Collection -> (5)No JetStreamPullAttribute -> (7)No JetStreamSubject -> (11)DefaultStreamEnabled=true -> (16)Pub/Sub Push Mode with JetStream

    /// <summary>
    /// Explicitly use JetStream Push Mode to publish events
    /// Decision tree path: (1)No ReturnType -> (3)No Collection -> (5)No JetStreamPullAttribute -> (7)Has JetStreamSubject -> (10)Pub/Sub Pull Mode with JetStream
    /// </summary>
    [Subject("author-created-event", "authorsys.events.author.created")]
    [JetStream(true)] // Explicitly enable JetStream
    public Task PublishAuthorCreatedEvent(AuthorEventDto eventData)
    {
        Logger.LogInformation("Publishing author created event for author {AuthorId}", eventData.AuthorId);
        // This will use JetStream Push Mode to publish events
        // Suitable for events that need persistence and replay capabilities
        return Task.CompletedTask;
    }
    #endregion

    #region Mode 3: Classic Pub/Sub (without JetStream)
    /// <summary>
    /// Classic Pub/Sub Push Mode example - simple status update
    /// Decision tree path: (1)No ReturnType -> (3)Has Collection -> (4)No JetStreamPullAttribute -> (6)No JetStreamSubject -> (13)Explicitly disable JetStream -> (13)Pub/Sub Push Mode classic
    /// </summary>
    [Subject("batch-author-update", "authorsys.classic.batch.update")]
    [JetStream(false)] // Explicitly disable JetStream, use Classic Mode
    public Task HandleBatchAuthorUpdate(List<BatchAuthorOperationDto> operations)
    {
        Logger.LogInformation("Processing {Count} batch author operations in classic mode", operations.Count);

        foreach (var operation in operations)
        {
            Logger.LogInformation("Processing operation {Type} for {Count} authors",
                operation.OperationType, operation.AuthorIds.Count);

            // Process batch operations
            // Classic mode is suitable for simple, non-persistent batch operations
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Another Classic Pub/Sub example - fast status synchronization
    /// </summary>
    [Subject("author-stats-sync", "authorsys.classic.stats.sync")]
    [JetStream(false)] // Classic mode
    public Task SynchronizeAuthorStats(List<AuthorStatsUpdateDto> statsUpdates)
    {
        Logger.LogInformation("Synchronizing stats for {Count} authors in classic mode", statsUpdates.Count);

        // Classic mode is suitable for high-frequency, low-latency status synchronization
        // No persistence or replay capabilities are needed
        return Task.CompletedTask;
    }
    #endregion

    #region Mode 4: Pull Mode with JetStream
    /// <summary>
    /// JetStream Pull Mode example - handle high-priority notifications
    /// Decision tree path: (1)No ReturnType -> (3)No Collection -> (5)Has JetStreamPullAttribute -> (15)Pub/Sub Pull Mode with JetStream
    /// </summary>
    [Subject("priority-author-notification", "authorsys.pull.priority.notification")]
    [JetStreamPull(ConsumerName = "PriorityNotificationConsumer", MaxMessages = 5)]
    public async Task HandlePriorityNotification(AuthorNotificationDto notification)
    {
        Logger.LogInformation("Handling priority notification for author {AuthorId} in pull mode", 
            notification.AuthorId);
        
        // Pull mode is suitable for high-priority processing that needs to control consumption speed
        // Can control the number of messages pulled at once, suitable for important business logic
        
        // Simulate important business processing
        await ProcessHighPriorityNotification(notification);
    }

    /// <summary>
    /// Another Pull Mode example - handle important author events
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
        
        // Pull mode provides more precise control:
        // - Can control consumption speed
        // - Supports manual confirmation (ACK)
        // - Suitable for critical business processes that need to ensure successful processing
        
        try
        {
            await ProcessCriticalEvent(eventData);
            // In actual implementation, there will be ACK confirmation logic
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process critical event for author {AuthorId}", eventData.AuthorId);
            // In actual implementation, there will be NACK or retry logic
        }
    }

    /// <summary>
    /// Pull Mode example - batch processing low-priority tasks
    /// </summary>
    [Subject("batch-maintenance", "authorsys.pull.batch.maintenance")]
    [JetStreamPull(
        ConsumerName = "MaintenanceConsumer",
        MaxMessages = 10, // Batch process more messages
        AckPolicy = "Explicit"
    )]
    public async Task HandleBatchMaintenance(BatchAuthorOperationDto operation)
    {
        Logger.LogInformation("Handling maintenance operation {Type} for {Count} authors", 
            operation.OperationType, operation.AuthorIds.Count);
        
        // Pull mode is suitable for batch maintenance tasks:
        // - Can control batch size
        // - Process more messages when system load is low
        // - Ensure tasks are completed before confirmation
        
        await ProcessMaintenanceOperation(operation);
    }
    #endregion

    #region Helper Methods
    private async Task ProcessHighPriorityNotification(AuthorNotificationDto notification)
    {
        // Simulate high-priority notification processing
        Logger.LogInformation("Processing high priority notification: {Message}", notification.Message);
        
        // May include:
        // - Send real-time notifications
        // - Update emergency status
        // - Trigger related business processes
        
        await Task.Delay(100); // Simulate processing time
    }

    private async Task ProcessCriticalEvent(AuthorEventDto eventData)
    {
        // Simulate critical event processing
        Logger.LogInformation("Processing critical event: {EventType}", eventData.EventType);
        
        // May include:
        // - Update critical business data
        // - Send important alerts
        // - Execute business rule validation
        
        await Task.Delay(200); // Simulate processing time
    }

    private async Task ProcessMaintenanceOperation(BatchAuthorOperationDto operation)
    {
        // Simulate maintenance operation processing
        Logger.LogInformation("Processing maintenance operation: {Type}", operation.OperationType);
        
        // May include:
        // - Data cleanup
        // - Statistics calculation
        // - Index rebuild
        
        await Task.Delay(500); // Simulate longer processing time
    }
    #endregion
}