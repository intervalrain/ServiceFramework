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

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Handlers;

/// <summary>
/// Handler for pub-sub convention modes in NATS proxy
/// </summary>
public class PubSubHandler : IConventionModeHandler
{
    public ConventionMode SupportedMode => ConventionMode.PubSubPushClassic | 
                                          ConventionMode.PubSubPushJetStream | 
                                          ConventionMode.PubSubPullJetStream;
    
    private readonly ISerializerAdapterFactory _serializerAdapterFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectionResolver _connectionResolver;
    private readonly ILogger<PubSubHandler> _logger;

    public PubSubHandler(
        ISerializerAdapterFactory serializerAdapterFactory,
        IServiceProvider serviceProvider,
        IConnectionResolver connectionResolver,
        ILogger<PubSubHandler> logger)
    {
        _serializerAdapterFactory = serializerAdapterFactory;
        _serviceProvider = serviceProvider;
        _connectionResolver = connectionResolver;
        _logger = logger;
    }

    public async Task<IActionResult> HandleAsync(ActionExecutingContext context, INatsConnection connection, 
        ActionContextMetadata metadata, object? wrappedRequest)
    {
        try
        {
            await PublishMessage(connection, metadata.Subject, wrappedRequest, metadata.ConventionMode, metadata.ChannelName);
                
            _logger.LogDebug("PubSubHandler: NATS Publish completed. Subject: {Subject}, Mode: {Mode}",
                metadata.Subject, metadata.ConventionMode);

            // Log successful publish with audit info for monitoring
            LogSuccessfulPublish(metadata, wrappedRequest);

            // Pub-sub operations typically return NoContent (204) status
            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PubSubHandler for subject: {Subject}, Mode: {Mode}", 
                metadata.Subject, metadata.ConventionMode);
            throw;
        }
    }

    private async Task PublishMessage(INatsConnection connection, string subject, object? request, 
        ConventionMode mode, string? channelName)
    {
        _logger.LogDebug("PubSubHandler: Publishing message to subject: {Subject}, Mode: {Mode}, Request: {Request}, ChannelName: {ChannelName}",
            subject, mode, request?.ToString() ?? "null", channelName ?? "default");

        // Use channel-specific serializer and get appropriate adapter
        var serializerRegistry = _connectionResolver.GetSerializerForConnection(channelName);
        var adapter = _serializerAdapterFactory.GetAdapter(serializerRegistry);

        try
        {
            _logger.LogDebug("PubSubHandler: Using serializer adapter: {AdapterType} for subject: {Subject}", 
                adapter.GetType().Name, subject);

            // Delegate to the adapter based on the convention mode
            switch (mode)
            {
                case ConventionMode.PubSubPushClassic:
                    await adapter.PublishAsync(connection, subject, request, serializerRegistry);
                    break;
                    
                case ConventionMode.PubSubPushJetStream:
                case ConventionMode.PubSubPullJetStream:
                    // For JetStream modes, use standard publish for now
                    // TODO: Implement proper JetStream publish in future iterations
                    await adapter.PublishAsync(connection, subject, request, serializerRegistry);
                    break;
                    
                default:
                    throw new NotSupportedException($"Convention mode {mode} is not supported by PubSubHandler");
            }
            
            _logger.LogDebug("PubSubHandler: Successfully published message to subject: {Subject}, Mode: {Mode}",
                subject, mode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message to subject: {Subject}, Mode: {Mode} using adapter: {AdapterType}", 
                subject, mode, adapter.GetType().Name);
            throw;
        }
    }

    private void LogSuccessfulPublish(ActionContextMetadata metadata, object? wrappedRequest)
    {
        try
        {
            // Extract audit info from wrapped request for monitoring
            var requestAuditInfo = ExtractAuditInfo(wrappedRequest);
            
            if (requestAuditInfo != null)
            {
                _logger.LogInformation("NATS message published successfully. Service: {ServiceName}.{MethodName}, Subject: {Subject}, Mode: {Mode}, ReqSeqId: {ReqSeqId}, RequestTimestamp: {RequestTimestamp}",
                    metadata.ServiceName, metadata.MethodName, metadata.Subject, metadata.ConventionMode,
                    requestAuditInfo.Value.ReqSeqId, requestAuditInfo.Value.Timestamp);
            }
            else
            {
                _logger.LogInformation("NATS message published successfully. Service: {ServiceName}.{MethodName}, Subject: {Subject}, Mode: {Mode}",
                    metadata.ServiceName, metadata.MethodName, metadata.Subject, metadata.ConventionMode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log audit information for successful publish");
        }
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
}