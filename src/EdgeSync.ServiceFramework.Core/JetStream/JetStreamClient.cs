using Microsoft.Extensions.Logging;

using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Client.KeyValueStore;
using NATS.Net;

namespace EdgeSync.ServiceFramework.JetStream;

public class MsgBrokerJetStreamClient(ILogger<JetStreamClient> logger, INatsConnectionFactory natsConnectionFactory)
    : JetStreamClient(logger, natsConnectionFactory), IBrokerJetStreamClient
{
    public override string Url { get; } = ServiceConfig.MsgBrokerUrl;
    public override string UserCredFilePath { get; } = ServiceConfig.MsgBrokerCredFile;
}

public class MsgBusJetStreamClient(ILogger<JetStreamClient> logger, INatsConnectionFactory natsConnectionFactory)
    : JetStreamClient(logger, natsConnectionFactory), IBusJetStreamClient
{
    public override string Url { get; } = ServiceConfig.MsgBusUrl;
    public override string UserCredFilePath { get; } = ServiceConfig.MsgBusCredFile;
}

/// <summary>
/// Represents a client for interacting with NATS JetStream.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="JetStreamClient"/> class.
/// </remarks>
/// <param name="logger">The logger instance to use for logging.</param>
public abstract class JetStreamClient(ILogger<JetStreamClient> logger, INatsConnectionFactory natsConnectionFactory) : IDisposable, IJetStreamClient
{
    private INatsConnection? _natsConnection;
    private INatsJSContext? _jsCtx;
    private INatsJSStream? _jStream;

    private readonly ILogger<JetStreamClient> _logger = logger;
    private CancellationTokenSource _cts = new CancellationTokenSource();

    private readonly long _ackWait = ServiceConfig.NatsTimeout; // 10*1000 mseconds

    private readonly string _serviceUUID = Guid.NewGuid().ToString();

    private readonly INatsConnectionFactory _natsConnectionFactory = natsConnectionFactory;

    public abstract string Url { get; }
    public abstract string UserCredFilePath { get; }

    public string StreamName { get; set; } = "sf_stream";

    public int MaxMsgs { get; } = ServiceConfig.NatsJetStreamConsumerFetch; // max number of messages to per callback function call.

    public async Task TryConnectAsync()
    {

        if (IsConnected())
        {
            return;
        }

        if (_natsConnection != null)
        {
            await _natsConnection.DisposeAsync();
        }

        _natsConnection = await _natsConnectionFactory.CreateConnectionAsync(Url, UserCredFilePath);
    }

    /// <summary>
    /// Checks if the client is connected to the NATS server.
    /// </summary>
    /// <returns>True if connected, otherwise false.</returns>
    public bool IsConnected()
    {
        return _natsConnection != null && _natsConnection.ConnectionState == NatsConnectionState.Open;
    }

    /// <summary>
    /// Disconnects the client from the NATS server.
    /// </summary>
    public void Disconnect()
    {
        if (_natsConnection != null)
        {
            _natsConnection.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _natsConnection = null;
        }
    }

    /// <summary>
    /// Creates a key-value store in JetStream.
    /// </summary>
    /// <param name="bucket">The name of the bucket to create.</param>
    /// <returns>The created key-value store.</returns>
    /// <exception cref="Exception">Thrown if the client is not connected to NATS.</exception>
    protected async Task<INatsKVStore> CreateKeyValueStore(string bucket)
    {
        await TryConnectAsync();

        var kvCtx = _natsConnection?.CreateKeyValueStoreContext();
        if (kvCtx == null) throw new NatsKVException("No connection to NATS");
        return await kvCtx.CreateStoreAsync(bucket);
    }

    public async Task CreateStream(string streamName)
    {

        StreamName = streamName;

        await TryConnectAsync();

        if (_jsCtx == null && _natsConnection != null) _jsCtx = new NatsJSContext(_natsConnection);
        if (_jsCtx == null) throw new NatsJSException("NatsJSContext is not constructed successfully");
        var cfg = new StreamConfig(name: StreamName, subjects: Array.Empty<string>());

        try
        {
            _jStream = await _jsCtx.GetStreamAsync(StreamName);
            if (_jStream != null)
            {
                _logger.LogInformation("Stream '{StreamName}' exists, updating configuration.", StreamName);
                _jStream = await _jsCtx.UpdateStreamAsync(cfg);
            }
        }
        catch (NatsJSException ex) when (ex.Message.Contains("stream not found"))
        {
            _logger.LogInformation("Stream '{StreamName}' not found, create new stream.", StreamName);
            _jStream = await _jsCtx.CreateStreamAsync(cfg);
        }
    }

