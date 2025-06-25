using System.Text;
using System.Text.RegularExpressions;

using EdgeSync.ServiceFramework.Contracts;
using EdgeSync.ServiceFramework.Exceptions;

using EdgeSync.ServiceFramework.JetStream;

using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework;

/// <summary>
/// Base class for handling events in the ShadowAgent application.
/// Inherits from <see cref="MessageTransportBase"/>.
/// </summary>
public abstract class BaseEventHandler : MessageTransportBase
{
    /// <summary>
    /// Subject name for the JetStream consumer. Could be a single subject or a comma-separated list of subjects.
    /// </summary>
    protected abstract string SubjectName { get; }

    /// <summary>
    /// Stream name for the JetStream consumer.
    /// </summary>
    protected abstract string StreamName { get; }

    /// <summary>
    /// Consumer name for the JetStream consumer.
    /// </summary>
    protected abstract string ConsumerName { get; }
    public ILogger<BaseEventHandler> Logger { get; }
    protected string Protocol = string.Empty;
    protected string GroupId = string.Empty;
    protected string DeviceId = string.Empty;

    /// <summary>
    ///  Lazy initialization of the default JetStream client. app can override this to use a different client.
    /// </summary>
    /// <remarks>
    /// This is used to provide a default JetStream client for the event handler.
    /// </remarks>
    protected abstract JetStreamConfigOptions JStreamCfgOpts { get; set; }

    /// <summary>
    /// Lazy initialization of the consumer configuration options.
    /// </summary>
    /// <remarks>
    /// This is used to configure the consumer settings such as durable name, ack policy, etc.
    /// </remarks>
    protected abstract ConsumerConfigOptions ConsumerCfgOpts { get; set;}

    /// <summary>
    /// Legacy constructor for backward compatibility
    /// </summary>
    public BaseEventHandler(
        ILogger<BaseEventHandler> logger,
        IBrokerJetStreamClient broker,
        IBusJetStreamClient bus) : base(broker, bus)
    {
        DefaultLazy = new Lazy<IJetStreamClient>(() => broker); // Use broker as default for backward compatibility
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        JStreamCfgOpts = JStreamCfgOpts ?? new JetStreamConfigOptions();
        JStreamCfgOpts.Name = StreamName;
        JStreamCfgOpts.Subjects = SubjectName.Split(',').Select(s => s.Trim()).ToArray();
        ConsumerCfgOpts = ConsumerCfgOpts ?? new ConsumerConfigOptions();
        ConsumerCfgOpts.DurableName = ConsumerName;
        ConsumerCfgOpts.Name = ConsumerName;
    }

