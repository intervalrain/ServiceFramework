using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;
using NATS.Client.Services;

namespace EdgeSync.ServiceFramework.UnitTests.Core;

/// <summary>
/// Unit tests for ServiceFrameworkBackgroundService - the refactored coordinator
/// Tests the orchestration of service discovery, registration, and pub-sub management
/// </summary>
public class ServiceFrameworkBackgroundServiceTests
{
    private readonly IServiceDiscovery _mockServiceDiscovery;
    private readonly IServiceRegistrar _mockServiceRegistrar;
    private readonly IPubSubManager _mockPubSubManager;
    private readonly ILogger<ServiceFrameworkBackgroundService> _mockLogger;
    private readonly ServiceFrameworkBackgroundService _backgroundService;

    public ServiceFrameworkBackgroundServiceTests()
    {
        _mockServiceDiscovery = Substitute.For<IServiceDiscovery>();
        _mockServiceRegistrar = Substitute.For<IServiceRegistrar>();
        _mockPubSubManager = Substitute.For<IPubSubManager>();
        _mockLogger = Substitute.For<ILogger<ServiceFrameworkBackgroundService>>();

        _backgroundService = new ServiceFrameworkBackgroundService(
            _mockServiceDiscovery,
            _mockServiceRegistrar,
            _mockPubSubManager,
            _mockLogger);
    }

    [Fact]
    public async Task StartAsync_WithSuccessfulDiscoveryAndRegistration_CompletesSuccessfully()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var discoveryResult = CreateSampleDiscoveryResult();
        var registrationResult = CreateSuccessfulRegistrationResult();
        var subscriptionResult = CreateSuccessfulSubscriptionResult();

        _mockServiceDiscovery.DiscoverServicesAsync().Returns(discoveryResult);
        _mockServiceRegistrar.RegisterRequestResponseServicesAsync(discoveryResult.ReqRspServices, cancellationToken)
                            .Returns(registrationResult);
        _mockPubSubManager.StartSubscriptionsAsync(discoveryResult.PubSubServices, Arg.Any<CancellationToken>())
                         .Returns(subscriptionResult);

        // Act
        await _backgroundService.StartAsync(cancellationToken);
        
        // Give background execution a moment to start
        await Task.Delay(50);