    /// <summary>
    /// Creates a stream consumer in JetStream.
    /// </summary>
    /// <param name="consumerName">The name of the consumer.</param>
    /// <param name="streamName">The name of the stream.</param>
    /// <param name="subject">The subject to consume messages from.</param>
    /// <returns>The created stream consumer.</returns>
    /// <exception cref="Exception">Thrown if the client is not connected to NATS.</exception>
    public async Task<INatsJSConsumer> CreateStreamConsumerAsync(string consumerName, string streamName, string subject)
    {
        StreamName = streamName;

        await TryConnectAsync();

        _logger.LogInformation("{_serviceUUID} Checked _natsConnection: {ConnectionState}", _serviceUUID, _natsConnection?.ConnectionState);

        var ackWait = TimeSpan.FromMilliseconds(_ackWait);
        var ackPolicy = ConsumerConfigAckPolicy.Explicit;
        var subjects = Array.Empty<string>();

        if (subject != null)
        {
            subjects = [subject];
        }

        var cfg = new StreamConfig(name: streamName, subjects: subjects)
        {
            // Retention = StreamConfigRetention.Workqueue,
        };

        try
        {
            if (_jsCtx == null && _natsConnection != null)
            {
                _jsCtx = new NatsJSContext(_natsConnection);
            }
            if (_jsCtx == null) throw new NatsJSException("NatsJSContext is not constructed successfully");

            _jStream = await _jsCtx.GetStreamAsync(streamName);
            if (_jStream != null)
            {
                _logger.LogInformation("Stream '{streamName}' exists, updating configuration.", streamName);
                _jStream = await _jsCtx.UpdateStreamAsync(cfg);
            }
        }
        catch (NatsJSException ex) when (ex.Message.Contains("stream not found"))
        {
            _logger.LogInformation("Stream '{streamName}' not found, create new stream.", streamName);
            _jStream = await _jsCtx!.CreateStreamAsync(cfg);
        }

        var consumer = await _jStream!.CreateOrUpdateConsumerAsync(new ConsumerConfig(consumerName)
        {
            AckPolicy = ackPolicy,
            AckWait = ackWait,
        });

        _logger.LogInformation("{_serviceUUID} Created consumer: {consumerName}, stream: {streamName}, subject: {subject}", _serviceUUID, consumerName, streamName, subject);

        return consumer;
    }

    // public void EnsureJetStreamContext()
    // {
    //     if (_natsConnection == null || !IsConnected())
    //     {
    //         Connect();
    //     }

    //     if (_jsCtx == null)
    //     {
    //         _jsCtx = new NatsJSContext(_natsConnection);
    //         _logger.LogInformation("{UUID} Created new JetStream context", _serviceUUID);
    //     }
    // }

