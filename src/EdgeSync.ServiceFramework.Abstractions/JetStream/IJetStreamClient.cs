using NATS.Client.Core;
using NATS.Client.JetStream;

namespace EdgeSync.ServiceFramework.JetStream;

public interface IBrokerJetStreamClient : IJetStreamClient {}

public interface IBusJetStreamClient : IJetStreamClient {}

/// <summary>
/// Interface for JetStream client operations.
/// </summary>
public interface IJetStreamClient
{
    // abstract string Url {get;}

    // abstract string UserCredFilePath {get;}

    /// <summary>
    /// Gets the maximum number of messages.
    /// </summary>
    int MaxMsgs { get; }

    /// <summary>
    /// Consumes messages asynchronously.
    /// </summary>
    /// <param name="consumer">The NATS JetStream consumer.</param>
    /// <param name="handler">The handler function to process messages.</param>
    /// <param name="autoAck">Indicates whether to automatically acknowledge messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ConsumeAsync(INatsJSConsumer consumer, Func<byte[], string, Task> handler, bool autoAck = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a stream consumer asynchronously.
    /// </summary>
    /// <param name="consumerName">The name of the consumer.</param>
    /// <param name="streamName">The name of the stream.</param>
    /// <param name="subject">The subject to consume messages from.</param>
    /// <returns>A task representing the asynchronous operation, with the created consumer as the result.</returns>
    Task<INatsJSConsumer> CreateStreamConsumerAsync(string consumerName, string streamName, string subject);

    Task TryConnectAsync();

    /// <summary>
    /// Disconnects from the NATS server.
    /// </summary>
    void Disconnect();

    /// <summary>
    /// Disposes the client resources.
    /// </summary>
    void Dispose();

    /// <summary>
    /// Checks if the client is connected.
    /// </summary>
    /// <returns>True if connected, otherwise false.</returns>
    bool IsConnected();

    /// <summary>
    /// Publishes a message asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the message data.</typeparam>
    /// <param name="subject">The subject to publish the message to.</param>
    /// <param name="data">The message data.</param>
    /// <param name="_serializer">The serializer for the message data.</param>
    /// <param name="_cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NatsPublishAsync<T>(string subject, T? data, INatsSerialize<T>? _serializer = null, CancellationToken _cancellationToken = default);

    /// <summary>
    /// Publishes a message asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the message data.</typeparam>
    /// <param name="subject">The subject to publish the message to.</param>
    /// <param name="data">The message data.</param>
    /// <param name="_serializer">The serializer for the message data.</param>
    /// <param name="_cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishAsync<T>(string subject, T? data, INatsSerialize<T>? _serializer = null, CancellationToken _cancellationToken = default);
}