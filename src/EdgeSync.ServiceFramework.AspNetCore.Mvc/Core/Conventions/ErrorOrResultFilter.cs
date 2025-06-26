using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ErrorOr;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Conventions;

public class ErrorOrResultFilter : ActionFilterAttribute
{
    public override void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is ObjectResult objectResult && objectResult.Value != null)
        {
            var resultType = objectResult.Value.GetType();
            
            // Check if the result is ErrorOr<T>
            if (IsErrorOrType(resultType))
            {
                var errorOrResult = objectResult.Value;
                var unwrappedResult = UnwrapErrorOr(errorOrResult);
                context.Result = unwrappedResult;
            }
        }
        
        base.OnActionExecuted(context);
    }

    private static bool IsErrorOrType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ErrorOr<>);
    }

    private static IActionResult UnwrapErrorOr(object errorOrResult)
    {
        var type = errorOrResult.GetType();
        var isErrorProperty = type.GetProperty("IsError");
        var valueProperty = type.GetProperty("Value");
        var errorsProperty = type.GetProperty("Errors");

        if (isErrorProperty != null && valueProperty != null && errorsProperty != null)
        {
            var isError = (bool)isErrorProperty.GetValue(errorOrResult)!;
            
            if (isError)
            {
                var errors = errorsProperty.GetValue(errorOrResult) as IEnumerable<Error>;
                var firstError = errors?.FirstOrDefault();
                
                return firstError?.Type switch
                {
                    ErrorType.NotFound => new NotFoundObjectResult(new { error = GetErrorMessage(firstError) }),
                    ErrorType.Validation => new BadRequestObjectResult(new { error = GetErrorMessage(firstError) }),
                    ErrorType.Conflict => new ConflictObjectResult(new { error = GetErrorMessage(firstError) }),
                    ErrorType.Unauthorized => new UnauthorizedObjectResult(new { error = GetErrorMessage(firstError) }),
                    ErrorType.Forbidden => new ObjectResult(new { error = GetErrorMessage(firstError) }) { StatusCode = 403 },
                    _ => new BadRequestObjectResult(new { error = GetErrorMessage(firstError) ?? "Unknown error" })
                };
            }
            else
            {
                var value = valueProperty.GetValue(errorOrResult);
                return new OkObjectResult(value);
            }
        }

        return new BadRequestObjectResult(new { error = "Invalid result type" });
    }

    private static string? GetErrorMessage(Error? error)
    {
        if (error == null) return null;
        
        // Try different possible property names for the error message
        var errorType = error.GetType();
        
        // Try Description property first
        var descriptionProperty = errorType.GetProperty("Description");
        if (descriptionProperty != null)
        {
            return descriptionProperty.GetValue(error)?.ToString();
        }
        
        // Try Message property
        var messageProperty = errorType.GetProperty("Message");
        if (messageProperty != null)
        {
            return messageProperty.GetValue(error)?.ToString();
        }
        
        // Try Code property as fallback
        var codeProperty = errorType.GetProperty("Code");
        if (codeProperty != null)
        {
            return codeProperty.GetValue(error)?.ToString();
        }
        
        // Fallback to ToString()
        return error.ToString();
    }
}