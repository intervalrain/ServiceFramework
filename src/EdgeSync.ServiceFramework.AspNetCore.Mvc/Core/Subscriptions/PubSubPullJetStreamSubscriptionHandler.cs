using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

using Microsoft.Extensions.Logging;

using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Subscriptions;

/// <summary>
/// Subscription handler for Pub/Sub Pull JetStream mode
/// </summary>
public class PubSubPullJetStreamSubscriptionHandler : BaseSubscriptionHandler
{
    public PubSubPullJetStreamSubscriptionHandler(
        IServiceProvider serviceProvider,
        ILogger<PubSubPullJetStreamSubscriptionHandler> logger) 
        : base(serviceProvider, logger)
    {
    }

    public override ConventionMode SupportedMode => ConventionMode.PubSubPullJetStream;

    public override async Task SubscribeAsync(INatsConnection connection, Type serviceType, NatsMethodInfo methodInfo, CancellationToken cancellationToken)
    {
        var js = new NatsJSContext(connection);
        
        // Ensure Stream exists
        await EnsureStreamExists(js, methodInfo.SubjectName);
        
        // Create or update consumer for pull mode
        var consumerName = methodInfo.JetStreamPullAttribute?.ConsumerName ?? $"{serviceType.Name}_{methodInfo.Method.Name}_pull";
        var consumer = await js.CreateOrUpdateConsumerAsync(
            stream: GetStreamName(methodInfo.SubjectName), 
            new ConsumerConfig(consumerName)
            {
                FilterSubject = methodInfo.SubjectName,
                DeliverPolicy = ConsumerConfigDeliverPolicy.New,
                AckPolicy = ConsumerConfigAckPolicy.Explicit
            });

        Logger.LogInformation("JetStream Pull subscription created for subject: {Subject}", methodInfo.SubjectName);

        // Check if method parameter is collection to determine fetch strategy
        if (IsCollectionParameter(methodInfo.Method))
        {
            await ProcessWithFetch(consumer, serviceType, methodInfo, cancellationToken);
        }
        else
        {
            await ProcessWithNext(consumer, serviceType, methodInfo, cancellationToken);
        }
    }

    private async Task ProcessWithNext(INatsJSConsumer consumer, Type serviceType, NatsMethodInfo methodInfo, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var msg = await consumer.NextAsync<string>(cancellationToken: cancellationToken);
                if (msg.HasValue)
                {
                    try
                    {
                        await HandleJetStreamMessage(serviceType, methodInfo, msg.Value);
                        await msg.Value.AckAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error processing JetStream pull (next) message for subject: {Subject}", methodInfo.SubjectName);
                        await msg.Value.NakAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in JetStream pull (next) loop for subject: {Subject}", methodInfo.SubjectName);
                await Task.Delay(1000, cancellationToken); // backoff
            }
        }
    }

    private async Task ProcessWithFetch(INatsJSConsumer consumer, Type serviceType, NatsMethodInfo methodInfo, CancellationToken cancellationToken)
    {
        var maxMsgs = methodInfo.JetStreamPullAttribute?.MaxMessages ?? 100;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var msg in consumer.FetchAsync<string>(new NatsJSFetchOpts { MaxMsgs = maxMsgs }).WithCancellation(cancellationToken))
                {
                    try
                    {
                        await HandleJetStreamMessage(serviceType, methodInfo, msg);
                        await msg.AckAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error processing JetStream pull (fetch) message for subject: {Subject}", methodInfo.SubjectName);
                        await msg.NakAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in JetStream pull (fetch) loop for subject: {Subject}", methodInfo.SubjectName);
                await Task.Delay(1000, cancellationToken); // backoff
            }
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

    private bool IsCollectionParameter(System.Reflection.MethodInfo method)
    {
        var parameters = method.GetParameters();
        return parameters.Any(p => 
            p.ParameterType != typeof(string) && 
            typeof(System.Collections.IEnumerable).IsAssignableFrom(p.ParameterType));
    }
}