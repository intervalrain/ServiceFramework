using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.JetStream;


/// <summary>
/// Factory for creating JetStream clients
/// </summary>
public class JetStreamClientFactory : IJetStreamClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly INatsConnectionFactory _natsConnectionFactory;

    /// <summary>
    /// Initializes a new instance of the JetStreamClientFactory class
    /// </summary>
    /// <param name="loggerFactory">The logger factory</param>
    /// <param name="natsConnectionFactory">The NATS connection factory</param>
    public JetStreamClientFactory(ILoggerFactory loggerFactory, INatsConnectionFactory natsConnectionFactory)
    {
        _loggerFactory = loggerFactory;
        _natsConnectionFactory = natsConnectionFactory;
    }

    /// <summary>
    /// Creates a Message Broker JetStream client
    /// </summary>
    /// <returns>A new MsgBrokerJetStreamClient instance</returns>
    public IBrokerJetStreamClient CreateMsgBrokerClient()
    {
        var logger = _loggerFactory.CreateLogger<JetStreamClient>();
        var client = new MsgBrokerJetStreamClient(logger, _natsConnectionFactory);
        // client.TryConnect().Wait();
        return client;
    }

    /// <summary>
    /// Creates a Message Bus JetStream client
    /// </summary>
    /// <returns>A new MsgBusJetStreamClient instance</returns>
    public IBusJetStreamClient CreateMsgBusClient()
    {
        var logger = _loggerFactory.CreateLogger<JetStreamClient>();
        var client = new MsgBusJetStreamClient(logger, _natsConnectionFactory);
        // client.TryConnect().Wait();
        return client;
    }

    /// <summary>
    /// Creates the appropriate JetStream client based on the client type
    /// </summary>
    /// <param name="clientType">The type of client to create</param>
    /// <returns>A JetStreamClient instance</returns>
    /// <exception cref="ArgumentException">Thrown when an invalid client type is provided</exception>
    public IJetStreamClient CreateClient(JetStreamClientType clientType)
    {
        return clientType switch
        {
            JetStreamClientType.MsgBroker => CreateMsgBrokerClient(),
            JetStreamClientType.MsgBus => CreateMsgBusClient(),
            _ => throw new ArgumentException($"Unsupported client type: {clientType}", nameof(clientType))
        };
    }
}