using ErrorOr;

namespace BookStore.Domain.Errors;

public static class BookErrors
{
    public static Error NotFound => Error.NotFound("Book.NotFound", "Book not found");
    public static Error InvalidTitle => Error.Validation("Book.InvalidTitle", "Title is required and must not exceed 200 characters");
    public static Error InvalidAuthor => Error.Validation("Book.InvalidAuthor", "Author is required and must not exceed 100 characters");
    public static Error InvalidISBN => Error.Validation("Book.InvalidISBN", "Invalid ISBN format. Must be 10 or 13 digits");
    public static Error InvalidPrice => Error.Validation("Book.InvalidPrice", "Price must be greater than 0 and less than 999999.99");
    public static Error InvalidStock => Error.Validation("Book.InvalidStock", "Stock must be 0 or greater");
    public static Error InsufficientStock => Error.Conflict("Book.InsufficientStock", "Insufficient stock available");
}