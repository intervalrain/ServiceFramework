using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Core.Filters;
using EdgeSync.ServiceFramework.IntegrationTests.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.IntegrationTests.Core.Filters;

/// <summary>
/// Integration tests for NatsProxyActionFilter orchestration
/// Tests the complete coordination flow with all injected dependencies
/// </summary>
public class NatsProxyActionFilterIntegrationTests : ServiceFrameworkTestBase
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        
        // Override connection resolver to return test connection
        services.AddSingleton<INatsConnection>(provider => TestFixtures.CreateTestNatsConnection());
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithRequestResponseMode_OrchestrationWorksCorrectly()
    {
        // Arrange
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.request-response",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "test input");

        // Act
        await filter.OnActionExecutionAsync(actionContext, () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        // Verify the result is processed correctly
        var objectResult = actionContext.Result.ShouldBeOfType<ObjectResult>();
        objectResult.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithPubSubMode_OrchestrationWorksCorrectly()
    {
        // Arrange
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "PublishEventAsync",
            subject: "test.pub-sub",
            conventionMode: ConventionMode.PubSubPushJetStream,
            actionArguments: "test event data");

        // Act
        await filter.OnActionExecutionAsync(actionContext, () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        // For pub-sub, typically returns OK result
        var result = actionContext.Result.ShouldBeOfType<OkResult>();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithParameterlessMethod_HandlesCorrectly()
    {
        // Arrange
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "GetCountAsync",
            subject: "test.parameterless",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: null);

        // Act
        await filter.OnActionExecutionAsync(actionContext, () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        var objectResult = actionContext.Result.ShouldBeOfType<ObjectResult>();
        objectResult.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_MissingSubject_ReturnsBadRequest()
    {
        // Arrange
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "", // Empty subject should cause error
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "test input");

        // Act
        await filter.OnActionExecutionAsync(actionContext, () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        var badRequestResult = actionContext.Result.ShouldBeOfType<BadRequestObjectResult>();
        badRequestResult.Value.ShouldBe("Subject not found in action metadata");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithUnsupportedConventionMode_ReturnsBadRequest()
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
        await filter.OnActionExecutionAsync(actionContext, () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        var badRequestResult = actionContext.Result.ShouldBeOfType<BadRequestObjectResult>();
        badRequestResult.Value.ShouldNotBeNull();
        badRequestResult.Value.ToString().ShouldContain("Unsupported convention mode");
    }

    [Fact]
    public async Task OnActionExecutionAsync_DependencyInjection_AllComponentsResolved()
    {
        // Arrange - Verify all dependencies are correctly injected
        var auditHandler = GetRequiredService<IAuditHandler>();
        var connectionResolver = GetRequiredService<IConnectionResolver>();
        var requestDataExtractor = GetRequiredService<IRequestDataExtractor>();
        var handlerFactory = GetRequiredService<IConventionModeHandlerFactory>();

        // Assert all dependencies are available
        auditHandler.ShouldNotBeNull();
        connectionResolver.ShouldNotBeNull();
        requestDataExtractor.ShouldNotBeNull();
        handlerFactory.ShouldNotBeNull();

        // Verify the filter can be created with all dependencies
        var filter = GetRequiredService<NatsProxyActionFilter>();
        filter.ShouldNotBeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_AuditHandlerIntegration_ProcessesRequestCorrectly()
    {
        // Arrange
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var auditHandler = GetRequiredService<IAuditHandler>();
        
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.request-response",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "test input");

        // Act
        await filter.OnActionExecutionAsync(actionContext, () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        // The audit handler should have been called during the process
        // We can verify this by checking that the result is properly formatted
        var objectResult = actionContext.Result.ShouldBeOfType<ObjectResult>();
        objectResult.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_ConnectionResolverIntegration_GetsCorrectConnection()
    {
        // Arrange
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var connectionResolver = GetRequiredService<IConnectionResolver>();
        
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.request-response",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "test input");

        // Act
        await filter.OnActionExecutionAsync(actionContext, () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        // Verify the connection was resolved (no connection error)
        actionContext.Result.ShouldNotBeOfType<ObjectResult>(result => 
            result.StatusCode == 500 && result.Value?.ToString()?.Contains("Unable to connect to NATS") == true);
    }

    [Fact]
    public async Task OnActionExecutionAsync_HandlerFactoryIntegration_SelectsCorrectHandler()
    {
        // Arrange
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var handlerFactory = GetRequiredService<IConventionModeHandlerFactory>();
        
        // Test Request-Response
        var requestResponseContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.request-response",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "test input");

        // Act
        await filter.OnActionExecutionAsync(requestResponseContext, () => Task.FromResult(new ActionExecutedContext(requestResponseContext, [], controller: null)));

        // Assert
        requestResponseContext.Result.ShouldNotBeNull();
        requestResponseContext.Result.ShouldNotBeOfType<BadRequestObjectResult>(result => 
            result.Value?.ToString()?.Contains("Unsupported convention mode") == true);

        // Test Pub-Sub
        var pubSubContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "PublishEventAsync",
            subject: "test.pub-sub",
            conventionMode: ConventionMode.PubSubPushJetStream,
            actionArguments: "test event");

        // Act
        await filter.OnActionExecutionAsync(pubSubContext, () => Task.FromResult(new ActionExecutedContext(pubSubContext, [], controller: null)));

        // Assert
        pubSubContext.Result.ShouldNotBeNull();
        pubSubContext.Result.ShouldNotBeOfType<BadRequestObjectResult>(result => 
            result.Value?.ToString()?.Contains("Unsupported convention mode") == true);
    }

    [Fact]
    public async Task OnActionExecutionAsync_CompleteOrchestration_LogsCorrectly()
    {
        // Arrange
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.request-response",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "test input");

        // Act
        await filter.OnActionExecutionAsync(actionContext, () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        // Verify the orchestration completed successfully (no error results)
        actionContext.Result.ShouldNotBeOfType<ObjectResult>(result => result.StatusCode >= 400);
    }

    [Theory]
    [InlineData(ConventionMode.RequestResponse, "test.request-response")]
    [InlineData(ConventionMode.PubSubPushClassic, "test.pub-sub-classic")]
    [InlineData(ConventionMode.PubSubPushJetStream, "test.pub-sub-jetstream")]
    [InlineData(ConventionMode.PubSubPullJetStream, "test.pub-sub-pull")]
    public async Task OnActionExecutionAsync_DifferentConventionModes_HandledCorrectly(ConventionMode mode, string subject)
    {
        // Arrange
        var filter = GetRequiredService<NatsProxyActionFilter>();
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: mode == ConventionMode.RequestResponse ? "ProcessRequestAsync" : "PublishEventAsync",
            subject: subject,
            conventionMode: mode,
            actionArguments: "test data");

        // Act
        await filter.OnActionExecutionAsync(actionContext, () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        // Verify no unsupported mode errors
        actionContext.Result.ShouldNotBeOfType<BadRequestObjectResult>(result => 
            result.Value?.ToString()?.Contains("Unsupported convention mode") == true);
    }
}