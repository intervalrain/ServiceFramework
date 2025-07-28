using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Core.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace EdgeSync.ServiceFramework.UnitTests.Core.Filters;

/// <summary>
/// Unit tests for NatsProxyActionFilter - the refactored coordinator
/// Tests the orchestration logic and proper delegation to specialized services
/// </summary>
public class NatsProxyActionFilterTests
{
    private readonly IAuditHandler _mockAuditHandler;
    private readonly IConnectionResolver _mockConnectionResolver;
    private readonly IRequestDataExtractor _mockRequestDataExtractor;
    private readonly IConventionModeHandlerFactory _mockHandlerFactory;
    private readonly IConventionModeHandler _mockHandler;
    private readonly ILogger<NatsProxyActionFilter> _mockLogger;
    private readonly INatsConnection _mockConnection;
    private readonly NatsProxyActionFilter _filter;

    public NatsProxyActionFilterTests()
    {
        _mockAuditHandler = Substitute.For<IAuditHandler>();
        _mockConnectionResolver = Substitute.For<IConnectionResolver>();
        _mockRequestDataExtractor = Substitute.For<IRequestDataExtractor>();
        _mockHandlerFactory = Substitute.For<IConventionModeHandlerFactory>();
        _mockHandler = Substitute.For<IConventionModeHandler>();
        _mockLogger = Substitute.For<ILogger<NatsProxyActionFilter>>();
        _mockConnection = Substitute.For<INatsConnection>();

        _filter = new NatsProxyActionFilter(
            _mockAuditHandler,
            _mockConnectionResolver,
            _mockRequestDataExtractor,
            _mockHandlerFactory,
            _mockLogger);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithValidRequest_CoordiatesSucesfully()
    {
        // Arrange
        var context = CreateActionExecutingContext("TestController", "TestAction", "test.subject");
        var actionDelegate = Substitute.For<ActionExecutionDelegate>();
        var requestData = new { Data = "test" };
        var wrappedRequest = new { Wrapped = requestData };
        var expectedResult = new OkObjectResult("Success");

        _mockRequestDataExtractor.ExtractRequestData(context).Returns(requestData);
        _mockAuditHandler.WrapRequestWithAudit(requestData, context.HttpContext).Returns(wrappedRequest);
        _mockConnectionResolver.GetConnectionAsync(Arg.Any<string>()).Returns(_mockConnection);
        _mockHandlerFactory.GetHandler(Arg.Any<ConventionMode>()).Returns(_mockHandler);
        _mockHandler.HandleAsync(context, _mockConnection, Arg.Any<ActionContextMetadata>(), wrappedRequest)
                   .Returns(expectedResult);

        // Act
        await _filter.OnActionExecutionAsync(context, actionDelegate);

        // Assert
        context.Result.ShouldBe(expectedResult);
        
        // Verify proper coordination
        _mockRequestDataExtractor.Received(1).ExtractRequestData(context);
        _mockAuditHandler.Received(1).WrapRequestWithAudit(requestData, context.HttpContext);
        await _mockConnectionResolver.Received(1).GetConnectionAsync(Arg.Any<string>());
        _mockHandlerFactory.Received(1).GetHandler(Arg.Any<ConventionMode>());
        await _mockHandler.Received(1).HandleAsync(context, _mockConnection, Arg.Any<ActionContextMetadata>(), wrappedRequest);
        
        // Should not call next() since we're replacing action execution
        await actionDelegate.DidNotReceive().Invoke();
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithMissingSubject_ReturnsBadRequest()
    {
        // Arrange
        // Create a context with no subject property at all
        var actionDescriptor = new ActionDescriptor
        {
            DisplayName = "TestController.TestAction",
            RouteValues = new Dictionary<string, string?>
            {
                ["controller"] = "TestController",
                ["action"] = "TestAction"
            }
        };

        // Add only required metadata, but NOT Subject
        actionDescriptor.Properties["ServiceName"] = "TestController";
        actionDescriptor.Properties["MethodName"] = "TestAction";
        actionDescriptor.Properties["ConventionMode"] = ConventionMode.RequestResponse;
        
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), actionDescriptor);
        var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), null);
        
        var actionDelegate = Substitute.For<ActionExecutionDelegate>();

        // Act
        await _filter.OnActionExecutionAsync(context, actionDelegate);

        // Assert
        context.Result.ShouldBeOfType<BadRequestObjectResult>();
        var result = (BadRequestObjectResult)context.Result;
        result.Value.ShouldBe("Subject not found in action metadata");

        // Should not proceed with other operations
        _mockRequestDataExtractor.DidNotReceive().ExtractRequestData(Arg.Any<ActionExecutingContext>());
        await _mockConnectionResolver.DidNotReceive().GetConnectionAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithConnectionFailure_ReturnsInternalServerError()
    {
        // Arrange
        var context = CreateActionExecutingContext("TestController", "TestAction", "test.subject");
        var actionDelegate = Substitute.For<ActionExecutionDelegate>();

        _mockConnectionResolver.GetConnectionAsync(Arg.Any<string>()).Returns((INatsConnection?)null);

        // Act
        await _filter.OnActionExecutionAsync(context, actionDelegate);

        // Assert
        context.Result.ShouldBeOfType<ObjectResult>();
        var result = (ObjectResult)context.Result;
        result.StatusCode.ShouldBe(500);
        result.Value.ShouldBe("Unable to connect to NATS for channel: default");

        // Should not proceed after connection failure
        _mockHandlerFactory.DidNotReceive().GetHandler(Arg.Any<ConventionMode>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithNotSupportedException_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateActionExecutingContext("TestController", "TestAction", "test.subject");
        var actionDelegate = Substitute.For<ActionExecutionDelegate>();
        var requestData = new { Data = "test" };

        _mockRequestDataExtractor.ExtractRequestData(context).Returns(requestData);
        _mockAuditHandler.WrapRequestWithAudit(requestData, context.HttpContext).Returns(requestData);
        _mockConnectionResolver.GetConnectionAsync(Arg.Any<string>()).Returns(_mockConnection);
        _mockHandlerFactory.GetHandler(Arg.Any<ConventionMode>())
                          .Throws(new NotSupportedException("Unsupported mode"));

        // Act
        await _filter.OnActionExecutionAsync(context, actionDelegate);

        // Assert
        context.Result.ShouldBeOfType<BadRequestObjectResult>();
        var result = (BadRequestObjectResult)context.Result;
        result.Value.ShouldBe("Unsupported convention mode: Unsupported mode");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithTimeoutException_ReturnsRequestTimeout()
    {
        // Arrange
        var context = CreateActionExecutingContext("TestController", "TestAction", "test.subject");
        var actionDelegate = Substitute.For<ActionExecutionDelegate>();
        var requestData = new { Data = "test" };
        var wrappedRequest = new { Wrapped = requestData };

        _mockRequestDataExtractor.ExtractRequestData(context).Returns(requestData);
        _mockAuditHandler.WrapRequestWithAudit(requestData, context.HttpContext).Returns(wrappedRequest);
        _mockConnectionResolver.GetConnectionAsync(Arg.Any<string>()).Returns(_mockConnection);
        _mockHandlerFactory.GetHandler(Arg.Any<ConventionMode>()).Returns(_mockHandler);
        _mockHandler.HandleAsync(context, _mockConnection, Arg.Any<ActionContextMetadata>(), wrappedRequest)
                   .Throws(new TimeoutException("Request timeout"));

        _mockAuditHandler.ExtractAuditInfo(wrappedRequest).Returns(("req123", "2023-01-01"));

        // Act
        await _filter.OnActionExecutionAsync(context, actionDelegate);

        // Assert
        context.Result.ShouldBeOfType<ObjectResult>();
        var result = (ObjectResult)context.Result;
        result.StatusCode.ShouldBe(408);
        result.Value.ShouldBe("Request timeout");

        // Verify audit info extraction was called
        _mockAuditHandler.Received(1).ExtractAuditInfo(wrappedRequest);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithGeneralException_ReturnsInternalServerError()
    {
        // Arrange
        var context = CreateActionExecutingContext("TestController", "TestAction", "test.subject");
        var actionDelegate = Substitute.For<ActionExecutionDelegate>();
        var requestData = new { Data = "test" };
        var wrappedRequest = new { Wrapped = requestData };

        _mockRequestDataExtractor.ExtractRequestData(context).Returns(requestData);
        _mockAuditHandler.WrapRequestWithAudit(requestData, context.HttpContext).Returns(wrappedRequest);
        _mockConnectionResolver.GetConnectionAsync(Arg.Any<string>()).Returns(_mockConnection);
        _mockHandlerFactory.GetHandler(Arg.Any<ConventionMode>()).Returns(_mockHandler);
        _mockHandler.HandleAsync(context, _mockConnection, Arg.Any<ActionContextMetadata>(), wrappedRequest)
                   .Throws(new Exception("Unexpected error"));

        _mockAuditHandler.ExtractAuditInfo(wrappedRequest).Returns(("req123", "2023-01-01"));

        // Act
        await _filter.OnActionExecutionAsync(context, actionDelegate);

        // Assert
        context.Result.ShouldBeOfType<ObjectResult>();
        var result = (ObjectResult)context.Result;
        result.StatusCode.ShouldBe(500);
        result.Value.ShouldBe("Internal server error: Unexpected error");

        // Verify audit info extraction was called
        _mockAuditHandler.Received(1).ExtractAuditInfo(wrappedRequest);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithRequestResponseMode_UsesCorrectHandler()
    {
        // Arrange
        var context = CreateActionExecutingContext("TestController", "TestAction", "test.subject", ConventionMode.RequestResponse);
        var actionDelegate = Substitute.For<ActionExecutionDelegate>();

        _mockRequestDataExtractor.ExtractRequestData(context).Returns(new object());
        _mockAuditHandler.WrapRequestWithAudit(Arg.Any<object>(), Arg.Any<HttpContext>()).Returns(new object());
        _mockConnectionResolver.GetConnectionAsync(Arg.Any<string>()).Returns(_mockConnection);
        _mockHandlerFactory.GetHandler(ConventionMode.RequestResponse).Returns(_mockHandler);
        _mockHandler.HandleAsync(Arg.Any<ActionExecutingContext>(), Arg.Any<INatsConnection>(), Arg.Any<ActionContextMetadata>(), Arg.Any<object>())
                   .Returns(new OkResult());

        // Act
        await _filter.OnActionExecutionAsync(context, actionDelegate);

        // Assert
        _mockHandlerFactory.Received(1).GetHandler(ConventionMode.RequestResponse);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithPubSubMode_UsesCorrectHandler()
    {
        // Arrange
        var context = CreateActionExecutingContext("TestController", "TestAction", "test.subject", ConventionMode.PubSubPushClassic);
        var actionDelegate = Substitute.For<ActionExecutionDelegate>();

        _mockRequestDataExtractor.ExtractRequestData(context).Returns(new object());
        _mockAuditHandler.WrapRequestWithAudit(Arg.Any<object>(), Arg.Any<HttpContext>()).Returns(new object());
        _mockConnectionResolver.GetConnectionAsync(Arg.Any<string>()).Returns(_mockConnection);
        _mockHandlerFactory.GetHandler(ConventionMode.PubSubPushClassic).Returns(_mockHandler);
        _mockHandler.HandleAsync(Arg.Any<ActionExecutingContext>(), Arg.Any<INatsConnection>(), Arg.Any<ActionContextMetadata>(), Arg.Any<object>())
                   .Returns(new OkResult());

        // Act
        await _filter.OnActionExecutionAsync(context, actionDelegate);

        // Assert
        _mockHandlerFactory.Received(1).GetHandler(ConventionMode.PubSubPushClassic);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithCustomChannelName_PassesCorrectChannelToConnectionResolver()
    {
        // Arrange
        var context = CreateActionExecutingContext("TestController", "TestAction", "test.subject", channelName: "custom-channel");
        var actionDelegate = Substitute.For<ActionExecutionDelegate>();

        _mockRequestDataExtractor.ExtractRequestData(context).Returns(new object());
        _mockAuditHandler.WrapRequestWithAudit(Arg.Any<object>(), Arg.Any<HttpContext>()).Returns(new object());
        _mockConnectionResolver.GetConnectionAsync("custom-channel").Returns(_mockConnection);
        _mockHandlerFactory.GetHandler(Arg.Any<ConventionMode>()).Returns(_mockHandler);
        _mockHandler.HandleAsync(Arg.Any<ActionExecutingContext>(), Arg.Any<INatsConnection>(), Arg.Any<ActionContextMetadata>(), Arg.Any<object>())
                   .Returns(new OkResult());

        // Act
        await _filter.OnActionExecutionAsync(context, actionDelegate);

        // Assert
        await _mockConnectionResolver.Received(1).GetConnectionAsync("custom-channel");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithNullAuditInfo_HandlesGracefully()
    {
        // Arrange
        var context = CreateActionExecutingContext("TestController", "TestAction", "test.subject");
        var actionDelegate = Substitute.For<ActionExecutionDelegate>();
        var requestData = new { Data = "test" };
        var wrappedRequest = new { Wrapped = requestData };

        _mockRequestDataExtractor.ExtractRequestData(context).Returns(requestData);
        _mockAuditHandler.WrapRequestWithAudit(requestData, context.HttpContext).Returns(wrappedRequest);
        _mockConnectionResolver.GetConnectionAsync(Arg.Any<string>()).Returns(_mockConnection);
        _mockHandlerFactory.GetHandler(Arg.Any<ConventionMode>()).Returns(_mockHandler);
        _mockHandler.HandleAsync(context, _mockConnection, Arg.Any<ActionContextMetadata>(), wrappedRequest)
                   .Throws(new TimeoutException("Request timeout"));

        _mockAuditHandler.ExtractAuditInfo(wrappedRequest).Returns((ValueTuple<string, string>?)null);

        // Act
        await _filter.OnActionExecutionAsync(context, actionDelegate);

        // Assert
        context.Result.ShouldBeOfType<ObjectResult>();
        var result = (ObjectResult)context.Result;
        result.StatusCode.ShouldBe(408);

        // Should handle null audit info gracefully
        _mockAuditHandler.Received(1).ExtractAuditInfo(wrappedRequest);
    }

    private ActionExecutingContext CreateActionExecutingContext(
        string controllerName, 
        string actionName, 
        string? subject = null, 
        ConventionMode conventionMode = ConventionMode.RequestResponse,
        string? channelName = null)
    {
        var actionDescriptor = new ActionDescriptor
        {
            DisplayName = $"{controllerName}.{actionName}",
            RouteValues = new Dictionary<string, string?>
            {
                ["controller"] = controllerName,
                ["action"] = actionName
            }
        };

        // Add metadata for ActionContextMetadata
        var properties = actionDescriptor.Properties;
        properties["ServiceName"] = controllerName;
        properties["MethodName"] = actionName;
        properties["ConventionMode"] = conventionMode;
        
        if (subject != null)
        {
            properties["Subject"] = subject;
        }
        
        if (channelName != null)
        {
            properties["ChannelName"] = channelName;
        }

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), actionDescriptor);
        
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), null);
    }
}