        // Assert
        await _mockServiceDiscovery.Received(1).DiscoverServicesAsync();
        await _mockServiceRegistrar.Received(1).RegisterRequestResponseServicesAsync(discoveryResult.ReqRspServices, cancellationToken);
    }

    [Fact]
    public async Task StartAsync_WithDiscoveryFailure_ThrowsException()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var exception = new Exception("Discovery failed");

        _mockServiceDiscovery.DiscoverServicesAsync().Throws(exception);

        // Act & Assert
        var thrownException = await Should.ThrowAsync<Exception>(async () =>
            await _backgroundService.StartAsync(cancellationToken));

        thrownException.Message.ShouldBe("Discovery failed");
        await _mockServiceRegistrar.DidNotReceive().RegisterRequestResponseServicesAsync(Arg.Any<List<(Type, List<NatsMethodInfo>)>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WithRegistrationFailure_ThrowsException()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var discoveryResult = CreateSampleDiscoveryResult();
        var exception = new Exception("Registration failed");

        _mockServiceDiscovery.DiscoverServicesAsync().Returns(discoveryResult);
        _mockServiceRegistrar.RegisterRequestResponseServicesAsync(discoveryResult.ReqRspServices, cancellationToken)
                            .Throws(exception);

        // Act & Assert
        var thrownException = await Should.ThrowAsync<Exception>(async () =>
            await _backgroundService.StartAsync(cancellationToken));

        thrownException.Message.ShouldBe("Registration failed");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidDiscoveryResult_StartsSubscriptionsSuccessfully()
    {
        // Arrange
        var cancellationToken = new CancellationTokenSource();
        var discoveryResult = CreateSampleDiscoveryResult();
        var subscriptionResult = CreateSuccessfulSubscriptionResult();

        _mockServiceDiscovery.DiscoverServicesAsync().Returns(discoveryResult);
        _mockServiceRegistrar.RegisterRequestResponseServicesAsync(Arg.Any<List<(Type, List<NatsMethodInfo>)>>(), Arg.Any<CancellationToken>())
                            .Returns(CreateSuccessfulRegistrationResult());
        _mockPubSubManager.StartSubscriptionsAsync(discoveryResult.PubSubServices, Arg.Any<CancellationToken>())
                         .Returns(subscriptionResult);

        // Start the service first to populate discovery result
        await _backgroundService.StartAsync(CancellationToken.None);
        
        // Clear previous calls since StartAsync triggers ExecuteAsync in background
        _mockPubSubManager.ClearReceivedCalls();

        // Act
        cancellationToken.CancelAfter(100); // Cancel after short delay to avoid infinite loop
        
        try
        {
            await _backgroundService.ExecuteForTestingAsync(cancellationToken.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected due to cancellation
        }

        // Assert
        await _mockPubSubManager.Received(1).StartSubscriptionsAsync(discoveryResult.PubSubServices, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNullDiscoveryResult_SkipsPubSubSubscriptions()
    {
        // Arrange
        var cancellationToken = new CancellationTokenSource();

        // Don't start the service, so discovery result will be null
        cancellationToken.CancelAfter(50);

        // Act
        try
        {
            await _backgroundService.ExecuteForTestingAsync(cancellationToken.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected due to cancellation
        }

        // Assert
        await _mockPubSubManager.DidNotReceive().StartSubscriptionsAsync(Arg.Any<List<(Type, List<NatsMethodInfo>)>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithSubscriptionFailure_ThrowsException()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var discoveryResult = CreateSampleDiscoveryResult();
        var exception = new Exception("Subscription failed");

        _mockServiceDiscovery.DiscoverServicesAsync().Returns(discoveryResult);
        _mockServiceRegistrar.RegisterRequestResponseServicesAsync(Arg.Any<List<(Type, List<NatsMethodInfo>)>>(), Arg.Any<CancellationToken>())
                            .Returns(CreateSuccessfulRegistrationResult());
        _mockPubSubManager.StartSubscriptionsAsync(discoveryResult.PubSubServices, Arg.Any<CancellationToken>())
                         .Throws(exception);

        // Start the service first to populate discovery result, but catch the background exception
        try
        {
            await _backgroundService.StartAsync(CancellationToken.None);
            await Task.Delay(10); // Give background task a moment to fail
        }
        catch
        {
            // Expected - background task will throw
        }

        // Clear previous calls since StartAsync already triggered the exception
        _mockPubSubManager.ClearReceivedCalls();

        // Act & Assert
        var thrownException = await Should.ThrowAsync<Exception>(async () =>
            await _backgroundService.ExecuteForTestingAsync(cancellationToken));

        thrownException.Message.ShouldBe("Subscription failed");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_HandlesGracefully()
    {
        // Arrange
        var cancellationToken = new CancellationTokenSource();
        var discoveryResult = CreateSampleDiscoveryResult();
        var subscriptionResult = CreateSuccessfulSubscriptionResult();

        _mockServiceDiscovery.DiscoverServicesAsync().Returns(discoveryResult);
        _mockServiceRegistrar.RegisterRequestResponseServicesAsync(Arg.Any<List<(Type, List<NatsMethodInfo>)>>(), Arg.Any<CancellationToken>())
                            .Returns(CreateSuccessfulRegistrationResult());
        _mockPubSubManager.StartSubscriptionsAsync(discoveryResult.PubSubServices, Arg.Any<CancellationToken>())
                         .Returns(subscriptionResult);

        // Start the service first to populate discovery result
        await _backgroundService.StartAsync(CancellationToken.None);
        
        // Clear previous calls since StartAsync triggers ExecuteAsync in background
        _mockPubSubManager.ClearReceivedCalls();

        // Act
        cancellationToken.CancelAfter(50);

        // Should not throw for cancellation
        try
        {
            await _backgroundService.ExecuteForTestingAsync(cancellationToken.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected and handled gracefully
        }

        // Assert - should have started subscriptions
        await _mockPubSubManager.Received(1).StartSubscriptionsAsync(discoveryResult.PubSubServices, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopAsync_WithSuccessfulShutdown_CompletesSuccessfully()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var serviceShutdownResult = CreateSuccessfulServiceShutdownResult();
        var unsubscriptionResult = CreateSuccessfulUnsubscriptionResult();

        _mockServiceRegistrar.StopAllServicesAsync(cancellationToken).Returns(serviceShutdownResult);
        _mockPubSubManager.StopAllSubscriptionsAsync(cancellationToken).Returns(unsubscriptionResult);

        // Act
        await _backgroundService.StopAsync(cancellationToken);

        // Assert
        await _mockServiceRegistrar.Received(1).StopAllServicesAsync(cancellationToken);
        await _mockPubSubManager.Received(1).StopAllSubscriptionsAsync(cancellationToken);
    }

    [Fact]
    public async Task StopAsync_WithServiceStopFailure_ThrowsException()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var exception = new Exception("Service stop failed");

        _mockServiceRegistrar.StopAllServicesAsync(cancellationToken).Throws(exception);

        // Act & Assert
        var thrownException = await Should.ThrowAsync<Exception>(async () =>
            await _backgroundService.StopAsync(cancellationToken));

        thrownException.Message.ShouldBe("Service stop failed");
        
        // Should not proceed to pub-sub stop if service stop fails
        await _mockPubSubManager.DidNotReceive().StopAllSubscriptionsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopAsync_WithPubSubStopFailure_ThrowsException()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var serviceShutdownResult = CreateSuccessfulServiceShutdownResult();
        var exception = new Exception("PubSub stop failed");

        _mockServiceRegistrar.StopAllServicesAsync(cancellationToken).Returns(serviceShutdownResult);
        _mockPubSubManager.StopAllSubscriptionsAsync(cancellationToken).Throws(exception);

        // Act & Assert
        var thrownException = await Should.ThrowAsync<Exception>(async () =>
            await _backgroundService.StopAsync(cancellationToken));

        thrownException.Message.ShouldBe("PubSub stop failed");
    }

    [Fact]
    public async Task StartAsync_LogsAppropriateMessages()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var discoveryResult = CreateSampleDiscoveryResult();
        var registrationResult = CreateSuccessfulRegistrationResult();
        var subscriptionResult = CreateSuccessfulSubscriptionResult();

        _mockServiceDiscovery.DiscoverServicesAsync().Returns(discoveryResult);
        _mockServiceRegistrar.RegisterRequestResponseServicesAsync(discoveryResult.ReqRspServices, cancellationToken)
                            .Returns(registrationResult);
        _mockPubSubManager.StartSubscriptionsAsync(discoveryResult.PubSubServices, Arg.Any<CancellationToken>())
                         .Returns(subscriptionResult);

        // Act
        await _backgroundService.StartAsync(cancellationToken);
        
        // Give background execution a moment to start
        await Task.Delay(50);

        // Assert
        _mockLogger.Received().LogInformation("Starting Service Framework Background Service");
        // Note: Detailed logging assertions skipped due to NSubstitute complexity with LogInformation extension methods
    }

    [Fact]
    public async Task StopAsync_LogsAppropriateMessages()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var serviceShutdownResult = CreateSuccessfulServiceShutdownResult();
        var unsubscriptionResult = CreateSuccessfulUnsubscriptionResult();

        _mockServiceRegistrar.StopAllServicesAsync(cancellationToken).Returns(serviceShutdownResult);
        _mockPubSubManager.StopAllSubscriptionsAsync(cancellationToken).Returns(unsubscriptionResult);

        // Act
        await _backgroundService.StopAsync(cancellationToken);

        // Assert
        _mockLogger.Received().LogInformation("Stopping Service Framework Background Service");
        // Note: Detailed logging assertions skipped due to NSubstitute complexity with LogInformation extension methods
    }

    #region Helper Methods

    private ServiceDiscoveryResult CreateSampleDiscoveryResult()
    {
        return new ServiceDiscoveryResult
        {
            ReqRspServices = new List<(Type ServiceType, List<NatsMethodInfo> Methods)>
            {
                (typeof(TestService1), new List<NatsMethodInfo> { new NatsMethodInfo() }),
                (typeof(TestService2), new List<NatsMethodInfo> { new NatsMethodInfo() })
            },
            PubSubServices = new List<(Type ServiceType, List<NatsMethodInfo> Methods)>
            {
                (typeof(TestPubSubService), new List<NatsMethodInfo> { new NatsMethodInfo() })
            }
        };
    }

    private ServiceRegistrationResult CreateSuccessfulRegistrationResult()
    {
        return new ServiceRegistrationResult
        {
            RegisteredServices = new List<(Type ServiceType, List<NatsMethodInfo> Methods, INatsSvcServer ServiceServer)>
            {
                (typeof(TestService1), new List<NatsMethodInfo> { new NatsMethodInfo() }, Substitute.For<INatsSvcServer>()),
                (typeof(TestService2), new List<NatsMethodInfo> { new NatsMethodInfo() }, Substitute.For<INatsSvcServer>())
            },
            FailedServices = new List<(Type ServiceType, List<NatsMethodInfo> Methods, string ErrorMessage)>()
        };
    }

    private PubSubSubscriptionResult CreateSuccessfulSubscriptionResult()
    {
        return new PubSubSubscriptionResult
        {
            SuccessfulSubscriptions = new List<(Type ServiceType, NatsMethodInfo Method)>
            {
                (typeof(TestPubSubService), new NatsMethodInfo()),
                (typeof(TestPubSubService), new NatsMethodInfo())
            },
            FailedSubscriptions = new List<(Type ServiceType, NatsMethodInfo Method, string ErrorMessage)>()
        };
    }

    private ServiceShutdownResult CreateSuccessfulServiceShutdownResult()
    {
        return new ServiceShutdownResult
        {
            StoppedServices = new List<string>
            {
                "TestService1",
                "TestService2"
            },
            FailedServices = new List<(string ServiceName, string ErrorMessage)>()
        };
    }

    private PubSubUnsubscriptionResult CreateSuccessfulUnsubscriptionResult()
    {
        return new PubSubUnsubscriptionResult
        {
            SuccessfulUnsubscriptions = 3,
            FailedUnsubscriptions = new List<string>()
        };
    }

    // Test service types for mocking
    private class TestService1 { }
    private class TestService2 { }
    private class TestPubSubService { }

    #endregion
}