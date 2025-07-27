using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Handlers;
using EdgeSync.ServiceFramework.Core.Filters;
using EdgeSync.ServiceFramework.IntegrationTests.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.Services;

namespace EdgeSync.ServiceFramework.IntegrationTests.ErrorHandling;

/// <summary>
/// Integration tests for error handling and edge cases across integrated components
/// Tests the robustness of the refactored architecture under various failure scenarios
/// </summary>
public class ErrorHandlingIntegrationTests : ServiceFrameworkTestBase
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        
        // Setup test connection that can simulate various error scenarios
        services.AddSingleton<INatsConnection>(provider => CreateErrorTestConnection());
    }

    [Fact]
    public async Task ErrorHandling_MissingSubject_ReturnsAppropriateError()
    {
        // Arrange
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "", // Empty subject
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "test input");

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        var badRequestResult = actionContext.Result.ShouldBeOfType<BadRequestObjectResult>();
        badRequestResult.Value.ShouldBe("Subject not found in action metadata");
    }

    [Fact]
    public async Task ErrorHandling_UnsupportedConventionMode_ReturnsAppropriateError()
    {
        // Arrange
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.unsupported",
            conventionMode: (ConventionMode)999, // Invalid mode
            actionArguments: "test input");

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        var badRequestResult = actionContext.Result.ShouldBeOfType<BadRequestObjectResult>();
        badRequestResult.Value.ShouldNotBeNull();
        badRequestResult.Value.ToString().ShouldContain("Unsupported convention mode");
    }

    [Fact]
    public async Task ErrorHandling_ConnectionFailure_ReturnsServerError()
    {
        // Arrange - Use a failing connection resolver
        var services = new ServiceCollection();
        ConfigureTestServices(services);
        services.AddSingleton<IConnectionResolver>(provider => new FailingConnectionResolver());
        
        using var testProvider = services.BuildServiceProvider();
        var filter = testProvider.GetRequiredService<NatsProxyActionFilter>();
        
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.connection-failure",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "test input");

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        var objectResult = actionContext.Result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(500);
        objectResult.Value.ShouldNotBeNull();
        objectResult.Value.ToString().ShouldContain("Unable to connect to NATS");
    }

    [Fact]
    public async Task ErrorHandling_NatsTimeout_ReturnsTimeoutError()
    {
        // Arrange - Use a connection that simulates timeout
        var services = new ServiceCollection();
        ConfigureTestServices(services);
        services.AddSingleton<INatsConnection>(provider => CreateTimeoutConnection());
        
        using var testProvider = services.BuildServiceProvider();
        var filter = testProvider.GetRequiredService<NatsProxyActionFilter>();
        
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.timeout",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "timeout test");

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        var objectResult = actionContext.Result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(408);
        objectResult.Value.ShouldBe("Request timeout");
    }

    [Fact]
    public async Task ErrorHandling_HandlerException_ReturnsServerError()
    {
        // Arrange - Use a handler that throws exceptions
        var services = new ServiceCollection();
        ConfigureTestServices(services);
        services.AddScoped<IConventionModeHandler>(provider => new ExceptionThrowingHandler());
        services.AddScoped<IConventionModeHandlerFactory>(provider => 
            new ExceptionHandlerFactory(provider.GetServices<IConventionModeHandler>(), 
                provider.GetRequiredService<ILogger<ConventionModeHandlerFactory>>()));
        
        using var testProvider = services.BuildServiceProvider();
        var filter = testProvider.GetRequiredService<NatsProxyActionFilter>();
        
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.handler-exception",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "exception test");

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        var objectResult = actionContext.Result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(500);
        objectResult.Value.ShouldNotBeNull();
        objectResult.Value.ToString().ShouldContain("Internal server error");
    }

    [Fact]
    public async Task ErrorHandling_AuditHandlerFailure_ContinuesProcessing()
    {
        // Arrange - Use an audit handler that fails
        var services = new ServiceCollection();
        ConfigureTestServices(services);
        services.AddScoped<IAuditHandler>(provider => new FailingAuditHandler());
        
        using var testProvider = services.BuildServiceProvider();
        var filter = testProvider.GetRequiredService<NatsProxyActionFilter>();
        
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.audit-failure",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "audit failure test");

        // Act & Assert - Should handle audit failure gracefully
        await Should.NotThrowAsync(async () =>
        {
            await filter.OnActionExecutionAsync(actionContext, 
                () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));
        });
    }

    [Fact]
    public async Task ErrorHandling_ServiceDiscoveryFailure_HandledGracefully()
    {
        // Arrange
        var services = new ServiceCollection();
        ConfigureTestServices(services);
        services.AddSingleton<IServiceDiscovery>(provider => new FailingServiceDiscovery());
        
        using var testProvider = services.BuildServiceProvider();
        var backgroundService = testProvider.GetRequiredService<ServiceFrameworkBackgroundService>();
        
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token;

        // Act & Assert - Should handle service discovery failure gracefully
        await Should.NotThrowAsync(async () =>
        {
            await backgroundService.StartAsync(cancellationToken);
        });
    }

    [Fact]
    public async Task ErrorHandling_ServiceRegistrationFailure_ContinuesOperation()
    {
        // Arrange
        var services = new ServiceCollection();
        ConfigureTestServices(services);
        services.AddSingleton<IServiceRegistrar>(provider => new FailingServiceRegistrar());
        
        using var testProvider = services.BuildServiceProvider();
        var backgroundService = testProvider.GetRequiredService<ServiceFrameworkBackgroundService>();
        
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token;

        // Act & Assert - Should handle service registration failure gracefully
        await Should.NotThrowAsync(async () =>
        {
            await backgroundService.StartAsync(cancellationToken);
        });
    }

    [Fact]
    public async Task ErrorHandling_PubSubManagerFailure_HandledGracefully()
    {
        // Arrange
        var services = new ServiceCollection();
        ConfigureTestServices(services);
        services.AddSingleton<IPubSubManager>(provider => new FailingPubSubManager());
        
        using var testProvider = services.BuildServiceProvider();
        var backgroundService = testProvider.GetRequiredService<ServiceFrameworkBackgroundService>();
        
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token;

        // Act & Assert - Should handle pub-sub manager failure gracefully
        await Should.NotThrowAsync(async () =>
        {
            await backgroundService.StartAsync(cancellationToken);
            var executeTask = backgroundService.ExecuteAsync(cancellationToken);
            await Task.Delay(50, CancellationToken.None);
            await backgroundService.StopAsync(CancellationToken.None);
            
            try
            {
                await executeTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        });
    }

    [Fact]
    public void ErrorHandling_FactoryWithNoHandlers_ThrowsAppropriateError()
    {
        // Arrange - Create factory with no handlers
        var emptyHandlers = new List<IConventionModeHandler>();
        var logger = Substitute.For<ILogger<ConventionModeHandlerFactory>>();
        var factory = new ConventionModeHandlerFactory(emptyHandlers, logger);

        // Act & Assert
        var exception = Should.Throw<NotSupportedException>(() => 
            factory.GetHandler(ConventionMode.RequestResponse));
            
        exception.Message.ShouldContain("No handler found for convention mode");
    }

    [Fact]
    public async Task ErrorHandling_NullRequestData_HandledCorrectly()
    {
        // Arrange
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.null-request",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: null); // Null request data

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        // Should handle null request data without throwing
        actionContext.Result.ShouldNotBeOfType<ObjectResult>(result => result.StatusCode >= 500);
    }

    [Fact]
    public async Task ErrorHandling_MalformedActionContext_HandledGracefully()
    {
        // Arrange
        var filter = GetRequiredService<NatsProxyActionFilter>();
        
        // Create malformed action context (missing required properties)
        var actionContext = TestFixtures.CreateActionExecutingContext();
        actionContext.ActionDescriptor.Properties.Clear(); // Remove all metadata

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        // Should handle malformed context appropriately
        var badRequestResult = actionContext.Result.ShouldBeOfType<BadRequestObjectResult>();
        badRequestResult.Value.ShouldBe("Subject not found in action metadata");
    }

    [Theory]
    [InlineData(ConventionMode.RequestResponse)]
    [InlineData(ConventionMode.PubSubPushClassic)]
    [InlineData(ConventionMode.PubSubPushJetStream)]
    [InlineData(ConventionMode.PubSubPullJetStream)]
    public async Task ErrorHandling_ValidModesWithNatsErrors_HandledAppropriately(ConventionMode mode)
    {
        // Arrange - Use connection that throws NATS errors
        var services = new ServiceCollection();
        ConfigureTestServices(services);
        services.AddSingleton<INatsConnection>(provider => CreateNatsErrorConnection());
        
        using var testProvider = services.BuildServiceProvider();
        var filter = testProvider.GetRequiredService<NatsProxyActionFilter>();
        
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: mode == ConventionMode.RequestResponse ? "ProcessRequestAsync" : "PublishEventAsync",
            subject: $"test.nats-error.{mode}",
            conventionMode: mode,
            actionArguments: "nats error test");

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        // Should return server error for NATS issues
        var objectResult = actionContext.Result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(500);
    }

    private INatsConnection CreateErrorTestConnection()
    {
        var connection = Substitute.For<INatsConnection>();
        
        connection.ServerInfo.Returns(new NatsServerInfo(
            "test-server", "2.9.0", "go1.19", "localhost", 4222, 8888, false, 0, [], []));
        
        // Setup normal behavior by default
        connection.RequestAsync<object?, object?>(
            Arg.Any<string>(), 
            Arg.Any<object?>(), 
            Arg.Any<INatsSerializerRegistry>(), 
            Arg.Any<NatsRequestOpts?>(), 
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<object?>("test response"));

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

    private INatsConnection CreateTimeoutConnection()
    {
        var connection = Substitute.For<INatsConnection>();
        
        connection.ServerInfo.Returns(new NatsServerInfo(
            "test-server", "2.9.0", "go1.19", "localhost", 4222, 8888, false, 0, [], []));
        
        // Setup timeout behavior
        connection.RequestAsync<object?, object?>(
            Arg.Any<string>(), 
            Arg.Any<object?>(), 
            Arg.Any<INatsSerializerRegistry>(), 
            Arg.Any<NatsRequestOpts?>(), 
            Arg.Any<CancellationToken>())
            .Returns<object?>(callInfo => throw new TimeoutException("NATS request timeout"));

        return connection;
    }

    private INatsConnection CreateNatsErrorConnection()
    {
        var connection = Substitute.For<INatsConnection>();
        
        connection.ServerInfo.Returns(new NatsServerInfo(
            "test-server", "2.9.0", "go1.19", "localhost", 4222, 8888, false, 0, [], []));
        
        // Setup to throw NATS-related errors
        connection.RequestAsync<object?, object?>(
            Arg.Any<string>(), 
            Arg.Any<object?>(), 
            Arg.Any<INatsSerializerRegistry>(), 
            Arg.Any<NatsRequestOpts?>(), 
            Arg.Any<CancellationToken>())
            .Returns<object?>(callInfo => throw new InvalidOperationException("NATS connection error"));

        connection.PublishAsync<object?>(
            Arg.Any<string>(), 
            Arg.Any<object?>(), 
            Arg.Any<NatsHeaders?>(), 
            Arg.Any<string?>(), 
            Arg.Any<INatsSerializerRegistry>(), 
            Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("NATS publish error")));

        return connection;
    }
}

