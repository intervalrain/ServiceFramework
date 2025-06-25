using BookStore.Application.Dtos;
using BookStore.Nats.Client.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Api.Controllers;

[Route("api/[controller]")]
public class BooksController : ApiController
{
    private readonly IBookNatsClient _bookNatsClient;

    public BooksController(IBookNatsClient bookNatsClient)
    {
        _bookNatsClient = bookNatsClient;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBook(Guid id)
    {
        var result = await _bookNatsClient.GetBookAsync(id);
        return result.Match(
            book => Ok(book),
            errors => Problem(errors));
    }

    [HttpGet]
    public async Task<IActionResult> GetBooks()
    {
        var result = await _bookNatsClient.GetBooksAsync();
        return result.Match(
            books => Ok(books),
            errors => Problem(errors));
    }

    [HttpPost]
    public async Task<IActionResult> CreateBook(CreateBookDto createBookDto)
    {
        var result = await _bookNatsClient.CreateBookAsync(createBookDto);
        return result.Match(
            book => CreatedAtAction(nameof(GetBook), new { id = book.Id }, book),
            errors => Problem(errors));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateBook(Guid id, UpdateBookDto updateBookDto)
    {
        var result = await _bookNatsClient.UpdateBookAsync(id, updateBookDto);
        return result.Match(
            book => Ok(book),
            errors => Problem(errors));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteBook(Guid id)
    {
        var result = await _bookNatsClient.DeleteBookAsync(id);
        return result.Match(
            _ => NoContent(),
            errors => Problem(errors));
    }

    [HttpPost("{id:guid}/refill")]
    public async Task<IActionResult> RefillBookStock(Guid id, [FromBody] RefillStockRequest request)
    {
        var result = await _bookNatsClient.RefillStockAsync(id, request.Quantity);
        return result.Match(
            book => Ok(),
            errors => Problem(errors));
    }
}

public class RefillStockRequest
{
    public int Quantity { get; set; }
    public string? Reason { get; set; }
}