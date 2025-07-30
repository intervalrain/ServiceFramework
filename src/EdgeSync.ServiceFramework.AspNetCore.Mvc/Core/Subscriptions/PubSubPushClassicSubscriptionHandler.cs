using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

using Microsoft.Extensions.Logging;

using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Subscriptions;

/// <summary>
/// Subscription handler for Pub/Sub Push Classic mode
/// </summary>
public class PubSubPushClassicSubscriptionHandler : BaseSubscriptionHandler
{
    public PubSubPushClassicSubscriptionHandler(
        IServiceProvider serviceProvider,
        ILogger<PubSubPushClassicSubscriptionHandler> logger) 
        : base(serviceProvider, logger)
    {
    }

    public override ConventionMode SupportedMode => ConventionMode.PubSubPushClassic;

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
        
        Logger.LogInformation("Classic PubSub subscription created for subject: {Subject} with type: {MessageType}", 
            methodInfo.SubjectName, typeof(T).Name);

        // Process messages
        await foreach (var msg in subscription.Msgs.ReadAllAsync(cancellationToken))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleClassicMessage<T>(serviceType, methodInfo, msg, connection);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error processing classic message for subject: {Subject}", methodInfo.SubjectName);
                }
            }, cancellationToken);
        }
    }
}