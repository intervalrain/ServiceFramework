using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

using Microsoft.Extensions.Logging;

using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Subscriptions;

/// <summary>
/// Subscription handler for Pub/Sub Push JetStream mode
/// </summary>
public class PubSubPushJetStreamSubscriptionHandler : BaseSubscriptionHandler
{
    public PubSubPushJetStreamSubscriptionHandler(
        IServiceProvider serviceProvider,
        ILogger<PubSubPushJetStreamSubscriptionHandler> logger) 
        : base(serviceProvider, logger)
    {
    }

    public override ConventionMode SupportedMode => ConventionMode.PubSubPushJetStream;

    public override async Task SubscribeAsync(INatsConnection connection, Type serviceType, NatsMethodInfo methodInfo, CancellationToken cancellationToken)
    {
        // Use the generic subscription helper to resolve and invoke with correct type
        await InvokeGenericSubscription(nameof(SubscribeWithTypeAsync), connection, serviceType, methodInfo, cancellationToken);
    }

    private async Task SubscribeWithTypeAsync<T>(INatsConnection connection, Type serviceType, NatsMethodInfo methodInfo, CancellationToken cancellationToken) where T : class
    {
        var js = new NatsJSContext(connection);
        
        // Ensure Stream exists
        await EnsureStreamExists(js, methodInfo.SubjectName);
        
        // Create or update consumer for push mode
        var consumerName = $"{serviceType.Name}_{methodInfo.Method.Name}_push";
        var consumer = await js.CreateOrUpdateConsumerAsync(
            stream: GetStreamName(methodInfo.SubjectName), 
            new ConsumerConfig(consumerName)
            {
                FilterSubject = methodInfo.SubjectName,
                DeliverPolicy = ConsumerConfigDeliverPolicy.New,
                AckPolicy = ConsumerConfigAckPolicy.Explicit
            });

        Logger.LogInformation("JetStream Push subscription created for subject: {Subject} with type: {MessageType}", 
            methodInfo.SubjectName, typeof(T).Name);

        // Get the correct deserializer for the message type
        var deserializer = connection.Opts.SerializerRegistry.GetDeserializer<T>();

        // Use ConsumeAsync with proper generic type and serializer
        await foreach (var msg in consumer.ConsumeAsync<T>(deserializer).WithCancellation(cancellationToken))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleJetStreamMessage<T>(serviceType, methodInfo, msg, connection);
                    await msg.AckAsync();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error processing JetStream push message for subject: {Subject}", methodInfo.SubjectName);
                    await msg.NakAsync();
                }
            }, cancellationToken);
        }
    }

    private async Task EnsureStreamExists(INatsJSContext js, string subject)
    {
        var streamName = GetStreamName(subject);
        try
        {
            await js.GetStreamAsync(streamName);
        }
        catch (NatsJSException)
        {
            // Stream doesn't exist, create new one
            await js.CreateStreamAsync(new StreamConfig(streamName, [subject]));
            Logger.LogInformation("Created JetStream stream: {StreamName} for subject: {Subject}", streamName, subject);
        }
    }

    private string GetStreamName(string subject)
    {
        // Convert subject to stream name (e.g., "author.get" -> "AUTHOR")
        var parts = subject.Split('.');
        return parts[0].ToUpperInvariant();
    }
}