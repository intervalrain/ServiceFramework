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
        var subscription = await connection.SubscribeCoreAsync<string>(methodInfo.SubjectName, cancellationToken: cancellationToken);
        
        Logger.LogInformation("Classic PubSub subscription created for subject: {Subject}", methodInfo.SubjectName);

        // Process messages
        await foreach (var msg in subscription.Msgs.ReadAllAsync(cancellationToken))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleClassicMessage(serviceType, methodInfo, msg);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error processing classic message for subject: {Subject}", methodInfo.SubjectName);
                }
            }, cancellationToken);
        }
    }
}