    /// <summary>
    /// Consumes messages from a JetStream consumer.
    /// </summary>
    /// <param name="consumer">The JetStream consumer to consume messages from.</param>
    /// <param name="handler">The handler function to process messages.</param>
    /// <param name="autoAck">Whether to automatically acknowledge messages.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    public async Task ConsumeAsync(INatsJSConsumer consumer, Func<byte[], string, Task> handler, bool autoAck = true, CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var natsJSFetchOpts = new NatsJSFetchOpts
        {
            MaxMsgs = MaxMsgs,
            NotificationHandler = (notification, _) =>
            {
                if (notification is NatsJSProtocolNotification { HeaderCode: 409, HeaderMessageText: "Server Shutdown" })
                {
                    _cts.Cancel();
                }
                return Task.CompletedTask;
            },
        };
        try
        {
            await foreach (var msg in consumer.FetchAsync<byte[]>(opts: natsJSFetchOpts, cancellationToken: _cts.Token))
            {
                _logger.LogDebug(_serviceUUID + " Received msg [{Subject}] len {Size}", msg.Subject, msg.Size);
                if (handler != null)
                {
                    if (msg.Data != null)
                    {
                        try
                        {
                            await handler(msg.Data, msg.Subject);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e.Message);
                            _logger.LogDebug(e.StackTrace);
                        }
                    }
                }
                if (autoAck)
                {
                    await msg.AckAsync();
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"{_serviceUUID} Error occurred while fetching messages.", _serviceUUID);
            throw;
        }
    }

    /// <summary>
    /// Publishes a message to the specified subject using JetStream.
    /// </summary>
    /// <typeparam name="T">The type of the data to publish.</typeparam>
    /// <param name="subject">The subject to publish the message to.</param>
    /// <param name="data">The data to publish.</param>
    /// <param name="_serializer">The serializer to use for the data.</param>
    /// <param name="_cancellationToken">The cancellation token to cancel the operation.</param>
    /// <exception cref="Exception">Thrown if the client is not connected to JetStream.</exception>
    public async Task PublishAsync<T>(string subject, T? data, INatsSerialize<T>? _serializer = null, CancellationToken _cancellationToken = default(CancellationToken))
    {
        await TryConnectAsync();

        if (_jsCtx == null)
        {
            _jsCtx = new NatsJSContext(_natsConnection!);
        }

        await _jsCtx.PublishAsync(subject, data, serializer: _serializer, cancellationToken: _cancellationToken);
    }

    /// <summary>
    /// Publishes a message to the specified subject using the NATS connection.
    /// </summary>
    /// <typeparam name="T">The type of the data to publish.</typeparam>
    /// <param name="subject">The subject to publish the message to.</param>
    /// <param name="data">The data to publish.</param>
    /// <param name="_serializer">The serializer to use for the data.</param>
    /// <param name="_cancellationToken">The cancellation token to cancel the operation.</param>
    public async Task NatsPublishAsync<T>(string subject, T? data, INatsSerialize<T>? _serializer = null, CancellationToken _cancellationToken = default)
    {
        await TryConnectAsync();

        await _natsConnection!.PublishAsync(subject, data, serializer: _serializer!, cancellationToken: _cancellationToken);
    }

    /// <summary>
    /// Sends a request message to the specified subject using the NATS connection,
    /// and awaits a reply with a 30-second timeout. Supports external cancellation via a linked token.
    /// </summary>
    /// <typeparam name="T">The type of the request data to send.</typeparam>
    /// <param name="subject">The NATS subject to send the request to.</param>
    /// <param name="data">The request data to send.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the request externally.</param>
    /// <returns>The response data as a string, or <c>null</c> if no response is received.</returns>
    /// <exception cref="TimeoutException">Thrown when the request exceeds the 30-second timeout.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled externally.</exception>
    /// <example>
    /// <code>
    /// var cancellationToken = httpContext.RequestAborted;
    /// try
    /// {
    ///     var reply = await natsService.RequestAsync&lt;MyRequest&gt;(
    ///         "my.service.subject",
    ///         new MyRequest { Id = 123 },
    ///         cancellationToken
    ///     );
    ///     Console.WriteLine($"Reply: {reply}");
    /// }
    /// catch (TimeoutException)
    /// {
    ///     Console.WriteLine("Request timed out.");
    /// }
    /// </code>
    /// </example>

    public async Task<string?> RequestAsync<T>(string subject, T data, CancellationToken cancellationToken = default)
    {
        await TryConnectAsync();

        var response = await _natsConnection!.RequestAsync<T, string>(subject, data, cancellationToken: cancellationToken);
        return response.Data;
    }

    /// <summary>
    /// Disposes the resources used by the JetStreamClient.
    /// </summary>
    /// <remarks>
    /// This method logs a warning message, sets the JetStream and context objects to null,
    /// and disposes the NATS connection asynchronously.
    /// </remarks>
    public void Dispose()
    {
        _logger.LogWarning(_serviceUUID + " JetStreamClient Dispose");
        if (_jStream != null)
            _jStream = null;
        if (_jsCtx != null)
            _jsCtx = null;
        if (_natsConnection != null)
            _natsConnection.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _natsConnection = null;
    }
}