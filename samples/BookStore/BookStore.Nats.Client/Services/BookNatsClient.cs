using BookStore.Application.Dtos;
using BookStore.Application.Events;
using BookStore.Nats.Client.Errors;
using EdgeSync.ServiceFramework.JetStream;
using ErrorOr;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace BookStore.Nats.Client.Services;

public class BookNatsClient : IBookNatsClient
{
    private readonly IJetStreamClient _bus;
    private readonly IJetStreamClient _broker;
    private readonly ILogger<BookNatsClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public BookNatsClient(IJetStreamClientFactory factory, ILogger<BookNatsClient> logger)
    {
        _bus = factory.CreateClient("bus");
        _broker = factory.CreateClient("broker");

        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<ErrorOr<BookDto>> GetBookAsync(Guid id)
    {
        try
        {
            var request = new { };
            var subject = $"bookstore.books.{id}.get";
            
            var response = await SendRequestAsync<BookDto>(subject, request);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting book with id {BookId}", id);
            return CommunicationErrors.UnexpectedError;
        }
    }

    public async Task<ErrorOr<List<BookDto>>> GetBooksAsync()
    {
        try
        {
            var request = new { };
            var subject = $"bookstore.books.get";
            
            var response = await SendRequestAsync<List<BookDto>>(subject, request);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting books list");
            return CommunicationErrors.UnexpectedError;
        }
    }

    public async Task<ErrorOr<BookDto>> CreateBookAsync(CreateBookDto createDto)
    {
        try
        {
            var subject = $"bookstore.books.post";
            
            var response = await SendRequestAsync<BookDto>(subject, createDto);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating book");
            return CommunicationErrors.UnexpectedError;
        }
    }

    public async Task<ErrorOr<BookDto>> UpdateBookAsync(Guid id, UpdateBookDto updateDto)
    {
        try
        {
            var request = new { UpdateDto = updateDto };
            var subject = $"bookstore.books.{id}.put";
            
            var response = await SendRequestAsync<BookDto>(subject, request);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating book with id {BookId}", id);
            return CommunicationErrors.UnexpectedError;
        }
    }

    public async Task<ErrorOr<Deleted>> DeleteBookAsync(Guid id)
    {
        try
        {
            var request = new { Id = id };
            var subject = $"bookstore.books.{id}.delete";
            
            var response = await SendRequestAsync<string>(subject, request);
            return response.Match<ErrorOr<Deleted>>(
                _ => Result.Deleted,
                errors => errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting book with id {BookId}", id);
            return CommunicationErrors.UnexpectedError;
        }
    }

    public async Task<ErrorOr<BookDto>> RefillStockAsync(Guid id, int quantity)
    {
        try
        {
            var refillEvent = new BookRefillEvent
            {
                BookId = id,
                RefillQuantity = quantity,
                Source = "NatsClient"
            };

            var eventJson = JsonSerializer.Serialize(refillEvent, _jsonOptions);
            var eventBytes = Encoding.UTF8.GetBytes(eventJson);

            await _broker.NatsPublishAsync("bookstore.events.book.refill", eventBytes);
            
            _logger.LogDebug("Published refill event for book {BookId} with quantity {Quantity}", id, quantity);
            
            return new BookDto { Id = id };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing refill event for book with id {BookId}", id);
            return CommunicationErrors.UnexpectedError;
        }
    }
    
    public async Task<ErrorOr<BookDto>> VoteAsync(Guid id)
    {
        try
        {
            var voteEvent = new BookVoteEvent
            {
                BookId = id,
            };

            var eventJson = JsonSerializer.Serialize(voteEvent, _jsonOptions);
            var eventBytes = Encoding.UTF8.GetBytes(eventJson);

            await _bus.NatsPublishAsync("bookstore.events.book.vote", eventBytes);
            
            _logger.LogDebug("Published vote event for book {BookId}", id);
            
            return new BookDto { Id = id };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing vote event for book with id {BookId}", id);
            return CommunicationErrors.UnexpectedError;
        }
    }

    private async Task<ErrorOr<T>> SendRequestAsync<T>(string subject, object request)
    {
        try
        {
            var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
            _logger.LogDebug("Sending request to {Subject}: {Request}", subject, requestJson);

            var responseJson = await _bus.RequestAsync(subject, requestJson);
            if (responseJson == null)
            {
                _logger.LogWarning("Received null response from {Subject}", subject);
                return CommunicationErrors.InvalidResponse;
            }
            _logger.LogDebug("Received response from {Subject}: {Response}", subject, responseJson);

            var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(responseJson, _jsonOptions);
            if (apiResponse == null)
            {
                _logger.LogWarning("Failed to deserialize response from {Subject}", subject);
                return CommunicationErrors.SerializationFailed;
            }

            if (!apiResponse.Success)
            {
                _logger.LogWarning("API returned error from {Subject}: {Error}", subject, apiResponse.Error);
                var errorMessage = apiResponse.Error ?? "Unknown error";

                // 根據錯誤訊息判斷錯誤類型
                if (errorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                    errorMessage.Contains("NotFound", StringComparison.OrdinalIgnoreCase))
                {
                    return Error.NotFound("BookStore.NotFound", errorMessage);
                }
                else if (errorMessage.Contains("Invalid", StringComparison.OrdinalIgnoreCase) ||
                         errorMessage.Contains("required", StringComparison.OrdinalIgnoreCase) ||
                         errorMessage.Contains("validation", StringComparison.OrdinalIgnoreCase))
                {
                    return Error.Validation("BookStore.Validation", errorMessage);
                }
                else
                {
                    return Error.Failure("BookStore.ApiError", errorMessage);
                }
            }

            if (apiResponse.Data == null)
            {
                _logger.LogWarning("API returned null data from {Subject}", subject);
                return CommunicationErrors.InvalidResponse;
            }

            return apiResponse.Data;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Request timeout for {Subject}", subject);
            return CommunicationErrors.RequestTimeout;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON serialization error for {Subject}", subject);
            return CommunicationErrors.SerializationFailed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection error for {Subject}", subject);
            return CommunicationErrors.ConnectionFailed;
        }
    }

    private class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Error { get; set; }
    }
}