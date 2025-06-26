using AutoMapper;
using AuthorSystem.Application.Dtos;
using AuthorSystem.Domain.Entities;
using AuthorSystem.Domain.Errors;
using AuthorSystem.Domain.Repositories;
using ErrorOr;

namespace AuthorSystem.Application.Services;

public class AuthorAppService : IAuthorAppService
{
    private readonly IAuthorRepository _authorRepository;
    private readonly IMapper _mapper;

    public AuthorAppService(IAuthorRepository authorRepository, IMapper mapper)
    {
        _authorRepository = authorRepository;
        _mapper = mapper;
    }

    public async Task<ErrorOr<AuthorDto>> GetAsync(Guid id)
    {
        var author = await _authorRepository.GetAsync(id);
        if (author is null)
        {
            return AuthorErrors.NotFound;
        }

        return _mapper.Map<AuthorDto>(author);
    }

    public async Task<ErrorOr<List<AuthorDto>>> GetListAsync()
    {
        var authors = await _authorRepository.GetListAsync();
        return _mapper.Map<List<AuthorDto>>(authors);
    }

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
}