using ErrorOr;

namespace EdgeSync.ServiceFramework.Utilities;

/// <summary>
/// Utility class for mapping exceptions to ErrorOr.Error objects
/// </summary>
public static class ExceptionToErrorMapper
{
    /// <summary>
    /// Maps an exception to an appropriate ErrorOr.Error with detailed information
    /// </summary>
    /// <param name="exception">The exception to map</param>
    /// <param name="defaultCode">Default error code if none can be determined</param>
    /// <param name="includeStackTrace">Whether to include stack trace in metadata</param>
    /// <returns>An Error object representing the exception</returns>
    public static Error MapToError(Exception exception, string? defaultCode = null, bool includeStackTrace = true)
    {
        var metadata = new Dictionary<string, object>
        {
            ["exceptionType"] = exception.GetType().Name,
            ["message"] = exception.Message,
        };

        if (includeStackTrace && !string.IsNullOrEmpty(exception.StackTrace))
        {
            metadata["stackTrace"] = exception.StackTrace;
        }

        // Include inner exception details if present
        if (exception.InnerException != null)
        {
            metadata["innerException"] = new Dictionary<string, object>
            {
                ["type"] = exception.InnerException.GetType().Name,
                ["message"] = exception.InnerException.Message,
            };
            
            if (includeStackTrace && !string.IsNullOrEmpty(exception.InnerException.StackTrace))
            {
                ((Dictionary<string, object>)metadata["innerException"])["stackTrace"] = exception.InnerException.StackTrace;
            }
        }

        // Create detailed description including exception type
        var description = $"{exception.GetType().Name}: {exception.Message}";

        return exception switch
        {
            ArgumentNullException => Error.Validation(
                code: defaultCode ?? "Argument.Null", 
                description: description,
                metadata: metadata),
            
            ArgumentException => Error.Validation(
                code: defaultCode ?? "Argument.Invalid",
                description: description,
                metadata: metadata),
            
            InvalidOperationException => Error.Failure(
                code: defaultCode ?? "Operation.Invalid",
                description: description,
                metadata: metadata),
            
            NotSupportedException => Error.Failure(
                code: defaultCode ?? "Operation.NotSupported",
                description: description,
                metadata: metadata),
            
            UnauthorizedAccessException => Error.Unauthorized(
                code: defaultCode ?? "Access.Unauthorized",
                description: description,
                metadata: metadata),
            
            TimeoutException => Error.Failure(
                code: defaultCode ?? "Operation.Timeout",
                description: description,
                metadata: metadata),
                
            _ => Error.Failure(
                code: defaultCode ?? "System.UnexpectedError",
                description: description,
                metadata: metadata)
        };
    }

    /// <summary>
    /// Maps an exception to an ErrorOr with the appropriate error
    /// </summary>
    /// <typeparam name="T">The expected return type</typeparam>
    /// <param name="exception">The exception to map</param>
    /// <param name="defaultCode">Default error code if none can be determined</param>
    /// <returns>An ErrorOr containing the mapped error</returns>
    public static ErrorOr<T> MapToErrorOr<T>(Exception exception, string? defaultCode = null)
    {
        var error = MapToError(exception, defaultCode);
        return error;
    }
}