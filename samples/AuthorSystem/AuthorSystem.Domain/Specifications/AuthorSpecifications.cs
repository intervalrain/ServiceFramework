using System.Text.RegularExpressions;

namespace AuthorSystem.Domain.Specifications;

public static class AuthorSpecifications
{
    public static bool HasValidName(string name) => 
        !string.IsNullOrWhiteSpace(name) && name.Length <= 100;
        
    public static bool HasValidEmail(string email) => 
        !string.IsNullOrWhiteSpace(email) && 
        Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        
    public static bool HasValidBiography(string biography) => 
        biography.Length <= 1000;
        
    public static bool HasValidBirthDate(DateTime birthDate) => 
        birthDate <= DateTime.UtcNow.AddYears(-16) && 
        birthDate >= DateTime.UtcNow.AddYears(-150);
}