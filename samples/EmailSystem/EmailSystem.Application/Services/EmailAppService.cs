using AutoMapper;
using EmailSystem.Application.Dtos;
using EmailSystem.Domain.Entities;
using EmailSystem.Domain.Errors;
using EmailSystem.Domain.Repositories;
using ErrorOr;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;
using Microsoft.Extensions.Logging;
using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.Abstractions.Attributes;

namespace EmailSystem.Application.Services;

public class EmailAppService : NatsService, IEmailAppService
{
    private readonly IEmailRepository _emailRepository;
    private readonly IMapper _mapper;

    private readonly IEmailSender _emailSender;

    public EmailAppService(ILogger<EmailAppService> logger, IEmailRepository emailRepository, IMapper mapper, IEmailSender emailSender)
        : base(logger)
    {
        _emailRepository = emailRepository;
        _mapper = mapper;
        _emailSender = emailSender;
    }

    /// <summary>
    /// Get email by ID
    /// </summary>
    /// <param name="id">The unique identifier of the email</param>
    /// <returns>Email details if found, otherwise error</returns>
    [Subject("get-email", "emailsys.emails.*.get")]
    public async Task<ErrorOr<EmailDto>> GetAsync(Guid id)
    {
        var email = await _emailRepository.GetByIdAsync(id);

        if (email is null)
            return EmailErrors.NotFound;

        return _mapper.Map<EmailDto>(email);
    }

    /// <summary>
    /// Get all emails
    /// </summary>
    /// <returns>List of all emails</returns>
    [Subject("get-emails", "emailsys.emails.get")]
    public async Task<ErrorOr<List<EmailDto>>> GetListAsync()
    {
        var emails = await _emailRepository.GetAllAsync();
        return _mapper.Map<List<EmailDto>>(emails);
    }

    /// <summary>
    /// Send an email
    /// </summary>
    /// <param name="input">The email information to send</param>
    /// <returns>Sent email details if successful, otherwise error</returns>
    [Subject("send-email", "emailsys.emails.send")]
    public async Task<ErrorOr<EmailDto>> SendEmailAsync(SendEmailDto input)
    {
        var createResult = Email.Create(
            input.Subject,
            input.To,
            input.From,
            input.HtmlContent,
            input.TextContent,
            input.Data);

        if (createResult.IsError)
            return createResult.Errors;

        var email = createResult.Value;

        // Save email to repository first
        var savedEmail = await _emailRepository.AddAsync(email);

        // Send email using SMTP
        var sendResult = await _emailSender.SendEmailAsync(email);
        if (sendResult.IsError)
        {
            // Mark as failed if sending fails
            email.MarkAsFailed();
            await _emailRepository.UpdateAsync(email);
            return sendResult.Errors;
        }

        // Mark as sent if successful
        var markResult = email.MarkAsSent();
        if (markResult.IsError)
            return EmailErrors.SendFailed;

        // Update the email status in repository
        await _emailRepository.UpdateAsync(email);
        
        return _mapper.Map<EmailDto>(email);
    }

    /// <summary>
    /// Process email event - converts subject and data to email format and sends
    /// </summary>
    /// <param name="input">The email event containing subject and data</param>
    /// <returns>Sent email details if successful, otherwise error</returns>
    [Subject("process-email-event", "emailsys.events.process")]
    public async Task<ErrorOr<EmailDto>> ProcessEmailEventAsync(EmailEventDto input)
    {
        // Convert subject and data to email format
        var emailContent = GenerateEmailContent(input.Subject, input.Data);
        
        var sendEmailDto = new SendEmailDto
        {
            Subject = input.Subject,
            To = GetRecipientFromData(input.Data),
            From = GetSenderFromData(input.Data),
            HtmlContent = emailContent.HtmlContent,
            TextContent = emailContent.TextContent,
            Data = input.Data
        };

        return await SendEmailAsync(sendEmailDto);
    }

    private (string HtmlContent, string TextContent) GenerateEmailContent(string subject, Dictionary<string, object> data)
    {
        var htmlContent = $"<html><body><h1>{subject}</h1>";
        var textContent = $"{subject}\n\n";

        foreach (var item in data)
        {
            htmlContent += $"<p><strong>{item.Key}:</strong> {item.Value}</p>";
            textContent += $"{item.Key}: {item.Value}\n";
        }

        htmlContent += "</body></html>";
        
        return (htmlContent, textContent);
    }

    private string GetRecipientFromData(Dictionary<string, object> data)
    {
        return data.TryGetValue("to", out var to) ? to.ToString() ?? "default@example.com" : "default@example.com";
    }

    private string GetSenderFromData(Dictionary<string, object> data)
    {
        return data.TryGetValue("from", out var from) ? from.ToString() ?? "noreply@example.com" : "noreply@example.com";
    }
}