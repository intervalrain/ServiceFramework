using System.Text;
using System.Text.Json;
using BookStore.Application.Events;
using BookStore.Application.Services;
using EdgeSync.ServiceFramework;
using EdgeSync.ServiceFramework.JetStream;
using Microsoft.Extensions.Logging;

namespace BookStore.Nats.Api.EventHandlers;

public class BookRefillEventHandler : BaseEventHandler
{
    private readonly IBookAppService _bookAppService;
    private readonly JsonSerializerOptions _jsonOptions;

    public BookRefillEventHandler(
        ILogger<BaseEventHandler> logger,
        IJetStreamClientFactory factory,
        IBookAppService bookAppService)
        : base(logger, factory, "broker")
    {
        _bookAppService = bookAppService;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    protected override string SubjectName => "bookstore.events.book.refill";
    protected override string StreamName => "bookstore-stream";
    protected override string ConsumerName => "book-refill-consumer";

    protected override async Task HandleInputEventCore(byte[] message, string subject)
    {
        try
        {
            var messageJson = Encoding.UTF8.GetString(message);
            Logger.LogDebug("Received book refill event: {Message} on subject: {Subject}", messageJson, subject);

            var refillEvent = JsonSerializer.Deserialize<BookRefillEvent>(messageJson, _jsonOptions);
            if (refillEvent == null)
            {
                Logger.LogWarning("Failed to deserialize book refill event from subject: {Subject}", subject);
                return;
            }

            Logger.LogInformation("Processing book refill event for BookId: {BookId}, Quantity: {Quantity}, Reason: {Reason}", 
                refillEvent.BookId, refillEvent.RefillQuantity, refillEvent.Reason);

            var result = await _bookAppService.RefillStockAsync(refillEvent.BookId, refillEvent.RefillQuantity);
            
            if (result.IsError)
            {
                var errorMessages = string.Join(", ", result.Errors.Select(e => e.Description));
                Logger.LogError("Failed to refill stock for BookId: {BookId}. Errors: {Errors}", 
                    refillEvent.BookId, errorMessages);
                return;
            }

            Logger.LogInformation("Successfully refilled stock for BookId: {BookId}. New stock level: {Stock}", 
                refillEvent.BookId, result.Value.Stock);
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "Failed to deserialize book refill event from subject: {Subject}", subject);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error processing book refill event from subject: {Subject}", subject);
        }
    }
}