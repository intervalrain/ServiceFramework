namespace EdgeSync.ServiceFramework.JetStream;

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
    /// <param name="name">The name of client to create</param>
    /// <returns>A JetStreamClient instance</returns>
    IJetStreamClient CreateClient(string name);
}