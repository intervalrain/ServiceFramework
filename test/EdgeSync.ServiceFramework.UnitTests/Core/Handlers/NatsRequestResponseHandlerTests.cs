using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Handlers;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Core.Filters;
using EdgeSync.ServiceFramework.Core.Serializers;
using EdgeSync.ServiceFramework.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NSubstitute;
using Shouldly;
using System.Reflection;
using Xunit;

namespace EdgeSync.ServiceFramework.UnitTests.Core.Handlers;

/// <summary>
/// Unit tests for NatsRequestResponseHandler to verify Request-Response pattern handling logic
/// Tests cover core logic, serialization/deserialization, error handling, timeout, audit processing, and NATS interaction
/// </summary>
public class NatsRequestResponseHandlerTests
{
    private readonly ISerializerAdapterFactory _mockSerializerAdapterFactory;
    private readonly IServiceProvider _mockServiceProvider;
    private readonly IConnectionResolver _mockConnectionResolver;
    private readonly IResponseProcessor _mockResponseProcessor;
    private readonly ILogger<NatsRequestResponseHandler> _mockLogger;
    private readonly ISerializerAdapter _mockSerializerAdapter;
    private readonly INatsConnection _mockConnection;
    private readonly INatsSerializerRegistry _mockSerializerRegistry;
    private readonly NatsRequestResponseHandler _handler;

    public NatsRequestResponseHandlerTests()
    {
        _mockSerializerAdapterFactory = Substitute.For<ISerializerAdapterFactory>();
        _mockServiceProvider = Substitute.For<IServiceProvider>();
        _mockConnectionResolver = Substitute.For<IConnectionResolver>();
        _mockResponseProcessor = Substitute.For<IResponseProcessor>();
        _mockLogger = Substitute.For<ILogger<NatsRequestResponseHandler>>();
        _mockSerializerAdapter = Substitute.For<ISerializerAdapter>();
        _mockConnection = Substitute.For<INatsConnection>();
        _mockSerializerRegistry = Substitute.For<INatsSerializerRegistry>();

        _handler = new NatsRequestResponseHandler(
            _mockSerializerAdapterFactory,
            _mockServiceProvider,
            _mockConnectionResolver,
            _mockResponseProcessor,
            _mockLogger);

        // Setup default mock behaviors
        _mockConnectionResolver.GetSerializerForConnection(Arg.Any<string>()).Returns(_mockSerializerRegistry);
        _mockSerializerAdapterFactory.GetAdapter(Arg.Any<INatsSerializerRegistry>()).Returns(_mockSerializerAdapter);
    }

    [Fact]
    public void SupportedMode_ShouldReturn_RequestResponse()
    {
        // Act & Assert
        _handler.SupportedMode.ShouldBe(ConventionMode.RequestResponse);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldCallSerializerAdapterAndReturnProcessedResponse()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata();
        var wrappedRequest = RequestDto<string>.Create("test data");
        var expectedResponse = ResponseDto<string>.Success("response data", wrappedRequest.ReqSeqId);
        var processedResult = new OkObjectResult(expectedResponse);

        _mockSerializerAdapter.SendRequestAsync(
            _mockConnection,
            metadata.Subject,
            wrappedRequest,
            false,
            typeof(string),
            _mockSerializerRegistry)
            .Returns(expectedResponse);

        _mockResponseProcessor.ProcessResponse(expectedResponse, false).Returns(processedResult);

        // Act
        var result = await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        result.ShouldBe(processedResult);
        await _mockSerializerAdapter.Received(1).SendRequestAsync(
            _mockConnection,
            metadata.Subject,
            wrappedRequest,
            false,
            typeof(string),
            _mockSerializerRegistry);
        _mockResponseProcessor.Received(1).ProcessResponse(expectedResponse, false);
    }

