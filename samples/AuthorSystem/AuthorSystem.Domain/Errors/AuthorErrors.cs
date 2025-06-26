using ErrorOr;

namespace AuthorSystem.Domain.Errors;

public static class AuthorErrors
{
    public static Error NotFound => Error.NotFound("Author.NotFound", "Author not found");
    public static Error InvalidName => Error.Validation("Author.InvalidName", "Name is required and must not exceed 100 characters");
    public static Error InvalidEmail => Error.Validation("Author.InvalidEmail", "Invalid email format");
    public static Error InvalidBiography => Error.Validation("Author.InvalidBiography", "Biography must not exceed 1000 characters");
    public static Error InvalidBirthDate => Error.Validation("Author.InvalidBirthDate", "Birth date must be valid and author must be at least 16 years old");
    public static Error EmailAlreadyExists => Error.Conflict("Author.EmailAlreadyExists", "Email already exists");
}