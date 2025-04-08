using EdgeSync.ServiceFramework.JetStream;

using Microsoft.Extensions.Hosting;

namespace EdgeSync.ServiceFramework;

public abstract class MessageTransportBase(IBrokerJetStreamClient broker, IBusJetStreamClient bus) : BackgroundService
{
    /// <summary>
    /// JetStream client for device communication.
    /// Handles message exchange with IoT devices and hardware endpoints.
    /// </summary>
    protected IBrokerJetStreamClient Broker { get; } = broker;


    /// <summary>
    /// JetStream client for client-side communication.
    /// Manages message exchange with user applications and frontend services.
    /// </summary>
    protected IBusJetStreamClient Bus { get; } = bus;
}