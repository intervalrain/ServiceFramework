using System.Text.RegularExpressions;

namespace EmailSystem.Domain.Specifications;

public static class EmailSpecifications
{
    public static bool HasValidSubject(string subject)
    {
        return !string.IsNullOrWhiteSpace(subject) && subject.Length <= 200;
    }

    public static bool HasValidEmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var emailRegex = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
        return Regex.IsMatch(email, emailRegex);
    }

    public static bool HasValidContent(string htmlContent, string textContent)
    {
        return !string.IsNullOrWhiteSpace(htmlContent) || !string.IsNullOrWhiteSpace(textContent);
    }
}