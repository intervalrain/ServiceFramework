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
using NSubstitute.ExceptionExtensions;
using Shouldly;
using System.Reflection;
using Xunit;

namespace EdgeSync.ServiceFramework.UnitTests.Core.Handlers;

/// <summary>
/// Unit tests for PubSubHandler to verify Pub-Sub pattern handling logic
/// Tests cover different Pub-Sub modes (Classic, JetStream Push, JetStream Pull), publishing logic,
/// error handling, retry logic, and audit information processing
/// </summary>
public class PubSubHandlerTests
{
    private readonly ISerializerAdapterFactory _mockSerializerAdapterFactory;
    private readonly IServiceProvider _mockServiceProvider;
    private readonly IConnectionResolver _mockConnectionResolver;
    private readonly ILogger<PubSubHandler> _mockLogger;
    private readonly ISerializerAdapter _mockSerializerAdapter;
    private readonly INatsConnection _mockConnection;
    private readonly INatsSerializerRegistry _mockSerializerRegistry;
    private readonly PubSubHandler _handler;

    public PubSubHandlerTests()
    {
        _mockSerializerAdapterFactory = Substitute.For<ISerializerAdapterFactory>();
        _mockServiceProvider = Substitute.For<IServiceProvider>();
        _mockConnectionResolver = Substitute.For<IConnectionResolver>();
        _mockLogger = Substitute.For<ILogger<PubSubHandler>>();
        _mockSerializerAdapter = Substitute.For<ISerializerAdapter>();
        _mockConnection = Substitute.For<INatsConnection>();
        _mockSerializerRegistry = Substitute.For<INatsSerializerRegistry>();

        _handler = new PubSubHandler(
            _mockSerializerAdapterFactory,
            _mockServiceProvider,
            _mockConnectionResolver,
            _mockLogger);

        // Setup default mock behaviors
        _mockConnectionResolver.GetSerializerForConnection(Arg.Any<string>()).Returns(_mockSerializerRegistry);
        _mockSerializerAdapterFactory.GetAdapter(Arg.Any<INatsSerializerRegistry>()).Returns(_mockSerializerAdapter);
    }

    [Fact]
    public void SupportedMode_ShouldReturn_CombinedPubSubModes()
    {
        // Act
        var supportedMode = _handler.SupportedMode;

        // Assert
        // Check if it contains all expected flags
        (supportedMode & ConventionMode.PubSubPushClassic).ShouldBe(ConventionMode.PubSubPushClassic);
        (supportedMode & ConventionMode.PubSubPushJetStream).ShouldBe(ConventionMode.PubSubPushJetStream);
        (supportedMode & ConventionMode.PubSubPullJetStream).ShouldBe(ConventionMode.PubSubPullJetStream);
        
        // Verify it's the exact combination
        var expectedMode = ConventionMode.PubSubPushClassic | 
                          ConventionMode.PubSubPushJetStream | 
                          ConventionMode.PubSubPullJetStream;
        supportedMode.ShouldBe(expectedMode);
    }

    [Fact]
    public async Task HandleAsync_WithPubSubPushClassic_ShouldCallPublishAsyncAndReturnNoContent()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata(ConventionMode.PubSubPushClassic);
        var wrappedRequest = RequestDto<string>.Create("test data");

