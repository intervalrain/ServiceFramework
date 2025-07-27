using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Core.Filters;
using EdgeSync.ServiceFramework.Core.Serializers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using System.Reflection;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Handlers;

/// <summary>
/// Handler for request-response convention mode in NATS proxy
/// </summary>
public class NatsRequestResponseHandler : IConventionModeHandler
{
    public ConventionMode SupportedMode => ConventionMode.RequestResponse;
    
    private readonly ISerializerAdapterFactory _serializerAdapterFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectionResolver _connectionResolver;
    private readonly IResponseProcessor _responseProcessor;
    private readonly ILogger<NatsRequestResponseHandler> _logger;

    public NatsRequestResponseHandler(
        ISerializerAdapterFactory serializerAdapterFactory,
        IServiceProvider serviceProvider,
        IConnectionResolver connectionResolver,
        IResponseProcessor responseProcessor,
        ILogger<NatsRequestResponseHandler> logger)
    {
        _serializerAdapterFactory = serializerAdapterFactory;
        _serviceProvider = serviceProvider;
        _connectionResolver = connectionResolver;
        _responseProcessor = responseProcessor;
        _logger = logger;
    }

    public async Task<IActionResult> HandleAsync(ActionExecutingContext context, INatsConnection connection, 
        ActionContextMetadata metadata, object? wrappedRequest)
    {
        try
        {
            // Extract expected response type from original method
            var originalMethod = context.ActionDescriptor.Properties["OriginalMethod"] as MethodInfo;
            var expectedResponseType = GetExpectedResponseType(originalMethod);
            
            // Check if this is a parameterless method that got wrapped with audit info
            var isOriginallyParameterless = originalMethod?.GetParameters().Length == 0;

            var response = await SendNatsRequest(connection, metadata.Subject, wrappedRequest, 
                isOriginallyParameterless, expectedResponseType, metadata.ChannelName);
                
            _logger.LogDebug("NatsRequestResponseHandler: NATS Request/Response completed. Subject: {Subject}, Response: {Response}",
                metadata.Subject, response?.ToString() ?? "null");

            // Log successful request with audit info for monitoring
            LogSuccessfulRequest(metadata, wrappedRequest, response);

            // Handle response based on UseExceptionHandler setting
            var autoConventionOptions = _serviceProvider.GetService<IOptions<AutoConventionOptions>>()?.Value;
            var useExceptionHandler = autoConventionOptions?.UseExceptionHandler == true;
            
            return _responseProcessor.ProcessResponse(response, useExceptionHandler);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in NatsRequestResponseHandler for subject: {Subject}", metadata.Subject);
            throw;
        }
    }

    private async Task<object?> SendNatsRequest(INatsConnection connection, string subject, object? request, 
        bool isOriginallyParameterless, Type? expectedResponseType, string? channelName)
    {
        _logger.LogDebug("NatsRequestResponseHandler: Sending NATS request to subject: {Subject}, Request: {Request}, IsOriginallyParameterless: {IsParameterless}, ExpectedResponseType: {ResponseType}, ChannelName: {ChannelName}",
            subject, request?.ToString() ?? "null", isOriginallyParameterless, expectedResponseType?.Name ?? "Unknown", channelName ?? "default");

        // Use channel-specific serializer and get appropriate adapter
        var serializerRegistry = _connectionResolver.GetSerializerForConnection(channelName);
        var adapter = _serializerAdapterFactory.GetAdapter(serializerRegistry);

        try
        {
            _logger.LogDebug("NatsRequestResponseHandler: Using serializer adapter: {AdapterType} for subject: {Subject}", 
                adapter.GetType().Name, subject);

            // Delegate to the adapter - it knows how to handle the specific serializer requirements
            var response = await adapter.SendRequestAsync(connection, subject, request, isOriginallyParameterless, expectedResponseType, serializerRegistry);
            
            _logger.LogDebug("NatsRequestResponseHandler: Received NATS response for subject: {Subject}. Data: {Data}",
                subject, response?.ToString() ?? "null");
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending NATS request to subject: {Subject} using adapter: {AdapterType}", 
                subject, adapter.GetType().Name);
            throw;
        }
    }

    private void LogSuccessfulRequest(ActionContextMetadata metadata, object? wrappedRequest, object? response)
    {
        try
        {
            // Extract audit info from wrapped request
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
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log audit information for successful request");
        }
    }

    private Type? GetExpectedResponseType(MethodInfo? originalMethod)
    {
        if (originalMethod == null)
        {
            return null;
        }

        var returnType = originalMethod.ReturnType;

        // Handle Task<T> and ValueTask<T>
        if (returnType.IsGenericType)
        {
            var genericDefinition = returnType.GetGenericTypeDefinition();
            if (genericDefinition == typeof(Task<>) || genericDefinition == typeof(ValueTask<>))
            {
                // Extract T from Task<T> or ValueTask<T>
                returnType = returnType.GetGenericArguments()[0];
            }
        }

        // Handle void/Task (non-generic Task means void)
        if (returnType == typeof(void) || returnType == typeof(Task) || returnType == typeof(ValueTask))
        {
            return null; // No response expected
        }

        _logger.LogDebug("GetExpectedResponseType: Extracted response type {ResponseType} from method {MethodName}", 
            returnType.Name, originalMethod.Name);

        return returnType;
    }

    private (string ReqSeqId, string Timestamp)? ExtractAuditInfo(object? wrappedRequest)
    {
        if (wrappedRequest == null) return null;

        try
        {
            var requestType = wrappedRequest.GetType();

            // Check if it's a RequestDto<T>
            if (requestType.IsGenericType && requestType.GetGenericTypeDefinition() == typeof(EdgeSync.ServiceFramework.Data.RequestDto<>))
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
            if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(EdgeSync.ServiceFramework.Data.ResponseDto<>))
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