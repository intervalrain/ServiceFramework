using EdgeSync.ServiceFramework.Abstractions;
using EdgeSync.ServiceFramework.Abstractions.JetStream;
using EdgeSync.ServiceFramework.Abstractions.Models;

using Microsoft.Extensions.Logging;

using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Client.KeyValueStore;
using NATS.Net;

namespace EdgeSync.ServiceFramework.Core.JetStream;

/// <summary>
/// Represents a client for interacting with NATS JetStream.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="JetStreamClient"/> class.
/// </remarks>
public class JetStreamClient : IDisposable, IJetStreamClient
{
    public INatsConnection? NatsConnection { get; private set; }

    private INatsJSContext? _jsCtx;
    private INatsJSStream? _jStream;

    private readonly ILogger<JetStreamClient> _logger;
    private CancellationTokenSource _cts = new CancellationTokenSource();

    private readonly string _serviceUUID = Guid.NewGuid().ToString();

    private readonly INatsConnectionFactory _natsConnectionFactory;
    private readonly NatsConnectionSettings? _connectionSettings;

    /// <param name="logger">The logger instance to use for logging.</param>
    /// <param name="natsConnectionFactory">The connection factory for create nats connection instance.</param>
    /// <param name="connectionSettings">Optional connection settings to use instead of legacy config.</param>
    public JetStreamClient(ILogger<JetStreamClient> logger, INatsConnectionFactory natsConnectionFactory, NatsConnectionSettings? connectionSettings = null)
    {
        _logger = logger;
        _natsConnectionFactory = natsConnectionFactory;
        _connectionSettings = connectionSettings;
    }

    public string Url { get; set; } = string.Empty;
    public string UserCredFilePath { get; set; } = string.Empty;

    public string StreamName { get; set; } = "sf_stream";

    public int MaxMsgs { get; } = ServiceConfig.NatsJetStreamConsumerFetch; // max number of messages to per callback function call.

    public async Task TryConnectAsync()
    {

        if (IsConnected())
        {
            return;
        }

        if (NatsConnection != null)
        {
            await NatsConnection.DisposeAsync();
        }

        if (_connectionSettings != null)
        {
            NatsConnection = await _natsConnectionFactory.CreateConnectionAsync(_connectionSettings);
        }
        else
        {
            NatsConnection = await _natsConnectionFactory.CreateConnectionAsync(Url, UserCredFilePath);
        }
    }

    /// <summary>
    /// Checks if the client is connected to the NATS server.
    /// </summary>
    /// <returns>True if connected, otherwise false.</returns>
    public bool IsConnected()
    {
        return NatsConnection != null && NatsConnection.ConnectionState == NatsConnectionState.Open;
    }

