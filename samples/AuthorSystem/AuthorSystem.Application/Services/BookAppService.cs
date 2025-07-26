using AuthorSystem.Application.Protos;
using AuthorSystem.Domain.Entities;
using AuthorSystem.Domain.Errors;
using AuthorSystem.Domain.Repositories;
using AutoMapper;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;
using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.Abstractions.Attributes;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace AuthorSystem.Application.Services;

[Channel("broker")]
[ServiceInfo(ServiceName = "Book", ServiceVersion = "1.0.0", QueueGroup = "book-q")]
public class BookAppService : NatsService, IBookAppService
{
    private readonly IBookRepository _bookRepository;
    private readonly IMapper _mapper;

    public BookAppService(ILogger<BookAppService> logger, IBookRepository bookRepository, IMapper mapper) : base(logger)
    {
        _bookRepository = bookRepository;
        _mapper = mapper;
    }

    [Subject("get-book", "booksys.books.*.get")]
    public async Task<ErrorOr<BookDto>> GetAsync(GetBookDto input)
    {
        var book = await _bookRepository.GetAsync(input.Id);
        
        if (book == null)
        {
            return BookErrors.NotFound;
        }

        return _mapper.Map<BookDto>(book);
    }

    [Subject("create-book", "booksys.books.post")]
    public async Task<ErrorOr<BookDto>> CreateAsync(CreateBookDto input)
    {
        var existingBook = await _bookRepository.GetByIsbnAsync(input.Isbn);
        if (existingBook != null)
        {
            return BookErrors.IsbnAlreadyExists;
        }

        var newBook = Book.Create(
            title: input.Title,
            authorId: input.AuthorId,
            isbn: input.Isbn,
            description: input.Description,
            publicationYear: input.PublicationYear,
            price: input.Price
        );

        var createdBook = await _bookRepository.InsertAsync(newBook);

        return _mapper.Map<BookDto>(createdBook);
    }

    [Subject("update-book", "booksys.books.*.put")]
    public async Task<ErrorOr<BookDto>> UpdateAsync(UpdateBookDto input)
    {
        var book = await _bookRepository.GetAsync(input.Id);
        
        if (book == null)
        {
            return BookErrors.NotFound;
        }

        // Check if another book has the same ISBN
        var bookWithSameIsbn = await _bookRepository.GetByIsbnAsync(input.Isbn);
        if (bookWithSameIsbn != null && bookWithSameIsbn.Id != input.Id)
        {
            return BookErrors.IsbnAlreadyExists;
        }

        _mapper.Map(input, book);
        book.UpdatedAt = DateTime.UtcNow;

        var updatedBook = await _bookRepository.UpdateAsync(book);

        return _mapper.Map<BookDto>(updatedBook);
    }

    [Subject("delete-book", "booksys.books.*.delete")]
    public async Task<ErrorOr<Deleted>> DeleteAsync(GetBookDto input)
    {
        var book = await _bookRepository.GetAsync(input.Id);
        
        if (book == null)
        {
            return BookErrors.NotFound;
        }

        await _bookRepository.DeleteAsync(input.Id);
        
        return Result.Deleted;
    }

    [Subject("get-books", "booksys.books.get")]
    public async Task<ErrorOr<List<BookDto>>> GetListAsync()
    {
        var books = await _bookRepository.GetListAsync();
        return _mapper.Map<List<BookDto>>(books);
    }

}