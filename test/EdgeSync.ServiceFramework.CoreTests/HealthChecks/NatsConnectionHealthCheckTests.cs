using EdgeSync.ServiceFramework.Core.HealthChecks;
using EdgeSync.ServiceFramework.JetStream;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace EdgeSync.ServiceFramework.CoreTests.HealthChecks;

public class NatsConnectionHealthCheckTests
{
    private readonly ILogger<NatsBusConnectionHealthCheck> _mockBusLogger;
    private readonly ILogger<NatsBrokerConnectionHealthCheck> _mockBrokerLogger;
    private readonly ILogger<NatsConnectionHealthCheck> _mockLogger;
    private readonly IJetStreamClientFactory _mockFactory;
    private readonly IBusJetStreamClient _mockBus;
    private readonly IBrokerJetStreamClient _mockBroker;
    private readonly IBusJetStreamClient _mockNatsConnection;

    public NatsConnectionHealthCheckTests()
    {
        _mockBusLogger = Substitute.For<ILogger<NatsBusConnectionHealthCheck>>();
        _mockBrokerLogger = Substitute.For<ILogger<NatsBrokerConnectionHealthCheck>>();
        _mockLogger = Substitute.For<ILogger<NatsConnectionHealthCheck>>();

        _mockFactory = Substitute.For<IJetStreamClientFactory>();
        _mockBus = Substitute.For<IBusJetStreamClient>();
        _mockBroker = Substitute.For<IBrokerJetStreamClient>();
        _mockNatsConnection = Substitute.For<IBusJetStreamClient>();

        _mockFactory.CreateMsgBrokerClient().Returns(_mockBroker);
        _mockFactory.CreateMsgBusClient().Returns(_mockBus);
        _mockFactory.CreateClient("other").Returns(_mockNatsConnection);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnected_ReturnsHealthy()
    {
        // Arrange
        _mockBus.IsConnected().Returns(true);
        var healthCheck = new NatsBusConnectionHealthCheck(_mockBusLogger, _mockFactory);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldBe("Bus: OK");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDisconnected_ReturnsUnhealthy()
    {
        // Arrange
        _mockBroker.IsConnected().Returns(false);
        var healthCheck = new NatsBrokerConnectionHealthCheck(_mockBrokerLogger, _mockFactory);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldBe("Bus: Connection lost");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenThrowException_ReturnsUnhealthy()
    {
        // Arrange
        _mockNatsConnection.IsConnected().Throws<Exception>();
        var healthCheck = new NatsConnectionHealthCheck(_mockLogger, _mockFactory, "other");

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldBe("Bus: Connection check failed");
    }
}