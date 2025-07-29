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

}