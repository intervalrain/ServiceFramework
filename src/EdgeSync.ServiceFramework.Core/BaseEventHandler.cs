using System.Text;
using System.Text.RegularExpressions;

using EdgeSync.ServiceFramework.Contracts;

using EdgeSync.ServiceFramework.JetStream;

using Microsoft.Extensions.Logging;

using ShadowAgent.Infrastructure.Models;

namespace EdgeSync.ServiceFramework;

/// <summary>
/// Base class for handling events in the ShadowAgent application.
/// Inherits from <see cref="BackgroundService"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BaseEventHandler"/> class.
/// </remarks>
/// <param name="logger">Logger instance.</param>
/// <param name="jetStreamClient">JetStream client instance.</param>
public abstract class BaseEventHandler(
    ILogger<BaseEventHandler> logger,
    IBrokerJetStreamClient broker,
    IBusJetStreamClient bus)
    : MessageTransportBase(broker, bus)
{
    /// <summary>
    /// Subject name for the JetStream consumer.
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
    public ILogger<BaseEventHandler> Logger { get; } = logger;
    protected string Protocol = string.Empty;
    protected string GroupId = string.Empty;
    protected string DeviceId = string.Empty;

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
    /// Handles InvalidProtocolBufferException exceptions. Can be overridden in derived classes.
    /// </summary>
    /// <param name="ex">The validation exception.</param>
    /// <param name="message">The original message bytes.</param>
    /// <param name="subject">The message subject.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    // protected virtual async Task HandleInvalidProtocolBufferException(InvalidProtocolBufferException ex, byte[] message, string subject)
    // {
    //     await HandleGeneralException(ex, message, subject);
    // }

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
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var consumer = await Broker.CreateStreamConsumerAsync(ConsumerName, StreamName, SubjectName);
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Broker.ConsumeAsync(consumer, HandleInputEvent);
                    await Task.Yield();
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Consumer error: {ConsumerName} {StreamName} {SubjectName}", ConsumerName, StreamName, SubjectName);
                Logger.LogError(e.Message);
                Logger.LogError(e, e.StackTrace);
            }
            // to avoid high CPU usage when jetstream client has no connection or error.
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