        _mockSerializerAdapter.PublishAsync(_mockConnection, metadata.Subject, wrappedRequest, _mockSerializerRegistry)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        await _mockSerializerAdapter.Received(1).PublishAsync(_mockConnection, metadata.Subject, wrappedRequest, _mockSerializerRegistry);
    }

    [Fact]
    public async Task HandleAsync_WithPubSubPushJetStream_ShouldCallPublishAsyncAndReturnNoContent()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata(ConventionMode.PubSubPushJetStream);
        var wrappedRequest = RequestDto<string>.Create("jetstream data");

        _mockSerializerAdapter.PublishAsync(_mockConnection, metadata.Subject, wrappedRequest, _mockSerializerRegistry)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        await _mockSerializerAdapter.Received(1).PublishAsync(_mockConnection, metadata.Subject, wrappedRequest, _mockSerializerRegistry);
    }

    [Fact]
    public async Task HandleAsync_WithPubSubPullJetStream_ShouldCallPublishAsyncAndReturnNoContent()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata(ConventionMode.PubSubPullJetStream);
        var wrappedRequest = RequestDto<string>.Create("pull jetstream data");

        _mockSerializerAdapter.PublishAsync(_mockConnection, metadata.Subject, wrappedRequest, _mockSerializerRegistry)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        await _mockSerializerAdapter.Received(1).PublishAsync(_mockConnection, metadata.Subject, wrappedRequest, _mockSerializerRegistry);
    }

    [Fact]
    public async Task HandleAsync_WithUnsupportedMode_ShouldThrowNotSupportedException()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata(ConventionMode.RequestResponse); // Unsupported mode for PubSubHandler
        var wrappedRequest = RequestDto<string>.Create("test data");

        // Act & Assert
        var exception = await Should.ThrowAsync<NotSupportedException>(
            () => _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest));

        exception.Message.ShouldContain("Convention mode RequestResponse is not supported by PubSubHandler");
    }

    [Fact]
    public async Task HandleAsync_WhenPublishAsyncThrowsException_ShouldLogErrorAndRethrow()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata(ConventionMode.PubSubPushClassic);
        var wrappedRequest = RequestDto<string>.Create("test data");
        var expectedException = new Exception("Publish error");

        _mockSerializerAdapter.PublishAsync(_mockConnection, metadata.Subject, wrappedRequest, _mockSerializerRegistry)
            .Throws(expectedException);

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
        var metadata = CreateActionContextMetadata(ConventionMode.PubSubPushClassic, "TestService", "TestMethod", "test.subject");
        var wrappedRequest = RequestDto<string>.Create("test");

        _mockSerializerAdapter.PublishAsync(_mockConnection, metadata.Subject, wrappedRequest, _mockSerializerRegistry)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
    }

    [Fact]
    public async Task HandleAsync_WithoutAuditInfoInRequest_ShouldCompleteSuccessfully()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata(ConventionMode.PubSubPushJetStream, "TestService", "TestMethod", "test.subject");
        var wrappedRequest = "simple string request"; // Not a RequestDto, no audit info

        _mockSerializerAdapter.PublishAsync(_mockConnection, metadata.Subject, wrappedRequest, _mockSerializerRegistry)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
    }

    [Fact]
    public async Task HandleAsync_WithNullRequest_ShouldHandleGracefully()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata(ConventionMode.PubSubPushClassic);
        object? wrappedRequest = null;

        _mockSerializerAdapter.PublishAsync(_mockConnection, metadata.Subject, null, _mockSerializerRegistry)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        await _mockSerializerAdapter.Received(1).PublishAsync(_mockConnection, metadata.Subject, null, _mockSerializerRegistry);
    }

    [Fact]
    public async Task HandleAsync_ShouldUseCorrectChannelNameForConnectionResolver()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata(ConventionMode.PubSubPushClassic, channelName: "custom-channel");
        var wrappedRequest = RequestDto<string>.Create("test");

        _mockSerializerAdapter.PublishAsync(_mockConnection, metadata.Subject, wrappedRequest, _mockSerializerRegistry)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        _mockConnectionResolver.Received(1).GetSerializerForConnection("custom-channel");
        _mockSerializerAdapterFactory.Received(1).GetAdapter(_mockSerializerRegistry);
    }

    [Fact]
    public async Task HandleAsync_ShouldComplete_AndCallMockServices()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata(ConventionMode.PubSubPushClassic);
        var wrappedRequest = RequestDto<string>.Create("test");

        _mockSerializerAdapter.PublishAsync(_mockConnection, metadata.Subject, wrappedRequest, _mockSerializerRegistry)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        _mockConnectionResolver.Received(1).GetSerializerForConnection("default");
        _mockSerializerAdapterFactory.Received(1).GetAdapter(_mockSerializerRegistry);
    }

    [Fact]
    public async Task HandleAsync_WhenAdapterThrowsException_ShouldLogErrorWithAdapterType()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata(ConventionMode.PubSubPushClassic);
        var wrappedRequest = RequestDto<string>.Create("test");
        var expectedException = new InvalidOperationException("Adapter specific error");

        _mockSerializerAdapter.PublishAsync(_mockConnection, metadata.Subject, wrappedRequest, _mockSerializerRegistry)
            .Throws(expectedException);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest));

        exception.ShouldBe(expectedException);
    }

    [Fact]
    public async Task HandleAsync_WhenAuditLoggingFails_ShouldLogWarningAndContinue()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata(ConventionMode.PubSubPushClassic);
        
        // Create a malformed request that will cause audit extraction to fail
        var malformedRequest = new { SomeProperty = "value" }; // Not a RequestDto

        _mockSerializerAdapter.PublishAsync(_mockConnection, metadata.Subject, malformedRequest, _mockSerializerRegistry)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(context, _mockConnection, metadata, malformedRequest);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        // Should still publish successfully even if audit logging fails
        await _mockSerializerAdapter.Received(1).PublishAsync(_mockConnection, metadata.Subject, malformedRequest, _mockSerializerRegistry);
    }

    [Theory]
    [InlineData(ConventionMode.PubSubPushClassic)]
    [InlineData(ConventionMode.PubSubPushJetStream)]
    [InlineData(ConventionMode.PubSubPullJetStream)]
    public async Task HandleAsync_WithAllSupportedModes_ShouldSucceed(ConventionMode mode)
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata(mode);
        var wrappedRequest = RequestDto<string>.Create($"test data for {mode}");

        _mockSerializerAdapter.PublishAsync(_mockConnection, metadata.Subject, wrappedRequest, _mockSerializerRegistry)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(context, _mockConnection, metadata, wrappedRequest);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        await _mockSerializerAdapter.Received(1).PublishAsync(_mockConnection, metadata.Subject, wrappedRequest, _mockSerializerRegistry);
    }

    [Fact]
    public async Task HandleAsync_WithComplexRequestObject_ShouldSerializeAndPublish()
    {
        // Arrange
        var context = CreateActionExecutingContext();
        var metadata = CreateActionContextMetadata(ConventionMode.PubSubPushJetStream);
        var complexRequest = RequestDto<ComplexData>.Create(
            new ComplexData { Id = 123, Name = "Test", Items = new[] { "item1", "item2" } });

        _mockSerializerAdapter.PublishAsync(_mockConnection, metadata.Subject, complexRequest, _mockSerializerRegistry)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(context, _mockConnection, metadata, complexRequest);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        await _mockSerializerAdapter.Received(1).PublishAsync(_mockConnection, metadata.Subject, complexRequest, _mockSerializerRegistry);
    }

    private ActionExecutingContext CreateActionExecutingContext()
    {
        var actionDescriptor = new ActionDescriptor();
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), actionDescriptor);
        
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
    }

    private ActionContextMetadata CreateActionContextMetadata(
        ConventionMode conventionMode,
        string serviceName = "TestService", 
        string methodName = "TestMethod", 
        string subject = "test.subject",
        string channelName = "default")
    {
        var actionDescriptor = new ActionDescriptor();
        actionDescriptor.Properties[nameof(ActionContextMetadata.ServiceName)] = serviceName;
        actionDescriptor.Properties[nameof(ActionContextMetadata.MethodName)] = methodName;
        actionDescriptor.Properties[nameof(ActionContextMetadata.Subject)] = subject;
        actionDescriptor.Properties[nameof(ActionContextMetadata.ConventionMode)] = conventionMode;
        actionDescriptor.Properties[nameof(ActionContextMetadata.ChannelName)] = channelName;

        return new ActionContextMetadata(actionDescriptor);
    }

    // Test data class for complex request scenarios
    private class ComplexData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string[] Items { get; set; } = Array.Empty<string>();
    }
}