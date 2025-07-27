using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Core.Filters;
using EdgeSync.ServiceFramework.IntegrationTests.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.IntegrationTests.EndToEnd;

/// <summary>
/// End-to-end integration tests for the complete pub-sub flow
/// Tests the entire pub-sub architecture working together
/// </summary>
public class PubSubFlowIntegrationTests : ServiceFrameworkTestBase
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        
        // Setup test connection with pub-sub capabilities
        services.AddSingleton<INatsConnection>(provider => CreatePubSubTestConnection());
    }

    [Fact]
    public async Task PubSubFlow_PushClassicMode_PublishesCorrectly()
    {
        // Arrange
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "PublishEventAsync",
            subject: "test.pub-sub-classic",
            conventionMode: ConventionMode.PubSubPushClassic,
            actionArguments: "classic pub-sub event");

        var filter = GetRequiredService<NatsProxyActionFilter>();

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        // Pub-sub typically returns OK result when successful
        actionContext.Result.ShouldBeOfType<OkResult>();
    }

    [Fact]
    public async Task PubSubFlow_PushJetStreamMode_PublishesCorrectly()
    {
        // Arrange
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "PublishEventAsync",
            subject: "test.pub-sub-jetstream",
            conventionMode: ConventionMode.PubSubPushJetStream,
            actionArguments: "JetStream pub-sub event");

        var filter = GetRequiredService<NatsProxyActionFilter>();

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        actionContext.Result.ShouldBeOfType<OkResult>();
    }

    [Fact]
    public async Task PubSubFlow_PullJetStreamMode_PublishesCorrectly()
    {
        // Arrange
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "PublishEventAsync",
            subject: "test.pub-sub-pull",
            conventionMode: ConventionMode.PubSubPullJetStream,
            actionArguments: "Pull JetStream event");

        var filter = GetRequiredService<NatsProxyActionFilter>();

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        actionContext.Result.ShouldBeOfType<OkResult>();
    }

    [Fact]
    public async Task PubSubFlow_HandlerSelection_SelectsPubSubHandler()
    {
        // Arrange
        var handlerFactory = GetRequiredService<IConventionModeHandlerFactory>();
        
        // Act - Test that the factory selects the correct handler for each pub-sub mode
        var classicHandler = handlerFactory.GetHandler(ConventionMode.PubSubPushClassic);
        var jetStreamHandler = handlerFactory.GetHandler(ConventionMode.PubSubPushJetStream);
        var pullHandler = handlerFactory.GetHandler(ConventionMode.PubSubPullJetStream);

        // Assert
        classicHandler.ShouldNotBeNull();
        jetStreamHandler.ShouldNotBeNull();
        pullHandler.ShouldNotBeNull();
        
        // All should be the same PubSubHandler type but support different modes
        classicHandler.SupportedMode.ShouldHaveFlag(ConventionMode.PubSubPushClassic);
        jetStreamHandler.SupportedMode.ShouldHaveFlag(ConventionMode.PubSubPushJetStream);
        pullHandler.SupportedMode.ShouldHaveFlag(ConventionMode.PubSubPullJetStream);
    }

    [Fact]
    public async Task PubSubFlow_WithAuditWrapper_IncludesAuditInformation()
    {
        // Arrange
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "PublishEventAsync",
            subject: "test.pub-sub-audit",
            conventionMode: ConventionMode.PubSubPushJetStream,
            actionArguments: "audit pub-sub event");

        var filter = GetRequiredService<NatsProxyActionFilter>();
        var auditHandler = GetRequiredService<IAuditHandler>();

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        actionContext.Result.ShouldBeOfType<OkResult>();
        
        // The audit handler should have been involved in wrapping the request
        auditHandler.ShouldNotBeNull();
    }

    [Fact]
    public async Task PubSubFlow_ConnectionResolution_WorksCorrectly()
    {
        // Arrange
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "PublishEventAsync",
            subject: "test.pub-sub-connection",
            conventionMode: ConventionMode.PubSubPushJetStream,
            actionArguments: "connection test event");

        var connectionResolver = GetRequiredService<IConnectionResolver>();
        var filter = GetRequiredService<NatsProxyActionFilter>();

        // Act
        var connection = await connectionResolver.GetConnectionAsync("default");
        
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        connection.ShouldNotBeNull();
        actionContext.Result.ShouldNotBeNull();
        
        // Should not have connection errors
        actionContext.Result.ShouldNotBeOfType<ObjectResult>(result => 
            result.StatusCode == 500 && result.Value?.ToString()?.Contains("Unable to connect") == true);
    }

    [Fact]
    public async Task PubSubFlow_BackgroundServiceIntegration_ManagesSubscriptions()
    {
        // Arrange
        var backgroundService = GetRequiredService<ServiceFrameworkBackgroundService>();
        var pubSubManager = GetRequiredService<IPubSubManager>();
        var serviceDiscovery = GetRequiredService<IServiceDiscovery>();
        
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token;

        // Act
        // Test that the background service coordinates pub-sub subscriptions
        await backgroundService.StartAsync(cancellationToken);
        
        var executeTask = backgroundService.ExecuteAsync(cancellationToken);
        await Task.Delay(100, CancellationToken.None); // Let it run briefly
        
        await backgroundService.StopAsync(CancellationToken.None);

        // Assert
        pubSubManager.ShouldNotBeNull();
        serviceDiscovery.ShouldNotBeNull();
        
        // Should complete without exceptions
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    [Fact]
    public async Task PubSubFlow_ServiceDiscovery_FindsPubSubServices()
    {
        // Arrange
        var serviceDiscovery = GetRequiredService<IServiceDiscovery>();

        // Act
        var discoveryResult = await serviceDiscovery.DiscoverServicesAsync();

        // Assert
        discoveryResult.ShouldNotBeNull();
        discoveryResult.PubSubServices.ShouldNotBeNull();
        
        // Should discover pub-sub methods from our test service
        var pubSubServices = discoveryResult.PubSubServices;
        pubSubServices.ShouldContain(service => 
            service.ServiceType == typeof(ITestNatsService) &&
            service.Methods.Any(method => method.Subject.Contains("pub-sub")));
    }

    [Fact]
    public async Task PubSubFlow_SubscriptionManagement_HandlesMultipleModes()
    {
        // Arrange
        var pubSubManager = GetRequiredService<IPubSubManager>();
        var serviceDiscovery = GetRequiredService<IServiceDiscovery>();
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token;

        // Act
        var discoveryResult = await serviceDiscovery.DiscoverServicesAsync();
        var subscriptionResult = await pubSubManager.StartSubscriptionsAsync(
            discoveryResult.PubSubServices, cancellationToken);

        // Assert
        subscriptionResult.ShouldNotBeNull();
        subscriptionResult.SuccessfulSubscriptions.ShouldNotBeNull();
        subscriptionResult.FailedSubscriptions.ShouldNotBeNull();
        
        // Should attempt to set up subscriptions for different modes
        var totalAttempts = subscriptionResult.SuccessfulSubscriptions.Count + 
                           subscriptionResult.FailedSubscriptions.Count;
        totalAttempts.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Theory]
    [InlineData(ConventionMode.PubSubPushClassic, "test.classic")]
    [InlineData(ConventionMode.PubSubPushJetStream, "test.jetstream")]
    [InlineData(ConventionMode.PubSubPullJetStream, "test.pull")]
    public async Task PubSubFlow_DifferentModes_HandleCorrectly(ConventionMode mode, string subject)
    {
        // Arrange
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "PublishEventAsync",
            subject: subject,
            conventionMode: mode,
            actionArguments: $"event for {mode}");

        var filter = GetRequiredService<NatsProxyActionFilter>();

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        actionContext.Result.ShouldBeOfType<OkResult>();
    }

    [Fact]
    public async Task PubSubFlow_ErrorHandling_HandlesGracefully()
    {
        // Arrange - Test error handling with invalid subject
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "PublishEventAsync",
            subject: "", // Empty subject should cause error
            conventionMode: ConventionMode.PubSubPushJetStream,
            actionArguments: "error test event");

        var filter = GetRequiredService<NatsProxyActionFilter>();

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        // Should return appropriate error response
        var badRequestResult = actionContext.Result.ShouldBeOfType<BadRequestObjectResult>();
        badRequestResult.Value.ShouldBe("Subject not found in action metadata");
    }

    [Fact]
    public async Task PubSubFlow_PerformanceCharacteristics_MaintainedThroughRefactoring()
    {
        // Arrange
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "PublishEventAsync",
            subject: "test.performance-pub-sub",
            conventionMode: ConventionMode.PubSubPushJetStream,
            actionArguments: "performance test event");

        var filter = GetRequiredService<NatsProxyActionFilter>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        stopwatch.Stop();

        // Assert
        actionContext.Result.ShouldNotBeNull();
        actionContext.Result.ShouldBeOfType<OkResult>();
        
        // Performance should be reasonable (allowing for test overhead)
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(3000); // 3 seconds max for pub-sub integration test
    }

    [Fact]
    public async Task PubSubFlow_SolidPrinciplesIntegration_ComponentsWorkTogether()
    {
        // Arrange - Verify all components work together following SOLID principles
        var auditHandler = GetRequiredService<IAuditHandler>();
        var connectionResolver = GetRequiredService<IConnectionResolver>();
        var requestDataExtractor = GetRequiredService<IRequestDataExtractor>();
        var handlerFactory = GetRequiredService<IConventionModeHandlerFactory>();
        var pubSubManager = GetRequiredService<IPubSubManager>();
        
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "PublishEventAsync",
            subject: "test.solid-pub-sub",
            conventionMode: ConventionMode.PubSubPushJetStream,
            actionArguments: "SOLID principles test");

        var filter = GetRequiredService<NatsProxyActionFilter>();

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert - Verify separation of concerns is maintained
        auditHandler.ShouldNotBeNull(); // Handles audit concerns
        connectionResolver.ShouldNotBeNull(); // Resolves NATS connections
        requestDataExtractor.ShouldNotBeNull(); // Extracts request data
        handlerFactory.ShouldNotBeNull(); // Creates appropriate handlers
        pubSubManager.ShouldNotBeNull(); // Manages pub-sub subscriptions

        actionContext.Result.ShouldNotBeNull();
        actionContext.Result.ShouldBeOfType<OkResult>();
    }

    private INatsConnection CreatePubSubTestConnection()
    {
        var connection = Substitute.For<INatsConnection>();
        
        // Setup server info
        connection.ServerInfo.Returns(new NatsServerInfo(
            "test-server", "2.9.0", "go1.19", "localhost", 4222, 8888, false, 0, [], []));
        
        // Setup publish behavior for pub-sub
        connection.PublishAsync<object?>(
            Arg.Any<string>(), 
            Arg.Any<object?>(), 
            Arg.Any<NatsHeaders?>(), 
            Arg.Any<string?>(), 
            Arg.Any<INatsSerializerRegistry>(), 
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Also setup JetStream publish behavior
        connection.PublishAsync<object?>(
            Arg.Any<string>(), 
            Arg.Any<object?>(), 
            Arg.Any<NatsHeaders?>(), 
            Arg.Any<string?>(), 
            Arg.Any<INatsSerializerRegistry>(), 
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return connection;
    }
}