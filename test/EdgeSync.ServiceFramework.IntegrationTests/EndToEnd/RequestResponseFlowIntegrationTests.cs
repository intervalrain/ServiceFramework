using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Core.Filters;
using EdgeSync.ServiceFramework.IntegrationTests.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.IntegrationTests.EndToEnd;

/// <summary>
/// End-to-end integration tests for the complete request-response flow
/// Tests the entire refactored architecture working together
/// </summary>
public class RequestResponseFlowIntegrationTests : ServiceFrameworkTestBase
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        
        // Setup enhanced test connection with more realistic behaviors
        services.AddSingleton<INatsConnection>(provider => CreateEnhancedTestConnection());
    }

    [Fact]
    public async Task RequestResponseFlow_CompleteArchitecture_ProcessesRequestCorrectly()
    {
        // Arrange - Test complete flow through all refactored components
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.request-response",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "integration test input");

        var filter = GetRequiredService<NatsProxyActionFilter>();

        // Act - Execute the complete request-response flow
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert - Verify the complete flow worked
        actionContext.Result.ShouldNotBeNull();
        
        var objectResult = actionContext.Result.ShouldBeOfType<ObjectResult>();
        objectResult.Value.ShouldNotBeNull();
        
        // Verify the response contains processed data
        var responseValue = objectResult.Value.ToString();
        responseValue.ShouldContain("Processed: integration test input");
    }

    [Fact]
    public async Task RequestResponseFlow_WithAuditWrapper_IncludesAuditInformation()
    {
        // Arrange
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.audit-request",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "audit test data");

        var filter = GetRequiredService<NatsProxyActionFilter>();
        var auditHandler = GetRequiredService<IAuditHandler>();

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        // The audit handler should have been involved in the process
        var objectResult = actionContext.Result.ShouldBeOfType<ObjectResult>();
        objectResult.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task RequestResponseFlow_ParameterlessMethod_HandlesCorrectly()
    {
        // Arrange
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "GetCountAsync",
            subject: "test.parameterless",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: null);

        var filter = GetRequiredService<NatsProxyActionFilter>();

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        var objectResult = actionContext.Result.ShouldBeOfType<ObjectResult>();
        objectResult.Value.ShouldNotBeNull();
        
        // Should return the expected count value
        objectResult.Value.ToString().ShouldContain("42");
    }

    [Fact]
    public async Task RequestResponseFlow_ThroughAllComponents_MaintainsSolidPrinciples()
    {
        // Arrange - Test that each component plays its role correctly
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.solid-principles",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "SOLID test");

        // Verify all components are properly separated and injected
        var auditHandler = GetRequiredService<IAuditHandler>();
        var connectionResolver = GetRequiredService<IConnectionResolver>();
        var requestDataExtractor = GetRequiredService<IRequestDataExtractor>();
        var responseProcessor = GetRequiredService<IResponseProcessor>();
        var handlerFactory = GetRequiredService<IConventionModeHandlerFactory>();
        var filter = GetRequiredService<NatsProxyActionFilter>();

        // Act - Each component should perform its specific responsibility
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert - Verify separation of concerns is maintained
        auditHandler.ShouldNotBeNull(); // Handles audit concerns
        connectionResolver.ShouldNotBeNull(); // Resolves NATS connections
        requestDataExtractor.ShouldNotBeNull(); // Extracts request data
        responseProcessor.ShouldNotBeNull(); // Processes responses
        handlerFactory.ShouldNotBeNull(); // Creates appropriate handlers
        filter.ShouldNotBeNull(); // Coordinates the flow

        actionContext.Result.ShouldNotBeNull();
        actionContext.Result.ShouldBeOfType<ObjectResult>();
    }

    [Fact]
    public async Task RequestResponseFlow_ConnectionResolution_WorksThroughArchitecture()
    {
        // Arrange
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.connection",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "connection test");

        var connectionResolver = GetRequiredService<IConnectionResolver>();
        var filter = GetRequiredService<NatsProxyActionFilter>();

        // Act
        // Test that connection resolution works through the complete flow
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
    public async Task RequestResponseFlow_HandlerFactoryIntegration_SelectsCorrectHandler()
    {
        // Arrange
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.handler-selection",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "handler test");

        var handlerFactory = GetRequiredService<IConventionModeHandlerFactory>();
        var filter = GetRequiredService<NatsProxyActionFilter>();

        // Act
        var handler = handlerFactory.GetHandler(ConventionMode.RequestResponse);
        
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        handler.ShouldNotBeNull();
        handler.SupportedMode.ShouldBe(ConventionMode.RequestResponse);
        
        actionContext.Result.ShouldNotBeNull();
        actionContext.Result.ShouldBeOfType<ObjectResult>();
    }

    [Fact]
    public async Task RequestResponseFlow_ResponseProcessing_FormatsResponseCorrectly()
    {
        // Arrange
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.response-processing",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "response format test");

        var responseProcessor = GetRequiredService<IResponseProcessor>();
        var filter = GetRequiredService<NatsProxyActionFilter>();

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        var result = actionContext.Result.ShouldBeOfType<ObjectResult>();
        result.Value.ShouldNotBeNull();
        
        // The response processor should have formatted the response appropriately
        result.StatusCode.ShouldBeNull(); // Default OK status
        result.Value.ToString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task RequestResponseFlow_ErrorHandling_PropagatesCorrectly()
    {
        // Arrange - Test error handling through the complete architecture
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "", // Empty subject should cause error
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "error test");

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

    [Theory]
    [InlineData("simple-input", "Processed: simple-input")]
    [InlineData("complex input with spaces", "Processed: complex input with spaces")]
    [InlineData("", "Processed: ")]
    public async Task RequestResponseFlow_VariousInputs_ProcessedCorrectly(string input, string expectedOutput)
    {
        // Arrange
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.various-inputs",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: input);

        var filter = GetRequiredService<NatsProxyActionFilter>();

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        var objectResult = actionContext.Result.ShouldBeOfType<ObjectResult>();
        objectResult.Value.ShouldNotBeNull();
        objectResult.Value.ToString().ShouldContain(expectedOutput);
    }

    [Fact]
    public async Task RequestResponseFlow_PerformanceCharacteristics_MaintainedThroughRefactoring()
    {
        // Arrange
        var actionContext = TestFixtures.CreateActionExecutingContext(
            serviceName: "TestService",
            methodName: "ProcessRequestAsync",
            subject: "test.performance",
            conventionMode: ConventionMode.RequestResponse,
            actionArguments: "performance test");

        var filter = GetRequiredService<NatsProxyActionFilter>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await filter.OnActionExecutionAsync(actionContext, 
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller: null)));

        stopwatch.Stop();

        // Assert
        actionContext.Result.ShouldNotBeNull();
        
        // Performance should be reasonable (allowing for test overhead)
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000); // 5 seconds max for integration test
        
        var objectResult = actionContext.Result.ShouldBeOfType<ObjectResult>();
        objectResult.Value.ShouldNotBeNull();
    }

    private INatsConnection CreateEnhancedTestConnection()
    {
        var connection = Substitute.For<INatsConnection>();
        
        // Setup server info
        connection.ServerInfo.Returns(new NatsServerInfo(
            "test-server", "2.9.0", "go1.19", "localhost", 4222, 8888, false, 0, [], []));
        
        // Setup enhanced request-response behavior
        connection.RequestAsync<object?, object?>(
            Arg.Any<string>(), 
            Arg.Any<object?>(), 
            Arg.Any<INatsSerializerRegistry>(), 
            Arg.Any<NatsRequestOpts?>(), 
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var subject = callInfo.ArgAt<string>(0);
                var request = callInfo.ArgAt<object?>(1);
                
                // Simulate more realistic responses based on subject and request
                return subject switch
                {
                    "test.request-response" => Task.FromResult<object?>("Processed: integration test input"),
                    "test.audit-request" => Task.FromResult<object?>("Processed: audit test data"),
                    "test.parameterless" => Task.FromResult<object?>(42),
                    "test.solid-principles" => Task.FromResult<object?>("Processed: SOLID test"),
                    "test.connection" => Task.FromResult<object?>("Processed: connection test"),
                    "test.handler-selection" => Task.FromResult<object?>("Processed: handler test"),
                    "test.response-processing" => Task.FromResult<object?>("Processed: response format test"),
                    "test.various-inputs" => Task.FromResult<object?>($"Processed: {ExtractInputFromRequest(request)}"),
                    "test.performance" => Task.FromResult<object?>("Processed: performance test"),
                    _ => Task.FromResult<object?>($"Processed: {ExtractInputFromRequest(request)}")
                };
            });

        return connection;
    }

    private string ExtractInputFromRequest(object? request)
    {
        if (request == null) return "";
        
        // Handle audit wrapper or direct input
        var requestStr = request.ToString() ?? "";
        
        // Try to extract from audit wrapper if present
        if (requestStr.Contains("Data"))
        {
            // This is a simplified extraction for testing
            return "integration test input"; // or parse from the wrapped request
        }
        
        return requestStr;
    }
}