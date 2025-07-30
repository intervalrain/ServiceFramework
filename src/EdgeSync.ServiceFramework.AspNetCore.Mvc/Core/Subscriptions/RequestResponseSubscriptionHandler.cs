using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Data;
using Microsoft.Extensions.Logging;

using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Subscriptions;

/// <summary>
/// Subscription handler for Request/Response mode
/// </summary>
public class RequestResponseSubscriptionHandler : BaseSubscriptionHandler
{
    public RequestResponseSubscriptionHandler(
        IServiceProvider serviceProvider,
        ILogger<RequestResponseSubscriptionHandler> logger) 
        : base(serviceProvider, logger)
    {
    }

    public override ConventionMode SupportedMode => ConventionMode.RequestResponse;

    public override async Task SubscribeAsync(INatsConnection connection, Type serviceType, NatsMethodInfo methodInfo, CancellationToken cancellationToken)
    {
        // Use the generic subscription helper to resolve and invoke with correct type
        await InvokeGenericSubscription(nameof(SubscribeWithTypeAsync), connection, serviceType, methodInfo, cancellationToken);
    }

    private async Task SubscribeWithTypeAsync<T>(INatsConnection connection, Type serviceType, NatsMethodInfo methodInfo, CancellationToken cancellationToken) where T : class
    {
        // Get the correct deserializer for the message type
        var deserializer = connection.Opts.SerializerRegistry.GetDeserializer<T>();
        
        var subscription = await connection.SubscribeCoreAsync<T>(
            methodInfo.SubjectName, 
            serializer: deserializer, 
            cancellationToken: cancellationToken);
        
        Logger.LogInformation("RequestResponse subscription created for subject: {Subject} with type: {MessageType}", 
            methodInfo.SubjectName, typeof(T).Name);

        // Process messages with response handling
        await foreach (var msg in subscription.Msgs.ReadAllAsync(cancellationToken))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleRequestResponseMessage<T>(serviceType, methodInfo, msg, connection);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error processing request/response message for subject: {Subject}", methodInfo.SubjectName);
                    
                    // Send error response with proper type
                    var responseType = MessageTypeResolver.ResolveResponseType(methodInfo.Method, Options.EnableAuditWrapper);
                    
                    object errorResponse;
                    if (Options.EnableAuditWrapper)
                    {
                        var error = ErrorOr.Error.Failure("REQUEST_ERROR", ex.Message);
                        errorResponse = ResponseDto<object>.Failure(error, Guid.NewGuid());
                    }
                    else
                    {
                        errorResponse = new NatsResponse<object>
                        {
                            IsSuccess = false,
                            Data = null,
                            Error = ex.Message
                        };
                    }
                    
                    await msg.ReplyAsync(errorResponse);
                }
            }, cancellationToken);
        }
    }
}