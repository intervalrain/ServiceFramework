using ErrorOr;

namespace AuthorSystem.Domain.Errors;

public static class BookErrors
{
    public static Error NotFound => Error.NotFound("Book.NotFound", "Book not found");
    public static Error InvalidTitle => Error.Validation("Book.InvalidTitle", "Title is required and must not exceed 200 characters");
    public static Error InvalidIsbn => Error.Validation("Book.InvalidIsbn", "ISBN is required and must be valid");
    public static Error InvalidPrice => Error.Validation("Book.InvalidPrice", "Price must be greater than 0");
    public static Error InvalidPublicationYear => Error.Validation("Book.InvalidPublicationYear", "Publication year must be valid");
    public static Error IsbnAlreadyExists => Error.Conflict("Book.IsbnAlreadyExists", "Book with this ISBN already exists");
}