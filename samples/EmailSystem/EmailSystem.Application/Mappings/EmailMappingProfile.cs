using AutoMapper;
using EmailSystem.Application.Dtos;
using EmailSystem.Domain.Entities;

namespace EmailSystem.Application.Mappings;

public class EmailMappingProfile : Profile
{
    public EmailMappingProfile()
    {
        CreateMap<Email, EmailDto>();
        CreateMap<SendEmailDto, Email>();
        CreateMap<EmailEventDto, SendEmailDto>();
    }
}