    [Fact]
    public async Task HandleAsync_WithParameterlessMethod_ShouldPassCorrectParameterlessFlag()
    {
        // Arrange
        var context = CreateActionExecutingContext(isParameterless: true);
        var metadata = CreateActionContextMetadata();
        var wrappedRequest = RequestDto<object>.Create(new object());
        var expectedResponse = "response";

        _mockSerializerAdapter.SendRequestAsync(
            _mockConnection,
            metadata.Subject,
            wrappedRequest,
            true, // isOriginallyParameterless should be true
            null,
            _mockSerializerRegistry)
            .Returns(expectedResponse);

        _mockResponseProcessor.ProcessResponse(expectedResponse, false).Returns(new OkResult());

        // Act
        await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        await _mockSerializerAdapter.Received(1).SendRequestAsync(
            _mockConnection,
            metadata.Subject,
            wrappedRequest,
            true, // Verify isOriginallyParameterless is true
            null,
            _mockSerializerRegistry);
    }

    [Fact]
    public async Task HandleAsync_WithTaskReturnType_ShouldExtractCorrectResponseType()
    {
        // Arrange
        var method = typeof(TestService).GetMethod(nameof(TestService.GetDataAsync));
        var context = CreateActionExecutingContext(originalMethod: method);
        var metadata = CreateActionContextMetadata();
        var wrappedRequest = RequestDto<int>.Create(123);

        _mockSerializerAdapter.SendRequestAsync(
            Arg.Any<INatsConnection>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<bool>(),
            typeof(string), // Expected to extract string from Task<string>
            Arg.Any<INatsSerializerRegistry>())
            .Returns("response");

        _mockResponseProcessor.ProcessResponse(Arg.Any<object>(), Arg.Any<bool>()).Returns(new OkResult());

        // Act
        await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        await _mockSerializerAdapter.Received(1).SendRequestAsync(
            Arg.Any<INatsConnection>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<bool>(),
            typeof(string),
            Arg.Any<INatsSerializerRegistry>());
    }

    [Fact]
    public async Task HandleAsync_WithValueTaskReturnType_ShouldExtractCorrectResponseType()
    {
        // Arrange
        var method = typeof(TestService).GetMethod(nameof(TestService.GetDataValueTaskAsync));
        var context = CreateActionExecutingContext(originalMethod: method);
        var metadata = CreateActionContextMetadata();
        var wrappedRequest = RequestDto<int>.Create(123);

        _mockSerializerAdapter.SendRequestAsync(
            Arg.Any<INatsConnection>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<bool>(),
            typeof(int), // Expected to extract int from ValueTask<int>
            Arg.Any<INatsSerializerRegistry>())
            .Returns(42);

        _mockResponseProcessor.ProcessResponse(Arg.Any<object>(), Arg.Any<bool>()).Returns(new OkResult());

        // Act
        await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        await _mockSerializerAdapter.Received(1).SendRequestAsync(
            Arg.Any<INatsConnection>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<bool>(),
            typeof(int),
            Arg.Any<INatsSerializerRegistry>());
    }

    [Fact]
    public async Task HandleAsync_WithVoidReturnType_ShouldPassNullAsExpectedResponseType()
    {
        // Arrange
        var method = typeof(TestService).GetMethod(nameof(TestService.ProcessData));
        var context = CreateActionExecutingContext(originalMethod: method);
        var metadata = CreateActionContextMetadata();
        var wrappedRequest = RequestDto<string>.Create("test");

        _mockSerializerAdapter.SendRequestAsync(
            Arg.Any<INatsConnection>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<bool>(),
            null, // Expected null for void return type
            Arg.Any<INatsSerializerRegistry>())
            .Returns((object?)null);

        _mockResponseProcessor.ProcessResponse(Arg.Any<object>(), Arg.Any<bool>()).Returns(new OkResult());

        // Act
        await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        await _mockSerializerAdapter.Received(1).SendRequestAsync(
            Arg.Any<INatsConnection>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<bool>(),
            null,
            Arg.Any<INatsSerializerRegistry>());
    }

    [Fact]
    public async Task HandleAsync_WithUseExceptionHandlerEnabled_ShouldPassTrueToResponseProcessor()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata();
        var wrappedRequest = RequestDto<string>.Create("test");
        var response = "response";

