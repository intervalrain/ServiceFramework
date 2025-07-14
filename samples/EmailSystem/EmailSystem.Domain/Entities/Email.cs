using EmailSystem.Domain.Errors;
using EmailSystem.Domain.Specifications;

using ErrorOr;

namespace EmailSystem.Domain.Entities;

public class Email
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public string TextContent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public EmailStatus Status { get; private set; }
    public Dictionary<string, object> Data { get; set; } = new();

    private Email(Guid id, string subject, string to, string from, string htmlContent, string textContent, Dictionary<string, object> data)
    {
        Id = id;
        Subject = subject;
        To = to;
        From = from;
        HtmlContent = htmlContent;
        TextContent = textContent;
        Data = data;
        CreatedAt = DateTime.UtcNow;
        Status = EmailStatus.Pending;
    }

    public static ErrorOr<Email> Create(string subject, string to, string from, string htmlContent, string textContent, Dictionary<string, object> data)
    {
        var errors = new List<Error>();

        if (!EmailSpecifications.HasValidSubject(subject))
            errors.Add(EmailErrors.InvalidSubject);

        if (!EmailSpecifications.HasValidEmailAddress(to))
            errors.Add(EmailErrors.InvalidToEmail);

        if (!EmailSpecifications.HasValidEmailAddress(from))
            errors.Add(EmailErrors.InvalidFromEmail);

        if (!EmailSpecifications.HasValidContent(htmlContent, textContent))
            errors.Add(EmailErrors.InvalidContent);

        return errors.Count > 0 ? errors : new Email(Guid.NewGuid(), subject, to, from, htmlContent, textContent, data);
    }

    public ErrorOr<bool> MarkAsSent()
    {
        if (Status == EmailStatus.Sent) return false;

        Status = EmailStatus.Sent;
        SentAt = DateTime.UtcNow;
        return true;
    }

    public ErrorOr<bool> MarkAsFailed()
    {
        if (Status == EmailStatus.Failed) return false;

        Status = EmailStatus.Failed;
        return true;
    }
}

public enum EmailStatus
{
    Pending,
    Sent,
    Failed
}