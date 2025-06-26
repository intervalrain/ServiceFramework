using AutoMapper;
using AuthorSystem.Application.Dtos;
using AuthorSystem.Domain.Entities;

namespace AuthorSystem.Application.Mappings;

public class AuthorMappingProfile : Profile
{
    public AuthorMappingProfile()
    {
        CreateMap<Author, AuthorDto>();
        CreateMap<CreateAuthorDto, Author>();
        CreateMap<UpdateAuthorDto, Author>();
    }
}