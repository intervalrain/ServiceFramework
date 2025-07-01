using AuthorSystem.Domain.Errors;
using AuthorSystem.Domain.Specifications;

using ErrorOr;

namespace AuthorSystem.Domain.Entities;

public class Author
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Biography { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int Vote { get; private set; }

    private Author(Guid id, string name, string email, string biography, DateTime birthDate)
    {
        Id = id;
        Name = name;
        Email = email;
        Biography = biography;
        BirthDate = birthDate;
        CreatedAt = DateTime.UtcNow;
        Vote = 0;
    }

    public static ErrorOr<Author> Create(string name, string email, string biography, DateTime birthDate)
    {
        var errors = new List<Error>();

        if (!AuthorSpecifications.HasValidName(name))
            errors.Add(AuthorErrors.InvalidName);

        if (!AuthorSpecifications.HasValidEmail(email))
            errors.Add(AuthorErrors.InvalidEmail);

        if (!AuthorSpecifications.HasValidBiography(biography))
            errors.Add(AuthorErrors.InvalidBiography);

        if (!AuthorSpecifications.HasValidBirthDate(birthDate))
            errors.Add(AuthorErrors.InvalidBirthDate);

        return errors.Count > 0 ? errors : new Author(Guid.NewGuid(), name, email, biography, birthDate);
    }

    public ErrorOr<Author> Update(string name, string email, string biography, DateTime birthDate)
    {
        var errors = new List<Error>();

        if (!AuthorSpecifications.HasValidName(name))
            errors.Add(AuthorErrors.InvalidName);

        if (!AuthorSpecifications.HasValidEmail(email))
            errors.Add(AuthorErrors.InvalidEmail);

        if (!AuthorSpecifications.HasValidBiography(biography))
            errors.Add(AuthorErrors.InvalidBiography);

        if (!AuthorSpecifications.HasValidBirthDate(birthDate))
            errors.Add(AuthorErrors.InvalidBirthDate);

        if (errors.Count == 0)
        {
            Name = name;
            Email = email;
            Biography = biography;
            BirthDate = birthDate;
            UpdatedAt = DateTime.UtcNow;
        }

        return errors.Count > 0 ? errors : this;
    }

    public ErrorOr<bool> AddVote()
    {
        if (Vote == int.MaxValue) return false;

        Vote++;
        return true;
    }
}