    /// <summary>
    /// New constructor allowing custom connection name for Broker
    /// Bus still uses default "bus" connection
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="factory">JetStream client factory</param>
    /// <param name="connectionName">Connection name for Broker (defaults to "broker" if not specified)</param>
    public BaseEventHandler(
        ILogger<BaseEventHandler> logger,
        IJetStreamClientFactory factory,
        string connectionName = "Broker") : base(factory, connectionName)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        JStreamCfgOpts = JStreamCfgOpts ?? new JetStreamConfigOptions();
        JStreamCfgOpts.Name = StreamName;
        JStreamCfgOpts.Subjects = SubjectName.Split(',').Select(s => s.Trim()).ToArray();
        ConsumerCfgOpts = ConsumerCfgOpts ?? new ConsumerConfigOptions();
        ConsumerCfgOpts.DurableName = ConsumerName;
        ConsumerCfgOpts.Name = ConsumerName;
    }

    /// <summary>
    /// Regular expression pattern for matching subjects.
    /// </summary>
    public static readonly Regex SubjectPattern = new Regex(@"^(?<protocol>[^\.]+)\.(?<groupID>[^\.]+)\.(?<deviceID>[^\.]+)\..*$", RegexOptions.Compiled);

    /// <summary>
    /// Disposes the resources used by the <see cref="BaseEventHandler"/> class.
    /// </summary>
    /// <param name="disposing">Indicates whether the method is called from Dispose method.</param>
    protected virtual void Dispose(bool disposing)
    {
        Broker.Dispose();
    }

    /// <summary>
    /// Handles the input event. This method should be overridden in derived classes.
    /// </summary>
    /// <param name="message">The message received.</param>
    /// <param name="subject">The subject of the message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task HandleInputEventCore(byte[] message, string subject);

    public async Task HandleInputEvent(byte[] message, string subject)
    {
        try
        {
            (Protocol, GroupId, DeviceId) = GetGroupDeviceIDs(subject);
            await HandleInputEventCore(message, subject);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Failed to process message for subject {subject}. Error: {ex.Message}");
            await HandleGeneralException(ex, message, subject);
            // throw;
        }
    }

    /// <summary>
    /// Handles general exceptions. Can be overridden in derived classes.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <param name="message">The original message bytes.</param>
    /// <param name="subject">The message subject.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected virtual Task HandleGeneralException(Exception ex, byte[] message, string subject)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the background service. Connects to JetStream and consumes messages.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {

        if (ServiceConfig.MsgBrokerUrl.Length == 0)
        {
            Logger.LogError("ServiceConfig.MsgBusUrl is not set. Cannot initialize service.");
            return;
        }

        var retryAttempt = 0;
        const int maxRetryAttempts = 100; // Allow for many retries as this is a background service

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (retryAttempt > 0)
                {
                    Logger.LogWarning("Event handler reconnection attempt {RetryAttempt}/{MaxRetries} for consumer {ConsumerName}", 
                        retryAttempt, maxRetryAttempts, ConsumerName);
                }

                var consumer = await Broker.CreateStreamConsumerAsync(ConsumerCfgOpts, JStreamCfgOpts);
                
                // Reset retry counter on successful connection
                if (retryAttempt > 0)
                {
                    Logger.LogInformation("Event handler successfully reconnected for consumer {ConsumerName} after {RetryAttempt} attempts",
                        ConsumerName, retryAttempt);
                    retryAttempt = 0;
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    await Broker.ConsumeAsync(consumer, HandleInputEvent);
                    await Task.Yield();
                }
            }
            catch (NatsConnException ex)
            {
                retryAttempt++;
                Logger.LogError(ex, "NATS connection error on attempt {RetryAttempt}/{MaxRetries}: {ConsumerName} {StreamName} {SubjectName}", 
                    retryAttempt, maxRetryAttempts, ConsumerName, StreamName, SubjectName);
                
                if (ex.ErrorCode == NatsErrorCode.CredFileEmpty || ex.ErrorCode == NatsErrorCode.UrlEmpty)
                {
                    Logger.LogError("NATS URL or CredFile is empty. Cannot initialize service.");
                    return;
                }

                if (retryAttempt >= maxRetryAttempts)
                {
                    Logger.LogError("Maximum retry attempts ({MaxRetries}) reached for consumer {ConsumerName}. Stopping service.", 
                        maxRetryAttempts, ConsumerName);
                    return;
                }
            }
            catch (Exception e)
            {
                retryAttempt++;
                Logger.LogError(e, "Consumer error on attempt {RetryAttempt}/{MaxRetries}: {ConsumerName} {StreamName} {SubjectName}", 
                    retryAttempt, maxRetryAttempts, ConsumerName, StreamName, SubjectName);

                if (retryAttempt >= maxRetryAttempts)
                {
                    Logger.LogError("Maximum retry attempts ({MaxRetries}) reached for consumer {ConsumerName}. Stopping service.", 
                        maxRetryAttempts, ConsumerName);
                    return;
                }
            }
            
            // to avoid high CPU usage when jetstream client has no connection or error.
            Logger.LogWarning("Event handler will retry in 1000ms for consumer {ConsumerName} (attempt {RetryAttempt}/{MaxRetries})", 
                ConsumerName, retryAttempt, maxRetryAttempts);
            await Task.Delay(1000, cancellationToken);
        }
    }

    /// <summary>
    /// Stops the background service.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Extracts the protocol, group ID, and device ID from the subject.
    /// </summary>
    /// <param name="subject">The subject string.</param>
    /// <returns>A tuple containing the protocol, group ID, and device ID.</returns>
    /// <exception cref="Exception">Thrown when the subject format is invalid.</exception>
    public (string, string, string) GetGroupDeviceIDs(string subject)
    {
        Match match = SubjectPattern.Match(subject);

        if (match.Success)
        {
            string protocol = match.Groups["protocol"].Value;
            string groupID = match.Groups["groupID"].Value;
            string deviceID = match.Groups["deviceID"].Value;

            return (protocol, groupID, deviceID);
        }
        else
        {
            throw new Exception("Invalid subject format: " + subject);
        }
    }

    /// <summary>
    /// Sends a response message to the specified topic.
    /// </summary>
    /// <param name="topic">The topic to send the response to.</param>
    /// <param name="cmd">The command associated with the response.</param>
    /// <param name="data">The data to include in the response.</param>
    /// <param name="seqId">The sequnce id for the request.</param>
    /// <param name="reqSeqId">The request sequence ID.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SendResponse(string topic, string cmd, ServiceResponseDataModelDto data, ulong seqId = 0L, string reqSeqId = "")
    {
        var respMsg = new ServiceResponseModelDto
        {
            Cmd = cmd,
            SeqId = seqId,
            ReqSeqId = reqSeqId,
            RspSeqId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            data = data
        };
        try
        {
            var resp = ResponseModelDto.Serialize(respMsg);
            await Broker.PublishAsync(topic, Encoding.ASCII.GetBytes(resp));
            Logger.LogInformation("Response sent to {topic}", topic);
        }
        catch (Exception e)
        {
            Logger.LogError("Failed to send response: {e} to {subject}", e.Message, topic);
            Logger.LogError("StackTrace: {e}", e.StackTrace);
        }
    }

    /// <summary>
    /// Publishes a message to the specified topic.
    /// </summary>
    /// <param name="topic">The topic to publish the message to.</param>
    /// <param name="message">The message to publish.</param>
    /// <param name="isAtLeastOnce">Indicates whether the message should be published at least once.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task PublishMsgAsync(string topic, byte[] message, bool isAtLeastOnce = false)
    {
        try
        {
            if (isAtLeastOnce == true)
            {
                await Broker.PublishAsync(topic, message);
            }
            else
            {
                await Broker.NatsPublishAsync(topic, message);
            }
            Logger.LogInformation("Message published to {topic}", topic);
        }
        catch (Exception e)
        {
            Logger.LogError("Failed to publish message: {e} to {topic}", e.Message, topic);
            Logger.LogError("StackTrace: {e}", e.StackTrace);
        }
    }
}

