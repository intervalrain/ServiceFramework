using ErrorOr;

namespace EmailSystem.Domain.Errors;

public static class EmailErrors
{
    public static Error InvalidSubject => Error.Validation(
        "Email.InvalidSubject",
        "Subject cannot be empty and must be less than 200 characters");

    public static Error InvalidToEmail => Error.Validation(
        "Email.InvalidToEmail",
        "To email address is not valid");

    public static Error InvalidFromEmail => Error.Validation(
        "Email.InvalidFromEmail",
        "From email address is not valid");

    public static Error InvalidContent => Error.Validation(
        "Email.InvalidContent",
        "Email must have either HTML or text content");

    public static Error NotFound => Error.NotFound(
        "Email.NotFound",
        "Email not found");

    public static Error SendFailed => Error.Failure(
        "Email.SendFailed",
        "Failed to send email");
}