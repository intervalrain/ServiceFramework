using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.Data;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EdgeSync.ServiceFramework.Core.Services;

/// <summary>
/// Implementation of response processor for handling different response types
/// </summary>
public class ResponseProcessor : IResponseProcessor
{
    private readonly ILogger<ResponseProcessor> _logger;

    public ResponseProcessor(ILogger<ResponseProcessor> logger)
    {
        _logger = logger;
    }

    public IActionResult ProcessResponse(object? response, bool useExceptionHandler)
    {
        if (useExceptionHandler)
        {
            // UseExceptionHandler=true: Unwrap and extract data
            return UnwrapResponse(response);
        }
        else
        {
            // UseExceptionHandler=false: Keep full model but return appropriate status code
            return GetResponseWithStatus(response);
        }
    }

    public IActionResult GetResponseWithStatus(object? response)
    {
        if (response == null)
        {
            return new StatusCodeResult(204); // No Content
        }

        var responseType = response.GetType();

        // Handle ResponseDto<T> - check IsSuccess but return full model
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(ResponseDto<>))
        {
            var isSuccessProperty = responseType.GetProperty("IsSuccess");
            var isSuccess = (bool)(isSuccessProperty?.GetValue(response) ?? false);

            if (isSuccess)
            {
                return new OkObjectResult(response); // 200 with full ResponseDto
            }
            else
            {
                return new BadRequestObjectResult(response); // 400 with full ResponseDto (includes error info)
            }
        }

