using ErrorOr;

namespace BookStore.Nats.Client.Errors;

public static class CommunicationErrors
{
    public static Error ConnectionFailed => Error.Failure(
        "Communication.ConnectionFailed", 
        "Failed to connect to NATS server");
        
    public static Error RequestTimeout => Error.Failure(
        "Communication.RequestTimeout", 
        "Request timeout while communicating with NATS server");
        
    public static Error InvalidResponse => Error.Failure(
        "Communication.InvalidResponse", 
        "Received invalid response from NATS server");
        
    public static Error ServiceUnavailable => Error.Failure(
        "Communication.ServiceUnavailable", 
        "Book service is currently unavailable");
        
    public static Error SerializationFailed => Error.Failure(
        "Communication.SerializationFailed", 
        "Failed to serialize/deserialize message");
        
    public static Error UnexpectedError => Error.Failure(
        "Communication.UnexpectedError", 
        "An unexpected error occurred during communication");
}