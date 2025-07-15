using EdgeSync.ServiceFramework.Abstractions.JetStream;

using Microsoft.Extensions.Hosting;

namespace EdgeSync.ServiceFramework.Core;

public abstract class MessageTransportBase : BackgroundService
{
    private readonly IJetStreamClientFactory? _jetStreamFactory;
    private readonly string? _connectionName;
    protected Lazy<IJetStreamClient>? DefaultLazy;
    private readonly Lazy<IBrokerJetStreamClient>? _brokerLazy;
    private readonly Lazy<IBusJetStreamClient>? _busLazy;

    /// <summary>
    /// Legacy constructor for backward compatibility
    /// </summary>
    protected MessageTransportBase(IBrokerJetStreamClient broker, IBusJetStreamClient bus)
    {
        _brokerLazy = new Lazy<IBrokerJetStreamClient>(() => broker);
        _busLazy = new Lazy<IBusJetStreamClient>(() => bus);
    }

    /// <summary>
    /// New constructor using factory for lazy loading with named connection
    /// </summary>
    protected MessageTransportBase(IJetStreamClientFactory jetStreamFactory, string connectionName)
    {
        _jetStreamFactory = jetStreamFactory ?? throw new ArgumentNullException(nameof(jetStreamFactory));
        _connectionName = connectionName ?? throw new ArgumentNullException(nameof(connectionName));
        
        // Default uses the specified connection name
        DefaultLazy = new Lazy<IJetStreamClient>(() => _jetStreamFactory.CreateClient(_connectionName));
        
        // Broker and Bus use their respective default connections for backward compatibility
        _brokerLazy = new Lazy<IBrokerJetStreamClient>(() => _jetStreamFactory.CreateMsgBrokerClient());
        _busLazy = new Lazy<IBusJetStreamClient>(() => _jetStreamFactory.CreateMsgBusClient());
    }

    /// <summary>
    /// Default JetStream client using the specified connection name.
    /// Uses lazy loading - only creates connection when accessed.
    /// </summary>
    protected IJetStreamClient Default => DefaultLazy?.Value ?? throw new InvalidOperationException("Default client not initialized");

    /// <summary>
    /// JetStream client for device communication.
    /// Handles message exchange with IoT devices and hardware endpoints.
    /// Uses "broker" connection name.
    /// </summary>
    protected IBrokerJetStreamClient Broker => _brokerLazy?.Value ?? throw new InvalidOperationException("Broker client not initialized");

    /// <summary>
    /// JetStream client for client-side communication.
    /// Manages message exchange with user applications and frontend services.
    /// Uses "bus" connection name.
    /// </summary>
    protected IBusJetStreamClient Bus => _busLazy?.Value ?? throw new InvalidOperationException("Bus client not initialized");

    /// <summary>
    /// Publishes a message using the Default IJetStreamClient connection.
    /// This is a convenience method that delegates to Default.PublishAsync().
    /// </summary>
    /// <param name="subject">The subject to publish to</param>
    /// <param name="data">The data to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected async Task PublishAsync(string subject, byte[] data, CancellationToken cancellationToken = default)
    {
        await Default.PublishAsync(subject, data, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Publishes a message using the Default IJetStreamClient connection.
    /// This is a convenience method that delegates to Default.PublishAsync().
    /// </summary>
    /// <typeparam name="T">The type of data to publish</typeparam>
    /// <param name="subject">The subject to publish to</param>
    /// <param name="data">The data to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected async Task PublishAsync<T>(string subject, T data, CancellationToken cancellationToken = default)
    {
        await Default.PublishAsync(subject, data, cancellationToken: cancellationToken);
    }
}