        // Handle ErrorOr<T> - check IsError but return full model
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(ErrorOr<>))
        {
            var isErrorProperty = responseType.GetProperty("IsError");
            var isError = (bool)(isErrorProperty?.GetValue(response) ?? true);

            if (!isError)
            {
                return new OkObjectResult(response); // 200 with full ErrorOr (success case)
            }
            else
            {
                return new BadRequestObjectResult(response); // 400 with full ErrorOr (includes error info)
            }
        }

        // For non-wrapped responses, assume success
        return new OkObjectResult(response);
    }

    public IActionResult UnwrapResponse(object? response)
    {
        if (response == null)
        {
            return new OkResult(); // 204 No Content equivalent
        }

        // Handle JsonElement case
        if (response is JsonElement jsonElement)
        {
            return UnwrapJsonElement(jsonElement);
        }

        var responseType = response.GetType();

        // Handle ResponseDto<T>
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(ResponseDto<>))
        {
            return UnwrapResponseDto(response, responseType);
        }

        // Handle ErrorOr<T>
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(ErrorOr<>))
        {
            return UnwrapErrorOr(response, responseType);
        }

        // For non-wrapped responses, return as-is
        return new OkObjectResult(response);
    }

    private IActionResult UnwrapResponseDto(object responseDto, Type responseDtoType)
    {
        try
        {
            // Get IsSuccess property
            var isSuccessProperty = responseDtoType.GetProperty("IsSuccess");
            var isSuccess = (bool)(isSuccessProperty?.GetValue(responseDto) ?? false);

            if (isSuccess)
            {
                // Get Data property for successful response
                var dataProperty = responseDtoType.GetProperty("Data");
                var data = dataProperty?.GetValue(responseDto);
                return new OkObjectResult(data);
            }
            else
            {
                // Get Message and Errors for failed response
                var messageProperty = responseDtoType.GetProperty("Message");
                var errorsProperty = responseDtoType.GetProperty("Errors");
                var statusCodeProperty = responseDtoType.GetProperty("StatusCode");

                var errorMessage = messageProperty?.GetValue(responseDto) as string ?? "Unknown error";
                var errors = errorsProperty?.GetValue(responseDto);
                var statusCode = statusCodeProperty?.GetValue(responseDto) as int? ?? 400;

                // Extract audit info from response
                var reqSeqIdProp = responseDtoType.GetProperty("ReqSeqId");
                var rspSeqIdProp = responseDtoType.GetProperty("RspSeqId");
                var timestampProp = responseDtoType.GetProperty("Timestamp");
                var reqSeqId = reqSeqIdProp?.GetValue(responseDto)?.ToString() ?? "N/A";
                var rspSeqId = rspSeqIdProp?.GetValue(responseDto)?.ToString() ?? "N/A";
                var timestamp = timestampProp?.GetValue(responseDto)?.ToString() ?? "N/A";

                _logger.LogWarning("ResponseDto indicates failure: {ErrorMessage}. StatusCode: {StatusCode}, ReqSeqId: {ReqSeqId}, RspSeqId: {RspSeqId}, Timestamp: {Timestamp}",
                    errorMessage, statusCode, reqSeqId, rspSeqId, timestamp);
                
                // Create error response
                object errorResponse = errors != null && errors is System.Collections.IEnumerable 
                    ? new { error = errorMessage, details = errors }
                    : new { error = errorMessage };
                
                // Return appropriate status code
                return statusCode switch
                {
                    404 => new NotFoundObjectResult(errorResponse),
                    401 => new UnauthorizedObjectResult(errorResponse),
                    403 => new ObjectResult(errorResponse) { StatusCode = 403 },
                    409 => new ConflictObjectResult(errorResponse),
                    _ => new BadRequestObjectResult(errorResponse)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unwrapping ResponseDto");
            return new StatusCodeResult(500);
        }
    }

    private IActionResult UnwrapErrorOr(object errorOr, Type errorOrType)
    {
        try
        {
            // Get IsError property
            var isErrorProperty = errorOrType.GetProperty("IsError");
            var isError = (bool)(isErrorProperty?.GetValue(errorOr) ?? true);

            if (!isError)
            {
                // Get Value property for successful response
                var valueProperty = errorOrType.GetProperty("Value");
                var value = valueProperty?.GetValue(errorOr);
                return new OkObjectResult(value);
            }
            else
            {
                // Get Errors property for failed response
                var errorsProperty = errorOrType.GetProperty("Errors");
                var errors = errorsProperty?.GetValue(errorOr);

                if (errors != null)
                {
                    // Get the first error to determine the type
                    var firstErrorProperty = errorOrType.GetProperty("FirstError");
                    var firstError = (Error)firstErrorProperty?.GetValue(errorOr)!;
                    if (firstError != null)
                    {
                        var errorMessage = GetErrorDescription(firstError);
                        var errorType = GetErrorType(firstError);
                        
                        _logger.LogWarning("ErrorOr indicates failure: {ErrorMessage}, Type: {ErrorType}", errorMessage, errorType);
                        
                        // Return appropriate status code based on error type
                        return errorType switch
                        {
                            ErrorType.NotFound => new NotFoundObjectResult(new { error = errorMessage }),
                            ErrorType.Validation => new BadRequestObjectResult(new { error = errorMessage }),
                            ErrorType.Conflict => new ConflictObjectResult(new { error = errorMessage }),
                            ErrorType.Unauthorized => new UnauthorizedObjectResult(new { error = errorMessage }),
                            ErrorType.Forbidden => new ObjectResult(new { error = errorMessage }) { StatusCode = 403 },
                            _ => new BadRequestObjectResult(new { error = errorMessage })
                        };
                    }
                    else
                    {
                        var errorMessage = GetErrorMessage(errors);
                        return new BadRequestObjectResult(new { error = errorMessage });
                    }
                }
                else
                {
                    return new BadRequestObjectResult(new { error = "Unknown error occurred" });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unwrapping ErrorOr");
            return new StatusCodeResult(500);
        }
    }    

    private string GetErrorDescription(Error error)
    {
        try
        {
            // Try Description property first
            var descriptionProperty = error.GetType().GetProperty("Description");
            if (descriptionProperty != null)
            {
                var description = descriptionProperty.GetValue(error)?.ToString();
                if (!string.IsNullOrEmpty(description))
                    return description;
            }
            
            // Try Message property
            var messageProperty = error.GetType().GetProperty("Message");
            if (messageProperty != null)
            {
                var message = messageProperty.GetValue(error)?.ToString();
                if (!string.IsNullOrEmpty(message))
                    return message;
            }
            
            // Try Code property as fallback
            var codeProperty = error.GetType().GetProperty("Code");
            if (codeProperty != null)
            {
                var code = codeProperty.GetValue(error)?.ToString();
                if (!string.IsNullOrEmpty(code))
                    return code;
            }
            
            // Fallback to ToString()
            return error.ToString();
        }
        catch
        {
            return "Unknown error";
        }
    }

    private ErrorType GetErrorType(Error error)
    {
        try
        {
            var typeProperty = error.GetType().GetProperty("Type");
            if (typeProperty != null)
            {
                var errorType = typeProperty.GetValue(error);
                if (errorType is ErrorType type)
                {
                    return type;
                }
            }
            
            // Default to validation error
            return ErrorType.Validation;
        }
        catch
        {
            return ErrorType.Validation;
        }
    }

    private string GetErrorMessage(object errors)
    {
        try
        {
            // ErrorOr.Errors is typically IReadOnlyList<Error>
            if (errors is System.Collections.IEnumerable enumerable)
            {
                var errorMessages = new List<string>();
                foreach (var error in enumerable)
                {
                    if (error != null)
                    {
                        // Try to get Description property from Error
                        var descriptionProperty = error.GetType().GetProperty("Description");
                        var description = descriptionProperty?.GetValue(error) as string;
                        if (!string.IsNullOrEmpty(description))
                        {
                            errorMessages.Add(description);
                        }
                        else
                        {
                            errorMessages.Add(error.ToString() ?? "Unknown error");
                        }
                    }
                }
                return string.Join("; ", errorMessages);
            }

            return errors.ToString() ?? "Unknown error";
        }
        catch
        {
            return "Error occurred while processing error messages";
        }
    }

    private IActionResult UnwrapJsonElement(JsonElement jsonElement)
    {
        try
        {
            // Check if JsonElement represents ResponseDto<T>
            if (jsonElement.ValueKind == JsonValueKind.Object && 
                jsonElement.TryGetProperty("IsSuccess", out var isSuccessElement))
            {
                var isSuccess = isSuccessElement.GetBoolean();
                
                if (isSuccess)
                {
                    // Extract Data property for successful ResponseDto
                    if (jsonElement.TryGetProperty("Data", out var dataElement))
                    {
                        return new OkObjectResult(ConvertJsonElementToObject(dataElement));
                    }
                    return new OkResult();
                }
                else
                {
                    // Handle failed ResponseDto
                    var errorMessage = jsonElement.TryGetProperty("Message", out var messageElement) 
                        ? messageElement.GetString() ?? "Unknown error"
                        : "Unknown error";
                        
                    var statusCode = jsonElement.TryGetProperty("StatusCode", out var statusCodeElement) 
                        ? statusCodeElement.GetInt32() 
                        : 400;

                    // Log warning with audit info
                    var reqSeqId = jsonElement.TryGetProperty("ReqSeqId", out var reqSeqIdElement) 
                        ? reqSeqIdElement.GetString() ?? "N/A" 
                        : "N/A";
                    var rspSeqId = jsonElement.TryGetProperty("RspSeqId", out var rspSeqIdElement) 
                        ? rspSeqIdElement.GetString() ?? "N/A" 
                        : "N/A";
                    var timestamp = jsonElement.TryGetProperty("Timestamp", out var timestampElement) 
                        ? timestampElement.GetString() ?? "N/A" 
                        : "N/A";

                    _logger.LogWarning("ResponseDto indicates failure: {ErrorMessage}. StatusCode: {StatusCode}, ReqSeqId: {ReqSeqId}, RspSeqId: {RspSeqId}, Timestamp: {Timestamp}",
                        errorMessage, statusCode, reqSeqId, rspSeqId, timestamp);

                    // Create error response
                    object errorResponse = jsonElement.TryGetProperty("Errors", out var errorsElement) && errorsElement.ValueKind != JsonValueKind.Null
                        ? new { error = errorMessage, details = ConvertJsonElementToObject(errorsElement) }
                        : new { error = errorMessage };

                    // Return appropriate status code
                    return statusCode switch
                    {
                        404 => new NotFoundObjectResult(errorResponse),
                        401 => new UnauthorizedObjectResult(errorResponse),
                        403 => new ObjectResult(errorResponse) { StatusCode = 403 },
                        409 => new ConflictObjectResult(errorResponse),
                        _ => new BadRequestObjectResult(errorResponse)
                    };
                }
            }
            
            // Check if JsonElement represents ErrorOr<T>
            if (jsonElement.ValueKind == JsonValueKind.Object && 
                jsonElement.TryGetProperty("IsError", out var isErrorElement))
            {
                var isError = isErrorElement.GetBoolean();
                
                if (!isError)
                {
                    // Extract Value property for successful ErrorOr
                    if (jsonElement.TryGetProperty("Value", out var valueElement))
                    {
                        return new OkObjectResult(ConvertJsonElementToObject(valueElement));
                    }
                    return new OkResult();
                }
                else
                {
                    // Handle failed ErrorOr - extract first error
                    if (jsonElement.TryGetProperty("Errors", out var errorsElement) && 
                        errorsElement.ValueKind == JsonValueKind.Array && 
                        errorsElement.GetArrayLength() > 0)
                    {
                        var firstError = errorsElement[0];
                        
                        var errorMessage = firstError.TryGetProperty("Description", out var descElement) 
                            ? descElement.GetString() ?? "Unknown error"
                            : firstError.TryGetProperty("Message", out var msgElement)
                                ? msgElement.GetString() ?? "Unknown error"
                                : "Unknown error";
                        
                        // Try to get error type for status code mapping
                        var errorType = ErrorType.Validation; // default
                        if (firstError.TryGetProperty("Type", out var typeElement) && 
                            typeElement.ValueKind == JsonValueKind.Number)
                        {
                            var typeValue = typeElement.GetInt32();
                            if (Enum.IsDefined(typeof(ErrorType), typeValue))
                            {
                                errorType = (ErrorType)typeValue;
                            }
                        }

                        _logger.LogWarning("ErrorOr indicates failure: {ErrorMessage}, Type: {ErrorType}", errorMessage, errorType);

                        // Return appropriate status code based on error type
                        return errorType switch
                        {
                            ErrorType.NotFound => new NotFoundObjectResult(new { error = errorMessage }),
                            ErrorType.Validation => new BadRequestObjectResult(new { error = errorMessage }),
                            ErrorType.Conflict => new ConflictObjectResult(new { error = errorMessage }),
                            ErrorType.Unauthorized => new UnauthorizedObjectResult(new { error = errorMessage }),
                            ErrorType.Forbidden => new ObjectResult(new { error = errorMessage }) { StatusCode = 403 },
                            _ => new BadRequestObjectResult(new { error = errorMessage })
                        };
                    }
                    else
                    {
                        return new BadRequestObjectResult(new { error = "Unknown error occurred" });
                    }
                }
            }

            // For non-wrapped JsonElement responses, return as-is
            return new OkObjectResult(ConvertJsonElementToObject(jsonElement));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unwrapping JsonElement");
            return new StatusCodeResult(500);
        }
    }

    private object? ConvertJsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.Deserialize<Dictionary<string, object?>>(),
            JsonValueKind.Array => element.Deserialize<object[]>(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }
}