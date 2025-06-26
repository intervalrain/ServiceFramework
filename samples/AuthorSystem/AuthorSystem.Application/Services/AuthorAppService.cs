using AutoMapper;
using AuthorSystem.Application.Dtos;
using AuthorSystem.Domain.Entities;
using AuthorSystem.Domain.Errors;
using AuthorSystem.Domain.Repositories;
using ErrorOr;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;
using Microsoft.Extensions.Logging;
using EdgeSync.ServiceFramework.Attributes;

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
}