    /// <summary>
    /// Disconnects the client from the NATS server.
    /// </summary>
    public void Disconnect()
    {
        if (NatsConnection != null)
        {
            NatsConnection.DisposeAsync().AsTask().GetAwaiter().GetResult();
            NatsConnection = null;
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

        var kvCtx = NatsConnection?.CreateKeyValueStoreContext();
        if (kvCtx == null) throw new NatsKVException("No connection to NATS");
        return await kvCtx.CreateStoreAsync(bucket);
    }

    /// <summary>
    /// Creates a stream consumer in JetStream.
    /// </summary>
    /// <param name="consumerCfg">The configuration options for the consumer.</param>
    /// <param name="cfgOptions">The configuration options for the jet stream.</param>
    /// <returns>The created stream consumer.</returns>
    /// <exception cref="Exception">Thrown if the client is not connected to NATS.</exception>
    public async Task<INatsJSConsumer> CreateStreamConsumerAsync(ConsumerConfigOptions consumerCfg, JetStreamConfigOptions cfgOptions)
    {
        await TryConnectAsync();

        if (cfgOptions.Name == null || cfgOptions.Name.Trim().Length == 0)
        {
            throw new ArgumentException("Stream name must be provided in JetStreamConfigOptions", nameof(cfgOptions));
        }

        if (cfgOptions.Subjects == null || cfgOptions.Subjects.Count() == 0)
        {
            throw new ArgumentException("At least one subject must be provided in JetStreamConfigOptions", nameof(cfgOptions));
        }

        if (consumerCfg.Name == null || consumerCfg.Name.Trim().Length == 0)
        {
            throw new ArgumentException("Consumer name must be provided in ConsumerConfig", nameof(consumerCfg));
        }

        var streamName = cfgOptions.Name ?? "edgeSync_stream";
        StreamName = streamName;

        _logger.LogInformation("{_serviceUUID} Checked _natsConnection: {ConnectionState}", _serviceUUID, NatsConnection?.ConnectionState);

        try
        {
            if (_jsCtx == null && NatsConnection != null)
            {
                _jsCtx = new NatsJSContext(NatsConnection);
            }
            if (_jsCtx == null) throw new NatsJSException("NatsJSContext is not constructed successfully");

            _jStream = await _jsCtx.GetStreamAsync(streamName);
            if (_jStream != null)
            {
                _logger.LogInformation("Stream '{streamName}' exists, updating configuration.", streamName);
                _jStream = await _jsCtx.UpdateStreamAsync(cfgOptions);
            }
        }
        catch (NatsJSException ex) when (ex.Message.Contains("stream not found"))
        {
            _logger.LogInformation("Stream '{streamName}' not found, create new stream.", streamName);
            _jStream = await _jsCtx!.CreateStreamAsync(cfgOptions);
        }

        var consumer = await _jStream!.CreateOrUpdateConsumerAsync(consumerCfg);

        _logger.LogInformation("{_serviceUUID} Created consumer: {consumerName}, stream: {streamName}, subjects: {subject}", _serviceUUID, consumerCfg.Name, cfgOptions.Name, cfgOptions.Subjects);

        return consumer;
    }

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
    /// <param name="serializer">The serializer to use for the data.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <exception cref="Exception">Thrown if the client is not connected to JetStream.</exception>
    public async Task<PubAckResponse> PublishAsync<T>(string subject, T? data, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default)
    {
        await TryConnectAsync();

        if (_jsCtx == null)
        {
            _jsCtx = new NatsJSContext(NatsConnection!);
        }

        return await _jsCtx.PublishAsync(subject, data, serializer: serializer, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Publishes a message to the specified subject using the NATS connection.
    /// </summary>
    /// <typeparam name="T">The type of the data to publish.</typeparam>
    /// <param name="subject">The subject to publish the message to.</param>
    /// <param name="data">The data to publish.</param>
    /// <param name="serializer">The serializer to use for the data.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    public async Task NatsPublishAsync<T>(string subject, T? data, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default)
    {
        await TryConnectAsync();

        await NatsConnection!.PublishAsync(subject, data, serializer: serializer!, cancellationToken: cancellationToken);
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

        var response = await NatsConnection!.RequestAsync<T, string>(subject, data, cancellationToken: cancellationToken);
        return response.Data;
    }

    public async Task<TR?> RequestAsync<T, TR>(string subject, T data, CancellationToken cancellationToken = default)
    {
        await TryConnectAsync();

        var response = await NatsConnection!.RequestAsync<T, TR>(subject, data, cancellationToken: cancellationToken);
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
        if (NatsConnection != null)
            NatsConnection.DisposeAsync().AsTask().GetAwaiter().GetResult();
        NatsConnection = null;
    }
}

public class MsgBrokerJetStreamClient : JetStreamClient, IBrokerJetStreamClient
{
    public MsgBrokerJetStreamClient(ILogger<JetStreamClient> logger, INatsConnectionFactory natsConnectionFactory, NatsConnectionSettings? connectionSettings = null) 
        : base(logger, natsConnectionFactory, connectionSettings)
    {
        // Use legacy config as fallback if connectionSettings is null
        if (connectionSettings == null)
        {
            Url = ServiceConfig.MsgBrokerUrl;
            UserCredFilePath = ServiceConfig.MsgBrokerCredFile;
        }
    }
}

public class MsgBusJetStreamClient : JetStreamClient, IBusJetStreamClient
{
    public MsgBusJetStreamClient(ILogger<JetStreamClient> logger, INatsConnectionFactory natsConnectionFactory, NatsConnectionSettings? connectionSettings = null) 
        : base(logger, natsConnectionFactory, connectionSettings)
    {
        // Use legacy config as fallback if connectionSettings is null
        if (connectionSettings == null)
        {
            Url = ServiceConfig.MsgBusUrl;
            UserCredFilePath = ServiceConfig.MsgBusCredFile;
        }
    }
}