// Test implementations for error scenarios
public class FailingConnectionResolver : IConnectionResolver
{
    public Task<INatsConnection?> GetConnectionAsync(string? channelName)
    {
        return Task.FromResult<INatsConnection?>(null); // Simulate connection failure
    }

    public INatsSerializerRegistry GetSerializerForConnection(string? channelName)
    {
        throw new InvalidOperationException("Serializer not available");
    }
}

public class ExceptionThrowingHandler : IConventionModeHandler
{
    public ConventionMode SupportedMode => ConventionMode.RequestResponse;

    public Task<IActionResult> HandleAsync(ActionExecutingContext context, INatsConnection connection, 
        ActionContextMetadata metadata, object? wrappedRequest)
    {
        throw new InvalidOperationException("Handler exception for testing");
    }
}

public class ExceptionHandlerFactory : IConventionModeHandlerFactory
{
    private readonly IEnumerable<IConventionModeHandler> _handlers;
    private readonly ILogger _logger;

    public ExceptionHandlerFactory(IEnumerable<IConventionModeHandler> handlers, ILogger logger)
    {
        _handlers = handlers;
        _logger = logger;
    }

    public IConventionModeHandler GetHandler(ConventionMode mode)
    {
        return new ExceptionThrowingHandler();
    }

    public void RegisterHandler(IConventionModeHandler handler)
    {
        // Test implementation
    }
}

