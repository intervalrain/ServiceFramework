using AutoMapper;
using BookStore.Application.Dtos;
using BookStore.Domain.Entities;
using BookStore.Domain.Errors;
using BookStore.Domain.Repositories;
using ErrorOr;

namespace BookStore.Application.Services;

public class BookAppService : IBookAppService
{
    private readonly IBookRepository _bookRepository;
    private readonly IMapper _mapper;

    public BookAppService(IBookRepository bookRepository, IMapper mapper)
    {
        _bookRepository = bookRepository;
        _mapper = mapper;
    }

    public async Task<ErrorOr<BookDto>> GetAsync(Guid id)
    {
        var book = await _bookRepository.GetAsync(id);
        if (book is null)
        {
            return BookErrors.NotFound;
        }

        return _mapper.Map<BookDto>(book);
    }

    public async Task<ErrorOr<List<BookDto>>> GetListAsync()
    {
        var books = await _bookRepository.GetListAsync();
        return _mapper.Map<List<BookDto>>(books);
    }

    public async Task<ErrorOr<BookDto>> CreateAsync(CreateBookDto input)
    {
        var result = Book.Create(input.Title, input.Author, input.ISBN, input.Price, input.Stock);
        if (result.IsError)
        {
            return result.Errors;
        }

        var book = result.Value;
        var createdBook = await _bookRepository.InsertAsync(book);
        return _mapper.Map<BookDto>(createdBook);
    }

    public async Task<ErrorOr<BookDto>> UpdateAsync(Guid id, UpdateBookDto input)
    {
        var existingBook = await _bookRepository.GetAsync(id);
        if (existingBook is null)
        {
            return BookErrors.NotFound;
        }

        var validationResult = existingBook.Update(input.Title, input.Author, input.Price, input.Stock);
        if (validationResult.IsError)
        {
            return validationResult.Errors;
        }

        var updatedBook = await _bookRepository.UpdateAsync(validationResult.Value);
        return _mapper.Map<Book, BookDto>(updatedBook);
    }

    public async Task<ErrorOr<Deleted>> DeleteAsync(Guid id)
    {
        var book = await _bookRepository.GetAsync(id);
        if (book is null)
        {
            return BookErrors.NotFound;
        }

        await _bookRepository.DeleteAsync(id);
        return Result.Deleted;
    }

    public async Task<ErrorOr<BookDto>> RefillStockAsync(Guid id, int quantity)
    {
        var existingBook = await _bookRepository.GetAsync(id);
        if (existingBook is null)
        {
            return BookErrors.NotFound;
        }

        var refillResult = existingBook.RefillStock(quantity);
        if (refillResult.IsError)
        {
            return refillResult.Errors;
        }

        var updatedBook = await _bookRepository.UpdateAsync(refillResult.Value);
        return _mapper.Map<Book, BookDto>(updatedBook);
    }

    public async Task<ErrorOr<BookDto>> VoteAsync(Guid id)
    {
        var existingBook = await _bookRepository.GetAsync(id);
        if (existingBook is null)
        {
            return BookErrors.NotFound;
        }

        var voteResult = existingBook.VoteAsync();
        if (voteResult.IsError)
        {
            return voteResult.Errors;
        }

        var updateBook = await _bookRepository.UpdateAsync(voteResult.Value);
        return _mapper.Map<Book, BookDto>(updateBook);
    }
}