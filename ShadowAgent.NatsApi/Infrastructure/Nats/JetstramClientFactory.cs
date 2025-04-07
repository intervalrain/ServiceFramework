using Microsoft.Extensions.Logging;
using ShadowAgent.Infrastructure.Interfaces;

namespace ShadowAgent.Infrastructure.Nats
{
    /// <summary>
    /// Interface for the JetStream client factory
    /// </summary>
    public interface IJetStreamClientFactory
    {
        /// <summary>
        /// Creates a Message Broker JetStream client
        /// </summary>
        /// <returns>A new MsgBrokerJetStreamClient instance</returns>
        IBrokerJetStreamClient CreateMsgBrokerClient();
        
        /// <summary>
        /// Creates a Message Bus JetStream client
        /// </summary>
        /// <returns>A new MsgBusJetStreamClient instance</returns>
        IBusJetStreamClient CreateMsgBusClient();
        
        /// <summary>
        /// Creates the appropriate JetStream client based on the client type
        /// </summary>
        /// <param name="clientType">The type of client to create</param>
        /// <returns>A JetStreamClient instance</returns>
        IJetStreamClient CreateClient(JetStreamClientType clientType);
    }

    /// <summary>
    /// Enum defining the available JetStream client types
    /// </summary>
    public enum JetStreamClientType
    {
        MsgBroker,
        MsgBus
    }

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
        /// <exception cref="System.ArgumentException">Thrown when an invalid client type is provided</exception>
        public IJetStreamClient CreateClient(JetStreamClientType clientType)
        {
            return clientType switch
            {
                JetStreamClientType.MsgBroker => CreateMsgBrokerClient(),
                JetStreamClientType.MsgBus => CreateMsgBusClient(),
                _ => throw new System.ArgumentException($"Unsupported client type: {clientType}", nameof(clientType))
            };
        }
    }
}