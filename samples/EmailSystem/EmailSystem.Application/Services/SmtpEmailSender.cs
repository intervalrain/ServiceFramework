using EmailSystem.Application.Contracts.Services;
using EmailSystem.Domain.Entities;
using EmailSystem.Domain.Errors;
using ErrorOr;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace EmailSystem.Application.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly IConfiguration _configuration;

    public SmtpEmailSender(ILogger<SmtpEmailSender> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<ErrorOr<bool>> SendEmailAsync(Email email)
    {
        try
        {
            var message = CreateMimeMessage(email);
            
            using var client = new SmtpClient();
            
            // Get SMTP configuration
            var smtpHost = _configuration["EmailSettings:SmtpHost"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var username = _configuration["EmailSettings:Username"] ?? "";
            var password = _configuration["EmailSettings:Password"] ?? "";

            _logger.LogInformation("Connecting to SMTP server {Host}:{Port}", smtpHost, smtpPort);
            
            await client.ConnectAsync(smtpHost, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                await client.AuthenticateAsync(username, password);
                _logger.LogInformation("Authenticated with SMTP server");
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully to {To} with subject: {Subject}", 
                email.To, email.Subject);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To} with subject: {Subject}", 
                email.To, email.Subject);
            
            return EmailErrors.SendFailed;
        }
    }

    private MimeMessage CreateMimeMessage(Email email)
    {
        var message = new MimeMessage();
        
        message.From.Add(new MailboxAddress("EmailSystem", email.From));
        message.To.Add(new MailboxAddress("Recipient", email.To));
        message.Subject = email.Subject;

        var bodyBuilder = new BodyBuilder();
        
        if (!string.IsNullOrEmpty(email.HtmlContent))
        {
            bodyBuilder.HtmlBody = email.HtmlContent;
        }
        
        if (!string.IsNullOrEmpty(email.TextContent))
        {
            bodyBuilder.TextBody = email.TextContent;
        }

        message.Body = bodyBuilder.ToMessageBody();
        
        return message;
    }
}