public class FailingAuditHandler : IAuditHandler
{
    public object? WrapRequestWithAudit(object? requestData, Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        throw new InvalidOperationException("Audit handler failure");
    }

    public (string ReqSeqId, string Timestamp)? ExtractAuditInfo(object? wrappedRequest)
    {
        return null;
    }

    public (string ReqSeqId, string RspSeqId, string Timestamp)? ExtractResponseAuditInfo(object? response)
    {
        return null;
    }
}

public class FailingServiceDiscovery : IServiceDiscovery
{
    public Task<ServiceDiscoveryResult> DiscoverServicesAsync()
    {
        throw new InvalidOperationException("Service discovery failure");
    }
}

public class FailingServiceRegistrar : IServiceRegistrar
{
    public Task<ServiceRegistrationResult> RegisterRequestResponseServicesAsync(
        List<(Type ServiceType, List<NatsMethodInfo> Methods)> reqrspServices, 
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Service registration failure");
    }

    public Task<INatsSvcServer> CreateSvcServer(string serviceName, List<NatsMethodInfo> methods, INatsConnection connection, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Service server creation failure");
    }

    public Task SetupRequestResponseServiceGroup(INatsConnection connection, INatsSvcServer svcServer, Type serviceType, string serviceName, List<NatsMethodInfo> methods, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Service group setup failure");
    }

    public Task<ServiceShutdownResult> StopAllServicesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new ServiceShutdownResult 
        { 
            StoppedServices = new List<string>(), 
            FailedServices = new List<(string ServiceName, string ErrorMessage)>() 
        });
    }
}

public class FailingPubSubManager : IPubSubManager
{
    public Task<PubSubSubscriptionResult> StartSubscriptionsAsync(
        List<(Type ServiceType, List<NatsMethodInfo> Methods)> pubsubServices, 
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("PubSub manager failure");
    }

    public Task<PubSubUnsubscriptionResult> StopAllSubscriptionsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new PubSubUnsubscriptionResult 
        { 
            SuccessfulUnsubscriptions = 0, 
            FailedUnsubscriptions = new List<string>() 
        });
    }
}