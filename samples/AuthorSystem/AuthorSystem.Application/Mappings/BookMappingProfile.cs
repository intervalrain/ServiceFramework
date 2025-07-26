using AutoMapper;
using AuthorSystem.Application.Protos;
using AuthorSystem.Domain.Entities;

namespace AuthorSystem.Application.Mappings;

public class BookMappingProfile : Profile
{
    public BookMappingProfile()
    {
        // Domain to Proto mappings
        CreateMap<Book, BookDto>()
            .ForMember(dest => dest.AuthorId, opt => opt.MapFrom(src => src.AuthorId))
            .ForMember(dest => dest.PublicationYear, opt => opt.MapFrom(src => src.PublicationYear))
            .ForMember(dest => dest.IsAvailable, opt => opt.MapFrom(src => src.IsAvailable));
        
        // Proto to Domain mappings
        CreateMap<CreateBookDto, Book>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsAvailable, opt => opt.MapFrom(src => true));
        
        CreateMap<UpdateBookDto, Book>()
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());
    }
}