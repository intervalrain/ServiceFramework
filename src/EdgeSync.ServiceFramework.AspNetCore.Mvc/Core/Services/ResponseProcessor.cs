using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.Data;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;

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
                // Get Error and ErrorMessage for failed response
                var errorProperty = responseDtoType.GetProperty("Error");
                var errorMessageProperty = responseDtoType.GetProperty("ErrorMessage");

                var error = errorProperty?.GetValue(responseDto);
                var errorMessage = errorMessageProperty?.GetValue(responseDto) as string ?? "Unknown error";

                // Extract audit info from response
                var reqSeqIdProp = responseDtoType.GetProperty("ReqSeqId");
                var rspSeqIdProp = responseDtoType.GetProperty("RspSeqId");
                var timestampProp = responseDtoType.GetProperty("Timestamp");
                var reqSeqId = reqSeqIdProp?.GetValue(responseDto)?.ToString() ?? "N/A";
                var rspSeqId = rspSeqIdProp?.GetValue(responseDto)?.ToString() ?? "N/A";
                var timestamp = timestampProp?.GetValue(responseDto)?.ToString() ?? "N/A";

                _logger.LogWarning("ResponseDto indicates failure: {ErrorMessage}. ReqSeqId: {ReqSeqId}, RspSeqId: {RspSeqId}, Timestamp: {Timestamp}",
                    errorMessage, reqSeqId, rspSeqId, timestamp);
                return new BadRequestObjectResult(new { error = errorMessage });
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
                    // Convert ErrorOr errors to a more friendly format
                    var errorMessage = GetErrorMessage(errors);
                    _logger.LogWarning("ErrorOr indicates failure: {ErrorMessage}", errorMessage);
                    return new BadRequestObjectResult(new { error = errorMessage });
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
}