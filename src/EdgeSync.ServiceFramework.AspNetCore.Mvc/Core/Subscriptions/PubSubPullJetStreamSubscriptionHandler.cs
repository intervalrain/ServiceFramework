using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Subscriptions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Enums;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace EdgeSync.ServiceFramework.Core.Subscriptions;

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
        // Use the generic subscription helper to resolve and invoke with correct type
        await InvokeGenericSubscription(nameof(SubscribeWithTypeAsync), connection, serviceType, methodInfo, cancellationToken);
    }

    private async Task SubscribeWithTypeAsync<T>(INatsConnection connection, Type serviceType, NatsMethodInfo methodInfo, CancellationToken cancellationToken) where T : class
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

        Logger.LogInformation("JetStream Pull subscription created for subject: {Subject} with type: {MessageType}", 
            methodInfo.SubjectName, typeof(T).Name);

        // Determine pull type from attribute or default based on method parameter
        var pullType = methodInfo.JetStreamPullAttribute?.PullType ?? DeterminePullType(methodInfo.Method);
        
        Logger.LogInformation("Using pull type: {PullType} for subject: {Subject}", pullType, methodInfo.SubjectName);
        
        // Get the correct deserializer for the message type
        var deserializer = connection.Opts.SerializerRegistry.GetDeserializer<T>();
        
        switch (pullType)
        {
            case PullType.Consume:
                await ProcessWithConsume<T>(consumer, serviceType, methodInfo, connection, deserializer, cancellationToken);
                break;
            case PullType.Fetch:
                await ProcessWithFetch<T>(consumer, serviceType, methodInfo, connection, deserializer, cancellationToken);
                break;
            case PullType.FetchNoWait:
                await ProcessWithFetchNoWait<T>(consumer, serviceType, methodInfo, connection, deserializer, cancellationToken);
                break;
            case PullType.Next:
                await ProcessWithNext<T>(consumer, serviceType, methodInfo, connection, deserializer, cancellationToken);
                break;
            default:
                throw new NotSupportedException($"Pull type {pullType} is not supported");
        }
    }

    private async Task ProcessWithNext<T>(INatsJSConsumer consumer, Type serviceType, NatsMethodInfo methodInfo, INatsConnection connection, INatsDeserialize<T> deserializer, CancellationToken cancellationToken) where T : class
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var msg = await consumer.NextAsync<T>(deserializer, cancellationToken: cancellationToken);
                if (msg.HasValue)
                {
                    try
                    {
                        await HandleJetStreamMessage<T>(serviceType, methodInfo, msg.Value, connection);
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

    private async Task ProcessWithFetch<T>(INatsJSConsumer consumer, Type serviceType, NatsMethodInfo methodInfo, INatsConnection connection, INatsDeserialize<T> deserializer, CancellationToken cancellationToken) where T : class
    {
        var maxMsgs = methodInfo.JetStreamPullAttribute?.MaxMessages ?? 100;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var msg in consumer.FetchAsync<T>(new NatsJSFetchOpts { MaxMsgs = maxMsgs }, deserializer).WithCancellation(cancellationToken))
                {
                    try
                    {
                        await HandleJetStreamMessage<T>(serviceType, methodInfo, msg, connection);
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
    
    private PullType DeterminePullType(System.Reflection.MethodInfo method)
    {
        // Default logic: use Fetch for collection parameters, Next for single items
        return IsCollectionParameter(method) ? PullType.Fetch : PullType.Next;
    }
    
    private async Task ProcessWithConsume<T>(INatsJSConsumer consumer, Type serviceType, NatsMethodInfo methodInfo, INatsConnection connection, INatsDeserialize<T> deserializer, CancellationToken cancellationToken) where T : class
    {
        var maxMsgs = methodInfo.JetStreamPullAttribute?.MaxMessages ?? 10;
        var consumeOpts = new NatsJSConsumeOpts
        {
            MaxMsgs = maxMsgs,
            Expires = TimeSpan.FromSeconds(10),
            IdleHeartbeat = TimeSpan.FromSeconds(5)
        };
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var msg in consumer.ConsumeAsync<T>(deserializer, consumeOpts).WithCancellation(cancellationToken))
                {
                    try
                    {
                        await HandleJetStreamMessage<T>(serviceType, methodInfo, msg, connection);
                        await msg.AckAsync(cancellationToken: cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error processing JetStream pull (consume) message for subject: {Subject}", methodInfo.SubjectName);
                        await msg.NakAsync(cancellationToken: cancellationToken);
                    }
                }
            }
            catch (NatsJSProtocolException ex)
            {
                Logger.LogWarning(ex, "Protocol error in JetStream pull (consume) for subject: {Subject}", methodInfo.SubjectName);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in JetStream pull (consume) loop for subject: {Subject}", methodInfo.SubjectName);
                await Task.Delay(1000, cancellationToken);
            }
        }
    }
    
    private async Task ProcessWithFetchNoWait<T>(INatsJSConsumer consumer, Type serviceType, NatsMethodInfo methodInfo, INatsConnection connection, INatsDeserialize<T> deserializer, CancellationToken cancellationToken) where T : class
    {
        var maxMsgs = methodInfo.JetStreamPullAttribute?.MaxMessages ?? 10;
        var fetchNoWaitOpts = new NatsJSFetchOpts { MaxMsgs = maxMsgs };
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var fetchMsgCount = 0;
                
                // NoWaitFetch is a specialized operation not available on the public interface
                await foreach (var msg in ((NatsJSConsumer)consumer).FetchNoWaitAsync<T>(fetchNoWaitOpts, deserializer).WithCancellation(cancellationToken))
                {
                    fetchMsgCount++;
                    try
                    {
                        await HandleJetStreamMessage<T>(serviceType, methodInfo, msg, connection);
                        await msg.AckAsync(cancellationToken: cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error processing JetStream pull (fetch-no-wait) message for subject: {Subject}", methodInfo.SubjectName);
                        await msg.NakAsync(cancellationToken: cancellationToken);
                    }
                }
                
                if (fetchMsgCount < maxMsgs)
                {
                    Logger.LogDebug("No more messages available for subject: {Subject}. Waiting...", methodInfo.SubjectName);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
            catch (NatsJSProtocolException ex)
            {
                Logger.LogWarning(ex, "Protocol error in JetStream pull (fetch-no-wait) for subject: {Subject}", methodInfo.SubjectName);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in JetStream pull (fetch-no-wait) loop for subject: {Subject}", methodInfo.SubjectName);
                await Task.Delay(1000, cancellationToken);
            }
        }
    }
}