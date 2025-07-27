using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using EdgeSync.ServiceFramework.Abstractions;
using EdgeSync.ServiceFramework.Data;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using System.Reflection;

namespace EdgeSync.ServiceFramework.Core.Controllers;

/// <summary>
/// Generic proxy controller that routes HTTP requests through NATS
/// </summary>
public class NatsProxyController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NatsProxyController> _logger;
    private readonly INatsConnectionFactory _connectionFactory;

    public NatsProxyController(
        IServiceProvider serviceProvider,
        ILogger<NatsProxyController> logger,
        INatsConnectionFactory connectionFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Generic method to handle all NATS service calls
    /// This method will be called dynamically with action metadata
    /// </summary>
    public async Task<IActionResult> InvokeNatsService(CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract metadata from action context
            var actionDescriptor = ControllerContext.ActionDescriptor;
            var serviceName = actionDescriptor.Properties["ServiceName"] as string ?? "Unknown";
            var methodName = actionDescriptor.Properties["MethodName"] as string ?? "Unknown";
            var subject = actionDescriptor.Properties["Subject"] as string;
            var mode = (ConventionMode)(actionDescriptor.Properties["ConventionMode"] ?? ConventionMode.RequestResponse);
            var channelName = actionDescriptor.Properties["ChannelName"] as string;

            if (string.IsNullOrEmpty(subject))
            {
                return BadRequest("Subject not found in action metadata");
            }

            // Get connection based on channel name
            var connection = await GetConnectionAsync(channelName);
            if (connection == null)
            {
                return StatusCode(500, $"Unable to connect to NATS for channel: {channelName ?? "default"}");
            }

            // Extract request data from action parameters
            var requestData = ExtractRequestData();
            var wrappedRequest = WrapRequestWithAudit(requestData);

            // Handle different convention modes
            switch (mode)
            {
                case ConventionMode.RequestResponse:
                    var response = await SendNatsRequest(connection, subject, wrappedRequest, cancellationToken);
                    return Ok(response);

                case ConventionMode.PubSubPushClassic:
                case ConventionMode.PubSubPushJetStream:
                    await PublishNatsMessage(connection, subject, wrappedRequest, cancellationToken);
                    // For PubSub Push, return 202 Accepted since it's fire-and-forget
                    // This is suitable for async operations like notifications, events, etc.
                    return Accepted();

                case ConventionMode.PubSubPullJetStream:
                    // PubSub Pull is consumer mode for background services, not HTTP API calls
                    return BadRequest($"PubSub Pull mode is not supported for HTTP API calls - use background services instead");

                default:
                    return BadRequest($"Unsupported convention mode: {mode}");
            }
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("NATS request timeout for action {ActionName}", ControllerContext.ActionDescriptor.ActionName);
            return StatusCode(408, "Request timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling NATS service for action {ActionName}: {Message}", 
                ControllerContext.ActionDescriptor.ActionName, ex.Message);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    private async Task<INatsConnection?> GetConnectionAsync(string? channelName)
    {
        try
        {
            // Use the same connection logic as ServiceFrameworkBackgroundService
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

    private object? WrapRequestWithAudit(object? requestData)
    {
        // Check if audit wrapper is enabled
        var autoConventionOptions = _serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<AutoConventionOptions>>()?.Value;
        
        if (autoConventionOptions?.EnableAuditWrapper == true && requestData != null)
        {
            // Extract user context information from HTTP context
            var userId = HttpContext.User?.Identity?.Name;
            var tenantId = HttpContext.Request.Headers["TenantId"].FirstOrDefault();
            var correlationId = HttpContext.Request.Headers["CorrelationId"].FirstOrDefault() ?? Guid.NewGuid().ToString();

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
        }

        return requestData;
    }

    private object? ExtractRequestData()
    {
        // Get the original method from action metadata
        var originalMethod = ControllerContext.ActionDescriptor.Properties["OriginalMethod"] as MethodInfo;
        if (originalMethod == null)
        {
            return null;
        }

        var parameters = originalMethod.GetParameters();
        if (parameters.Length == 0)
        {
            return null;
        }

        // For single parameter methods, return the parameter value directly
        if (parameters.Length == 1)
        {
            var parameterName = parameters[0].Name!;
            if (ControllerContext.ActionDescriptor.Parameters.Any(p => p.Name == parameterName))
            {
                return HttpContext.Request.Form.ContainsKey(parameterName) 
                    ? HttpContext.Request.Form[parameterName].ToString()
                    : HttpContext.Request.Query[parameterName].ToString();
            }
        }

        // For multiple parameters, create an object with parameter values
        var requestData = new Dictionary<string, object?>();
        foreach (var parameter in parameters)
        {
            var parameterName = parameter.Name!;
            if (HttpContext.Request.Form.ContainsKey(parameterName))
            {
                requestData[parameterName] = HttpContext.Request.Form[parameterName].ToString();
            }
            else if (HttpContext.Request.Query.ContainsKey(parameterName))
            {
                requestData[parameterName] = HttpContext.Request.Query[parameterName].ToString();
            }
            else
            {
                requestData[parameterName] = null;
            }
        }

        return requestData.Count > 0 ? requestData : null;
    }

    private async Task<object?> SendNatsRequest(INatsConnection connection, string subject, object? request, CancellationToken cancellationToken)
    {
        // Use the serializer from connection settings
        var serviceFrameworkOptions = _serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<ServiceFrameworkOptions>>()?.Value;
        var serializerRegistry = serviceFrameworkOptions?.DefaultSerializerRegistry ?? NatsDefaultSerializerRegistry.Default;

        if (request == null)
        {
            // For parameterless requests
            var response = await connection.RequestAsync<object, object>(subject, new object(), 
                requestSerializer: serializerRegistry.GetSerializer<object>(),
                replySerializer: serializerRegistry.GetDeserializer<object>(),
                cancellationToken: cancellationToken);
            return response.Data;
        }
        else
        {
            // For requests with data
            var requestType = request.GetType();
            
            // Use reflection to call the generic RequestAsync method
            var requestMethod = typeof(INatsConnection).GetMethods()
                .Where(m => m.Name == "RequestAsync" && m.IsGenericMethod && m.GetParameters().Length >= 4)
                .FirstOrDefault(m => m.GetGenericArguments().Length == 2);

            if (requestMethod != null)
            {
                var genericMethod = requestMethod.MakeGenericMethod(requestType, typeof(object));
                var responseTask = (Task)genericMethod.Invoke(connection, 
                [
                    subject,
                    request,
                    null, // headers
                    serializerRegistry.GetSerializer<object>(),
                    serializerRegistry.GetDeserializer<object>(),
                    null, // requestOpts
                    cancellationToken
                ])!;

                await responseTask;
                
                // Extract the response data
                var resultProperty = responseTask.GetType().GetProperty("Result");
                if (resultProperty != null)
                {
                    var response = resultProperty.GetValue(responseTask);
                    var dataProperty = response?.GetType().GetProperty("Data");
                    return dataProperty?.GetValue(response);
                }
            }
        }

        return null;
    }

    private async Task PublishNatsMessage(INatsConnection connection, string subject, object? message, CancellationToken cancellationToken)
    {
        // Use the serializer from connection settings
        var serviceFrameworkOptions = _serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<ServiceFrameworkOptions>>()?.Value;
        var serializerRegistry = serviceFrameworkOptions?.DefaultSerializerRegistry ?? NatsDefaultSerializerRegistry.Default;

        if (message == null)
        {
            // For parameterless publish
            await connection.PublishAsync(subject, new object(), 
                serializer: serializerRegistry.GetSerializer<object>(),
                cancellationToken: cancellationToken);
        }
        else
        {
            // For publish with data - use generic PublishAsync
            await connection.PublishAsync(subject, message, 
                serializer: serializerRegistry.GetSerializer<object>(),
                cancellationToken: cancellationToken);
        }
    }
}