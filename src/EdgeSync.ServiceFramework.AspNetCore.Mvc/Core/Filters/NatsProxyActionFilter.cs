using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using EdgeSync.ServiceFramework.Abstractions;
using EdgeSync.ServiceFramework.Data;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using System.Reflection;
using ErrorOr;

namespace EdgeSync.ServiceFramework.Core.Filters;

/// <summary>
/// Action filter that intercepts requests and routes them through NATS instead of direct service calls
/// </summary>
public class NatsProxyActionFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NatsProxyActionFilter> _logger;
    private readonly INatsConnectionFactory _connectionFactory;

    public NatsProxyActionFilter(
        IServiceProvider serviceProvider,
        ILogger<NatsProxyActionFilter> logger,
        INatsConnectionFactory connectionFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _connectionFactory = connectionFactory;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        _logger.LogDebug("NatsProxyActionFilter: Starting execution for action {ActionName}",
            context.ActionDescriptor.DisplayName);

        object? wrappedRequest = null;

        try
        {
            var metadata = new ActionContextMetadata(context.ActionDescriptor);

            if (string.IsNullOrEmpty(metadata.Subject))
            {
                _logger.LogError("Subject not found in action metadata for {ServiceName}.{MethodName}", metadata.ServiceName, metadata.MethodName);
                context.Result = new BadRequestObjectResult("Subject not found in action metadata");
                return;
            }

            // Get connection based on channel name
            var connection = await GetConnectionAsync(metadata.ChannelName);
            if (connection == null)
            {
                _logger.LogError("Unable to connect to NATS for channel: {ChannelName}", metadata.ChannelName ?? "default");
                context.Result = new ObjectResult($"Unable to connect to NATS for channel: {metadata.ChannelName ?? "default"}")
                {
                    StatusCode = 500
                };
                return;
            }

            // Extract request data from action parameters
            var requestData = ExtractRequestData(context);
            wrappedRequest = WrapRequestWithAudit(requestData, context.HttpContext);

            // Check if this is a parameterless method that got wrapped with audit info
            var originalMethod = context.ActionDescriptor.Properties["OriginalMethod"] as MethodInfo;
            var isOriginallyParameterless = originalMethod?.GetParameters().Length == 0;

            // Handle different convention modes
            switch (metadata.ConventionMode)
            {
                case ConventionMode.RequestResponse:
                    var response = await SendNatsRequest(connection, metadata.Subject, wrappedRequest, isOriginallyParameterless, metadata.ChannelName);
                    _logger.LogDebug("NatsProxyActionFilter: NATS Request/Response completed. Subject: {Subject}, Response: {Response}",
                        metadata.Subject, response?.ToString() ?? "null");

                    // Log successful request with audit info for monitoring
                    var requestAuditInfo = ExtractAuditInfo(wrappedRequest);
                    var responseAuditInfo = ExtractResponseAuditInfo(response);
                    if (responseAuditInfo != null)
                    {
                        _logger.LogInformation("NATS request completed successfully. Service: {ServiceName}.{MethodName}, Subject: {Subject}, ReqSeqId: {ReqSeqId}, RspSeqId: {RspSeqId}, RequestTimestamp: {RequestTimestamp}, ResponseTimestamp: {ResponseTimestamp}",
                            metadata.ServiceName, metadata.MethodName, metadata.Subject,
                            requestAuditInfo?.ReqSeqId ?? responseAuditInfo.Value.ReqSeqId,
                            responseAuditInfo.Value.RspSeqId,
                            requestAuditInfo?.Timestamp ?? "N/A",
                            responseAuditInfo.Value.Timestamp);
                    }

                    // Handle response based on UseExceptionHandler setting
                    var autoConventionOptions = _serviceProvider.GetService<IOptions<AutoConventionOptions>>()?.Value;
                    if (autoConventionOptions?.UseExceptionHandler == true)
                    {
                        // UseExceptionHandler=true: Unwrap and extract data
                        var processedResponse = UnwrapResponse(response);
                        context.Result = processedResponse;
                    }
                    else
                    {
                        // UseExceptionHandler=false: Keep full model but return appropriate status code
                        var statusResponse = GetResponseWithStatus(response);
                        context.Result = statusResponse;
                    }
                    break;

                case ConventionMode.PubSubPushClassic:
                case ConventionMode.PubSubPushJetStream:
                    await PublishNatsMessage(connection, metadata.Subject, wrappedRequest, metadata.ChannelName);
                    // For PubSub Push, return 202 Accepted since it's fire-and-forget
                    context.Result = new AcceptedResult();
                    break;

                case ConventionMode.PubSubPullJetStream:
                    // PubSub Pull is consumer mode for background services, not HTTP API calls
                    context.Result = new BadRequestObjectResult($"PubSub Pull mode is not supported for HTTP API calls - use background services instead");
                    break;

                default:
                    context.Result = new BadRequestObjectResult($"Unsupported convention mode: {metadata.ConventionMode}");
                    break;
            }

            _logger.LogDebug("NatsProxyActionFilter: Successfully processed NATS request for {ServiceName}.{MethodName} via {Mode}. Result: {ResultType}",
                metadata.ServiceName, metadata.MethodName, metadata.ConventionMode, context.Result?.GetType().Name ?? "null");
        }
        catch (TimeoutException)
        {
            // Extract audit info from wrapped request if available
            var auditInfo = ExtractAuditInfo(wrappedRequest);
            _logger.LogWarning("NATS request timeout for action {ActionName}. ReqSeqId: {ReqSeqId}, Timestamp: {Timestamp}",
                context.ActionDescriptor.DisplayName,
                auditInfo?.ReqSeqId ?? "N/A",
                auditInfo?.Timestamp ?? "N/A");
            context.Result = new ObjectResult("Request timeout") { StatusCode = 408 };
        }
        catch (Exception ex)
        {
            // Extract audit info from wrapped request if available
            var auditInfo = ExtractAuditInfo(wrappedRequest);
            _logger.LogError(ex, "Error calling NATS service for action {ActionName}: {Message}. ReqSeqId: {ReqSeqId}, Timestamp: {Timestamp}",
                context.ActionDescriptor.DisplayName,
                ex.Message,
                auditInfo?.ReqSeqId ?? "N/A",
                auditInfo?.Timestamp ?? "N/A");
            context.Result = new ObjectResult($"Internal server error: {ex.Message}") { StatusCode = 500 };
        }

        // Don't call next() since we're completely replacing the action execution
    }

    private async Task<INatsConnection?> GetConnectionAsync(string? channelName)
    {
        try
        {
            var serviceFrameworkOptions = _serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<ServiceFrameworkOptions>>()?.Value;

            if (serviceFrameworkOptions == null)
            {
                return await _connectionFactory.CreateConnectionAsync();
            }

            // Priority: specific channel > default connection setting > fallback to default
            if (!string.IsNullOrEmpty(channelName) &&
                serviceFrameworkOptions.Connections.TryGetValue(channelName, out var connectionSettings))
            {
                return await _connectionFactory.CreateConnectionAsync(connectionSettings);
            }
            else if (!string.IsNullOrEmpty(serviceFrameworkOptions.DefaultConnection) &&
                     serviceFrameworkOptions.Connections.TryGetValue(serviceFrameworkOptions.DefaultConnection, out var defaultSettings))
            {
                return await _connectionFactory.CreateConnectionAsync(defaultSettings);
            }
            else
            {
                return await _connectionFactory.CreateConnectionAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create NATS connection for channel '{ChannelName}'", channelName ?? "default");
            return null;
        }
    }

    private object? WrapRequestWithAudit(object? requestData, Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        // Check if audit wrapper is enabled
        var autoConventionOptions = _serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<AutoConventionOptions>>()?.Value;

        if (autoConventionOptions?.EnableAuditWrapper == true)
        {
            // Extract user context information from HTTP context
            var userId = httpContext.User?.Identity?.Name;
            var tenantId = httpContext.Request.Headers["TenantId"].FirstOrDefault();
            var correlationId = httpContext.Request.Headers["CorrelationId"].FirstOrDefault() ?? Guid.NewGuid().ToString();

            if (requestData != null)
            {
                // Wrap in RequestDto if not already wrapped
                var dataType = requestData.GetType();
                if (!(dataType.IsGenericType && dataType.GetGenericTypeDefinition() == typeof(RequestDto<>)))
                {
                    var requestDtoType = typeof(RequestDto<>).MakeGenericType(dataType);
                    var createMethod = requestDtoType.GetMethod("Create", [dataType, typeof(string), typeof(string), typeof(string)]);

                    if (createMethod != null)
                    {
                        return createMethod.Invoke(null, [requestData, userId, tenantId, correlationId]);
                    }
                }
                return requestData;
            }
            else
            {
                // For null requestData (parameterless methods), create RequestDto<object>
                var requestDtoType = typeof(RequestDto<>).MakeGenericType(typeof(object));
                var createMethod = requestDtoType.GetMethod("Create", [typeof(object), typeof(string), typeof(string), typeof(string)]);

                if (createMethod != null)
                {
                    return createMethod.Invoke(null, [new object(), userId, tenantId, correlationId]);
                }
            }
        }

        return requestData;
    }

    private object? ExtractRequestData(ActionExecutingContext context)
    {
        // Get the original method from action metadata
        var originalMethod = context.ActionDescriptor.Properties["OriginalMethod"] as MethodInfo;
        if (originalMethod == null)
        {
            return null;
        }

        var originalParameters = originalMethod.GetParameters();

        // Check if this is originally a parameterless method
        if (originalParameters.Length == 0)
        {
            // For originally parameterless methods, check if EnableAuditWrapper added a RequestDto<object> parameter
            var autoConventionOptions = _serviceProvider.GetService<IOptions<AutoConventionOptions>>()?.Value;
            if (autoConventionOptions?.EnableAuditWrapper == true)
            {
                // When EnableAuditWrapper is true, parameterless methods get a RequestDto<object> parameter
                // Look for this auto-generated parameter in ActionArguments
                foreach (var arg in context.ActionArguments)
                {
                    if (arg.Value?.GetType().IsGenericType == true &&
                        arg.Value.GetType().GetGenericTypeDefinition() == typeof(RequestDto<>) &&
                        arg.Value.GetType().GetGenericArguments()[0] == typeof(object))
                    {
                        // Found the auto-generated RequestDto<object>, return it
                        return arg.Value;
                    }
                }
            }
            return null;
        }

        // For methods with original parameters, extract them normally
        if (originalParameters.Length == 1)
        {
            var parameterName = originalParameters[0].Name!;
            if (context.ActionArguments.TryGetValue(parameterName, out var value))
            {
                return value;
            }
        }

        // For multiple parameters, create an object with parameter values
        var requestData = new Dictionary<string, object?>();
        foreach (var parameter in originalParameters)
        {
            var parameterName = parameter.Name!;
            if (context.ActionArguments.TryGetValue(parameterName, out var value))
            {
                requestData[parameterName] = value;
            }
            else
            {
                requestData[parameterName] = null;
            }
        }

        return requestData.Count > 0 ? requestData : null;
    }

    private async Task<object?> SendNatsRequest(INatsConnection connection, string subject, object? request, bool isOriginallyParameterless, string? channelName)
    {
        _logger.LogDebug("NatsProxyActionFilter: Sending NATS request to subject: {Subject}, Request: {Request}, IsOriginallyParameterless: {IsParameterless}, ChannelName: {ChannelName}",
            subject, request?.ToString() ?? "null", isOriginallyParameterless, channelName ?? "default");

        // Use channel-specific serializer
        var serializerRegistry = GetSerializerForConnection(channelName);

        try
        {
            if (isOriginallyParameterless)
            {
                if (request != null)
                {
                    // Originally parameterless method with EnableAuditWrapper=true, send the RequestDto<object>
                    _logger.LogDebug("NatsProxyActionFilter: Sending NATS request with RequestDto<object> for originally parameterless method to subject: {Subject}, RequestType: {RequestType}",
                        subject, request.GetType().Name);

                    var response = await connection.RequestAsync<object, object>(subject, request,
                        requestSerializer: serializerRegistry.GetSerializer<object>(),
                        replySerializer: serializerRegistry.GetDeserializer<object>());
                    _logger.LogDebug("NatsProxyActionFilter: Received NATS response for parameterless method with wrapper. Data: {Data}",
                        response.Data?.ToString() ?? "null");
                    return response.Data;

                }
                else
                {
                    // Originally parameterless method with EnableAuditWrapper=false
                    _logger.LogDebug("NatsProxyActionFilter: Sending parameterless NATS request to subject: {Subject} (original method had no parameters)", subject);

                    var response = await connection.RequestAsync<object?, object>(subject, null,
                        requestSerializer: null, // Let NATS handle null serialization
                        replySerializer: serializerRegistry.GetDeserializer<object>());
                    _logger.LogDebug("NatsProxyActionFilter: Received NATS response for parameterless request. Data: {Data}",
                        response.Data?.ToString() ?? "null");
                    return response.Data;
                }
            }
            else if (request != null)
            {
                // For requests with data
                _logger.LogDebug("NatsProxyActionFilter: Sending NATS request with data to subject: {Subject}, RequestType: {RequestType}",
                    subject, request.GetType().Name);


                var response = await connection.RequestAsync<object, object>(subject, request,
                    requestSerializer: serializerRegistry.GetSerializer<object>(),
                    replySerializer: serializerRegistry.GetDeserializer<object>());
                _logger.LogDebug("NatsProxyActionFilter: Received NATS response for request with data. Data: {Data}",
                    response.Data?.ToString() ?? "null");
                return response.Data;
            }
            else
            {
                // This shouldn't happen, but handle it as parameterless
                _logger.LogWarning("NatsProxyActionFilter: Unexpected null request for non-parameterless method, treating as parameterless");
                var response = await connection.RequestAsync<object?, object>(subject, null,
                    requestSerializer: null,
                    replySerializer: serializerRegistry.GetDeserializer<object>());
                return response.Data;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending NATS request to subject: {Subject}", subject);
            throw;
        }
    }

    private async Task PublishNatsMessage(INatsConnection connection, string subject, object? message, string? channelName)
    {
        // Use channel-specific serializer
        var serializerRegistry = GetSerializerForConnection(channelName);

        if (message == null)
        {
            // For parameterless publish - use object type to match ServiceFrameworkBackgroundService
            // Send null as the payload for parameterless calls
            await connection.PublishAsync<object?>(subject, null,
                serializer: null); // Let NATS handle null serialization
        }
        else
        {
            // For publish with data - use generic PublishAsync
            await connection.PublishAsync(subject, message,
                serializer: serializerRegistry.GetSerializer<object>());
        }
    }

    private INatsSerializerRegistry GetSerializerForConnection(string? channelName)
    {
        var serviceFrameworkOptions = _serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<ServiceFrameworkOptions>>()?.Value;

        if (serviceFrameworkOptions == null)
        {
            return NatsDefaultSerializerRegistry.Default;
        }

        // Find the connection settings for the specific channel
        if (!string.IsNullOrEmpty(channelName) &&
            serviceFrameworkOptions.Connections.TryGetValue(channelName, out var connectionSettings) &&
            connectionSettings.NatsSerializerRegistry != null)
        {
            return connectionSettings.NatsSerializerRegistry;
        }

        // Try default connection if channelName is not found or empty
        if (!string.IsNullOrEmpty(serviceFrameworkOptions.DefaultConnection) &&
            serviceFrameworkOptions.Connections.TryGetValue(serviceFrameworkOptions.DefaultConnection, out var defaultSettings) &&
            defaultSettings.NatsSerializerRegistry != null)
        {
            return defaultSettings.NatsSerializerRegistry;
        }

        // Fallback to default serializer registry
        return serviceFrameworkOptions.DefaultSerializerRegistry;
    }

    private bool IsProtobufSerializer(INatsSerializerRegistry serializerRegistry)
    {
        // Check if the serializer registry is a Protobuf serializer
        var registryType = serializerRegistry.GetType();
        return registryType.Name.Contains("Protobuf", StringComparison.OrdinalIgnoreCase) ||
               registryType.Namespace?.Contains("Protobuf", StringComparison.OrdinalIgnoreCase) == true;
    }

    private IActionResult GetResponseWithStatus(object? response)
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

    private IActionResult UnwrapResponse(object? response)
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

    private (string ReqSeqId, string Timestamp)? ExtractAuditInfo(object? wrappedRequest)
    {
        if (wrappedRequest == null) return null;

        try
        {
            var requestType = wrappedRequest.GetType();

            // Check if it's a RequestDto<T>
            if (requestType.IsGenericType && requestType.GetGenericTypeDefinition() == typeof(RequestDto<>))
            {
                var reqSeqIdProp = requestType.GetProperty("ReqSeqId");
                var timestampProp = requestType.GetProperty("Timestamp");

                var reqSeqId = reqSeqIdProp?.GetValue(wrappedRequest)?.ToString() ?? "N/A";
                var timestamp = timestampProp?.GetValue(wrappedRequest)?.ToString() ?? "N/A";

                return (reqSeqId, timestamp);
            }
        }
        catch
        {
            // Ignore errors and return null
        }

        return null;
    }

    private (string ReqSeqId, string RspSeqId, string Timestamp)? ExtractResponseAuditInfo(object? response)
    {
        if (response == null) return null;

        try
        {
            var responseType = response.GetType();

            // Check if it's a ResponseDto<T>
            if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(ResponseDto<>))
            {
                var reqSeqIdProp = responseType.GetProperty("ReqSeqId");
                var rspSeqIdProp = responseType.GetProperty("RspSeqId");
                var timestampProp = responseType.GetProperty("Timestamp");

                var reqSeqId = reqSeqIdProp?.GetValue(response)?.ToString() ?? "N/A";
                var rspSeqId = rspSeqIdProp?.GetValue(response)?.ToString() ?? "N/A";
                var timestamp = timestampProp?.GetValue(response)?.ToString() ?? "N/A";

                return (reqSeqId, rspSeqId, timestamp);
            }
        }
        catch
        {
            // Ignore errors and return null
        }

        return null;
    }
}