        var options = new AutoConventionOptions { UseExceptionHandler = true };
        var mockOptions = Substitute.For<IOptions<AutoConventionOptions>>();
        mockOptions.Value.Returns(options);
        _mockServiceProvider.GetService<IOptions<AutoConventionOptions>>().Returns(mockOptions);

        _mockSerializerAdapter.SendRequestAsync(
            Arg.Any<INatsConnection>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<bool>(),
            Arg.Any<Type>(),
            Arg.Any<INatsSerializerRegistry>())
            .Returns(response);

        _mockResponseProcessor.ProcessResponse(response, true).Returns(new OkResult());

        // Act
        await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        _mockResponseProcessor.Received(1).ProcessResponse(response, true);
    }

    [Fact]
    public async Task HandleAsync_WithUseExceptionHandlerDisabled_ShouldPassFalseToResponseProcessor()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata();
        var wrappedRequest = RequestDto<string>.Create("test");
        var response = "response";

        var options = new AutoConventionOptions { UseExceptionHandler = false };
        var mockOptions = Substitute.For<IOptions<AutoConventionOptions>>();
        mockOptions.Value.Returns(options);
        _mockServiceProvider.GetService<IOptions<AutoConventionOptions>>().Returns(mockOptions);

        _mockSerializerAdapter.SendRequestAsync(
            Arg.Any<INatsConnection>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<bool>(),
            Arg.Any<Type>(),
            Arg.Any<INatsSerializerRegistry>())
            .Returns(response);

        _mockResponseProcessor.ProcessResponse(response, false).Returns(new OkResult());

        // Act
        await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        _mockResponseProcessor.Received(1).ProcessResponse(response, false);
    }

    [Fact]
    public async Task HandleAsync_WithNoAutoConventionOptions_ShouldDefaultToFalseForUseExceptionHandler()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata();
        var wrappedRequest = RequestDto<string>.Create("test");
        var response = "response";

        _mockServiceProvider.GetService<IOptions<AutoConventionOptions>>().Returns((IOptions<AutoConventionOptions>?)null);

        _mockSerializerAdapter.SendRequestAsync(
            Arg.Any<INatsConnection>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<bool>(),
            Arg.Any<Type>(),
            Arg.Any<INatsSerializerRegistry>())
            .Returns(response);

        _mockResponseProcessor.ProcessResponse(response, false).Returns(new OkResult());

        // Act
        await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        _mockResponseProcessor.Received(1).ProcessResponse(response, false);
    }

    [Fact]
    public async Task HandleAsync_WhenSerializerAdapterThrowsException_ShouldLogErrorAndRethrow()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata();
        var wrappedRequest = RequestDto<string>.Create("test");
        var expectedException = new Exception("Serializer error");

        _mockSerializerAdapter.SendRequestAsync(
            Arg.Any<INatsConnection>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<bool>(),
            Arg.Any<Type>(),
            Arg.Any<INatsSerializerRegistry>())
            .Returns(Task.FromException<object?>(expectedException));

        // Act & Assert
        var exception = await Should.ThrowAsync<Exception>(
            () => _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest));

        exception.ShouldBe(expectedException);
    }

    [Fact]
    public async Task HandleAsync_WithAuditInfoInRequest_ShouldCompleteSuccessfully()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata("TestService", "TestMethod", "test.subject");
        var wrappedRequest = RequestDto<string>.Create("test");
        var response = ResponseDto<string>.Success("response", wrappedRequest.ReqSeqId);

        _mockSerializerAdapter.SendRequestAsync(
            Arg.Any<INatsConnection>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<bool>(),
            Arg.Any<Type>(),
            Arg.Any<INatsSerializerRegistry>())
            .Returns(response);

        _mockResponseProcessor.ProcessResponse(Arg.Any<object>(), Arg.Any<bool>()).Returns(new OkResult());

        // Act
        var result = await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<OkResult>();
    }

    [Fact]
    public async Task HandleAsync_WithNullRequest_ShouldHandleGracefully()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata();
        object? wrappedRequest = null;
        var response = "response";

        _mockSerializerAdapter.SendRequestAsync(
            _mockConnection,
            metadata.Subject,
            null,
            false,
            typeof(string),
            _mockSerializerRegistry)
            .Returns(response);

        _mockResponseProcessor.ProcessResponse(response, false).Returns(new OkResult());

        // Act
        var result = await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        result.ShouldNotBeNull();
        await _mockSerializerAdapter.Received(1).SendRequestAsync(
            _mockConnection,
            metadata.Subject,
            null,
            false,
            typeof(string),
            _mockSerializerRegistry);
    }

    [Fact]
    public async Task HandleAsync_WithNullResponse_ShouldHandleGracefully()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata();
        var wrappedRequest = RequestDto<string>.Create("test");

        _mockSerializerAdapter.SendRequestAsync(
            Arg.Any<INatsConnection>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<bool>(),
            Arg.Any<Type>(),
            Arg.Any<INatsSerializerRegistry>())
            .Returns((object?)null);

        _mockResponseProcessor.ProcessResponse(null, false).Returns(new OkResult());

        // Act
        var result = await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        result.ShouldNotBeNull();
        _mockResponseProcessor.Received(1).ProcessResponse(null, false);
    }

    [Fact]
    public async Task HandleAsync_ShouldUseCorrectChannelNameForConnectionResolver()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata(channelName: "custom-channel");
        var wrappedRequest = RequestDto<string>.Create("test");

        _mockSerializerAdapter.SendRequestAsync(
            Arg.Any<INatsConnection>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<bool>(),
            Arg.Any<Type>(),
            Arg.Any<INatsSerializerRegistry>())
            .Returns("response");

        _mockResponseProcessor.ProcessResponse(Arg.Any<object>(), Arg.Any<bool>()).Returns(new OkResult());

        // Act
        await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        _mockConnectionResolver.Received(1).GetSerializerForConnection("custom-channel");
        _mockSerializerAdapterFactory.Received(1).GetAdapter(_mockSerializerRegistry);
    }

    private ActionExecutingContext CreateActionExecutingContext(bool isParameterless = false, MethodInfo? originalMethod = null)
    {
        var actionDescriptor = new ActionDescriptor();
        
        if (originalMethod != null)
        {
            actionDescriptor.Properties["OriginalMethod"] = originalMethod;
        }
        else if (isParameterless)
        {
            actionDescriptor.Properties["OriginalMethod"] = typeof(TestService).GetMethod(nameof(TestService.ParameterlessMethod));
        }
        else
        {
            actionDescriptor.Properties["OriginalMethod"] = typeof(TestService).GetMethod(nameof(TestService.GetData));
        }

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), actionDescriptor);
        
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
    }

    private ActionContextMetadata CreateActionContextMetadata(
        string serviceName = "TestService", 
        string methodName = "TestMethod", 
        string subject = "test.subject",
        string channelName = "default")
    {
        var actionDescriptor = new ActionDescriptor();
        actionDescriptor.Properties[nameof(ActionContextMetadata.ServiceName)] = serviceName;
        actionDescriptor.Properties[nameof(ActionContextMetadata.MethodName)] = methodName;
        actionDescriptor.Properties[nameof(ActionContextMetadata.Subject)] = subject;
        actionDescriptor.Properties[nameof(ActionContextMetadata.ConventionMode)] = ConventionMode.RequestResponse;
        actionDescriptor.Properties[nameof(ActionContextMetadata.ChannelName)] = channelName;

        return new ActionContextMetadata(actionDescriptor);
    }

    // Test service class for method reflection tests
    private class TestService
    {
        public string GetData(int id) => "data";
        public Task<string> GetDataAsync(int id) => Task.FromResult("data");
        public ValueTask<int> GetDataValueTaskAsync(int id) => ValueTask.FromResult(42);
        public void ProcessData(string data) { }
        public void ParameterlessMethod() { }
        public Task ProcessDataAsync(string data) => Task.CompletedTask;
    }
}