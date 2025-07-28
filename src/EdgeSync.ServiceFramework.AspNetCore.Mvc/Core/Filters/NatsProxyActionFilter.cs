using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;


namespace EdgeSync.ServiceFramework.Core.Filters;

/// <summary>
/// Refactored action filter that intercepts requests and routes them through NATS using the new handler architecture.
/// This class now acts as a coordinator, delegating specific functionality to specialized handlers and services.
/// Reduced from 650+ lines to ~70 lines by applying SOLID principles and separation of concerns.
/// </summary>
public class NatsProxyActionFilter : IAsyncActionFilter
{
    private readonly IAuditHandler _auditHandler;
    private readonly IConnectionResolver _connectionResolver;
    private readonly IRequestDataExtractor _requestDataExtractor;
    private readonly IConventionModeHandlerFactory _handlerFactory;
    private readonly ILogger<NatsProxyActionFilter> _logger;

    public NatsProxyActionFilter(
        IAuditHandler auditHandler,
        IConnectionResolver connectionResolver,
        IRequestDataExtractor requestDataExtractor,
        IConventionModeHandlerFactory handlerFactory,
        ILogger<NatsProxyActionFilter> logger)
    {
        _auditHandler = auditHandler;
        _connectionResolver = connectionResolver;
        _requestDataExtractor = requestDataExtractor;
        _handlerFactory = handlerFactory;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        _logger.LogDebug("NatsProxyActionFilter: Starting execution for action {ActionName}",
            context.ActionDescriptor.DisplayName);

        object? wrappedRequest = null;

        try
        {
            // Create metadata from action descriptor
            var metadata = new ActionContextMetadata(context.ActionDescriptor);

            if (string.IsNullOrEmpty(metadata.Subject))
            {
                _logger.LogError("Subject not found in action metadata for {ServiceName}.{MethodName}", 
                    metadata.ServiceName, metadata.MethodName);
                context.Result = new BadRequestObjectResult("Subject not found in action metadata");
                return;
            }

            // Get connection using the connection resolver
            var connection = await _connectionResolver.GetConnectionAsync(metadata.ChannelName);
            if (connection == null)
            {
                var channelDisplayName = string.IsNullOrEmpty(metadata.ChannelName) ? "default" : metadata.ChannelName;
                _logger.LogError("Unable to connect to NATS for channel: {ChannelName}", channelDisplayName);
                context.Result = new ObjectResult($"Unable to connect to NATS for channel: {channelDisplayName}")
                {
                    StatusCode = 500
                };
                return;
            }

            // Extract and wrap request data using specialized services
            var requestData = _requestDataExtractor.ExtractRequestData(context);
            wrappedRequest = requestData;
            // wrappedRequest = _auditHandler.WrapRequestWithAudit(requestData, context.HttpContext);

            // Get the appropriate handler for this convention mode
            var handler = _handlerFactory.GetHandler(metadata.ConventionMode);
            
            // Delegate to the specialized handler
            context.Result = await handler.HandleAsync(context, connection, metadata, wrappedRequest);

            _logger.LogDebug("NatsProxyActionFilter: Successfully processed NATS request for {ServiceName}.{MethodName} via {Mode}. Result: {ResultType}",
                metadata.ServiceName, metadata.MethodName, metadata.ConventionMode, context.Result?.GetType().Name ?? "null");
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Unsupported convention mode for action {ActionName}: {Message}",
                context.ActionDescriptor.DisplayName, ex.Message);
            context.Result = new BadRequestObjectResult($"Unsupported convention mode: {ex.Message}");
        }
        catch (TimeoutException)
        {
            // Extract audit info from wrapped request if available
            var auditInfo = _auditHandler.ExtractAuditInfo(wrappedRequest);
            _logger.LogWarning("NATS request timeout for action {ActionName}. ReqSeqId: {ReqSeqId}, Timestamp: {Timestamp}",
                context.ActionDescriptor.DisplayName,
                auditInfo?.ReqSeqId ?? "N/A",
                auditInfo?.Timestamp ?? "N/A");
            context.Result = new ObjectResult("Request timeout") { StatusCode = 408 };
        }
        catch (Exception ex)
        {
            // Extract audit info from wrapped request if available
            var auditInfo = _auditHandler.ExtractAuditInfo(wrappedRequest);
            _logger.LogError(ex, "Error calling NATS service for action {ActionName}: {Message}. ReqSeqId: {ReqSeqId}, Timestamp: {Timestamp}",
                context.ActionDescriptor.DisplayName,
                ex.Message,
                auditInfo?.ReqSeqId ?? "N/A",
                auditInfo?.Timestamp ?? "N/A");
            context.Result = new ObjectResult($"Internal server error: {ex.Message}") { StatusCode = 500 };
        }

        // Don't call next() since we're completely replacing the action execution
    }
}