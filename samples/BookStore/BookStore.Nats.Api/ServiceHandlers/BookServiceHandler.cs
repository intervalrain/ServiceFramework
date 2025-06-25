using BookStore.Application.Dtos;
using BookStore.Application.Services;
using EdgeSync.ServiceFramework;
using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.JetStream;
using NATS.Client.Core;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BookStore.Nats.Api.ServiceHandlers;

public class BookServiceHandler : ServiceHandler
{
    private readonly IBookAppService _bookAppService;

    public override string ServiceName => "bookstore";
    public override string ServiceVersion => "1.0.0";
    public override string QueueGroup => "bookstore-queue";
    private readonly JsonSerializerOptions _jsonOptions;

    public BookServiceHandler(
        ILogger<BookServiceHandler> logger,
        IJetStreamClientFactory factory,
        IBookAppService bookAppService)
        : base(logger, factory, "bus")
    {
        _bookAppService = bookAppService;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    [Subject("getBook", "bookstore.books.*.get")]
    public async ValueTask GetBookAsync(ServiceMsgContext<string> context, string message)
    {
        try
        {
            var id = context.Subject.Split('.')[2];
            if (!Guid.TryParse(id, out Guid guid))
            {
                var errorResponse = CreateErrorResponse("Invalid request: Id is required");
                await context.ServiceMsg.ReplyAsync(errorResponse);
                return;
            }

            var result = await _bookAppService.GetAsync(guid);
            if (result.IsError)
            {
                var errorResponse = CreateErrorResponse(string.Join(", ", result.Errors.Select(e => e.Description)));
                await context.ServiceMsg.ReplyAsync(errorResponse);
            }
            else
            {
                var successResponse = CreateSuccessResponse(result.Value);
                await context.ServiceMsg.ReplyAsync(successResponse);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in GetBookAsync");
            var errorResponse = CreateErrorResponse($"Internal error: {ex.Message}");
            await context.ServiceMsg.ReplyAsync(errorResponse);
        }
    }

    [Subject("getBooks", "bookstore.books.get")]
    public async ValueTask GetBooksAsync(ServiceMsgContext<string> context, string message)
    {
        try
        {
            var result = await _bookAppService.GetListAsync();
            
            if (result.IsError)
            {
                var errorResponse = CreateErrorResponse(string.Join(", ", result.Errors.Select(e => e.Description)));
                await context.ServiceMsg.ReplyAsync(errorResponse);
            }
            else
            {
                var successResponse = CreateSuccessResponse(result.Value);
                await context.ServiceMsg.ReplyAsync(successResponse);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in GetBooksAsync");
            var errorResponse = CreateErrorResponse($"Internal error: {ex.Message}");
            await context.ServiceMsg.ReplyAsync(errorResponse);
        }
    }

    [Subject("createBook", "bookstore.books.post")]
    public async ValueTask CreateBookAsync(ServiceMsgContext<string> context, string message)
    {
        try
        {
            var request = JsonSerializer.Deserialize<CreateBookDto>(message, _jsonOptions);
            if (request == null)
            {
                var errorResponse = CreateErrorResponse("Invalid request payload");
                await context.ServiceMsg.ReplyAsync(errorResponse);
                return;
            }

            var result = await _bookAppService.CreateAsync(request);
            
            if (result.IsError)
            {
                var errorResponse = CreateErrorResponse(string.Join(", ", result.Errors.Select(e => e.Description)));
                await context.ServiceMsg.ReplyAsync(errorResponse);
            }
            else
            {
                var successResponse = CreateSuccessResponse(result.Value);
                await context.ServiceMsg.ReplyAsync(successResponse);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in CreateBookAsync");
            var errorResponse = CreateErrorResponse($"Internal error: {ex.Message}");
            await context.ServiceMsg.ReplyAsync(errorResponse);
        }
    }

    [Subject("updateBook", "bookstore.books.*.put")]
    public async ValueTask UpdateBookAsync(ServiceMsgContext<string> context, string message)
    {
        try
        {
            var id = context.Subject.Split('.')[2];
            var request = JsonSerializer.Deserialize<UpdateBookRequest>(message, _jsonOptions);
            if (!Guid.TryParse(id, out Guid guid) || request?.UpdateDto == null)
            {
                var errorResponse = CreateErrorResponse("Invalid request: Id and UpdateDto are required");
                await context.ServiceMsg.ReplyAsync(errorResponse);
                return;
            }

            var result = await _bookAppService.UpdateAsync(guid, request.UpdateDto);
            
            if (result.IsError)
            {
                var errorResponse = CreateErrorResponse(string.Join(", ", result.Errors.Select(e => e.Description)));
                await context.ServiceMsg.ReplyAsync(errorResponse);
            }
            else
            {
                var successResponse = CreateSuccessResponse(result.Value);
                await context.ServiceMsg.ReplyAsync(successResponse);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in UpdateBookAsync");
            var errorResponse = CreateErrorResponse($"Internal error: {ex.Message}");
            await context.ServiceMsg.ReplyAsync(errorResponse);
        }
    }

    [Subject("deleteBook", "bookstore.books.*.delete")]
    public async ValueTask DeleteBookAsync(ServiceMsgContext<string> context, string message)
    {
        try
        {
            var id = context.Subject.Split('.')[2];
            if (!Guid.TryParse(id, out Guid guid))
            {
                var errorResponse = CreateErrorResponse("Invalid request: Id is required");
                await context.ServiceMsg.ReplyAsync(errorResponse);
                return;
            }

            var result = await _bookAppService.DeleteAsync(guid);
            
            if (result.IsError)
            {
                var errorResponse = CreateErrorResponse(string.Join(", ", result.Errors.Select(e => e.Description)));
                await context.ServiceMsg.ReplyAsync(errorResponse);
            }
            else
            {
                var successResponse = CreateSuccessResponse("Book deleted successfully");
                await context.ServiceMsg.ReplyAsync(successResponse);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in DeleteBookAsync");
            var errorResponse = CreateErrorResponse($"Internal error: {ex.Message}");
            await context.ServiceMsg.ReplyAsync(errorResponse);
        }
    }


    private string CreateSuccessResponse(object data)
    {
        var response = new
        {
            Success = true,
            Data = data,
            Error = (string?)null
        };
        return JsonSerializer.Serialize(response, _jsonOptions);
    }

    private string CreateErrorResponse(string error)
    {
        var response = new
        {
            Success = false,
            Data = (object?)null,
            Error = error
        };
        return JsonSerializer.Serialize(response, _jsonOptions);
    }
}

public class UpdateBookRequest
{
    public UpdateBookDto UpdateDto { get; set; } = null!;
}