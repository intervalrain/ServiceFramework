using Microsoft.Extensions.Hosting;

using ShadowAgent.Infrastructure.Interfaces;

namespace ShadowAgent.Infrastructure.Base;

public abstract class MessageTransportBase(
    IBrokerJetStreamClient broker,
    IBusJetStreamClient bus)
    : BackgroundService
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