using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Handlers;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Core.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using Shouldly;
using Xunit;

namespace EdgeSync.ServiceFramework.UnitTests.Core.Handlers;

/// <summary>
/// Unit tests for ConventionModeHandlerFactory to verify handler registration and retrieval
/// Tests the factory pattern implementation and mode-based handler selection
/// </summary>
public class ConventionModeHandlerFactoryTests
{
    private readonly ILogger<ConventionModeHandlerFactory> _mockLogger;
    private readonly IConventionModeHandler _mockRequestResponseHandler;
    private readonly IConventionModeHandler _mockPubSubPushJetStreamHandler;
    private readonly IConventionModeHandler _mockPubSubPullJetStreamHandler;
    private readonly IConventionModeHandler _mockPubSubPushClassicHandler;

    public ConventionModeHandlerFactoryTests()
    {
        _mockLogger = Substitute.For<ILogger<ConventionModeHandlerFactory>>();
        
        _mockRequestResponseHandler = Substitute.For<IConventionModeHandler>();
        _mockRequestResponseHandler.SupportedMode.Returns(ConventionMode.RequestResponse);
        
        _mockPubSubPushJetStreamHandler = Substitute.For<IConventionModeHandler>();
        _mockPubSubPushJetStreamHandler.SupportedMode.Returns(ConventionMode.PubSubPushJetStream);
        
        _mockPubSubPullJetStreamHandler = Substitute.For<IConventionModeHandler>();
        _mockPubSubPullJetStreamHandler.SupportedMode.Returns(ConventionMode.PubSubPullJetStream);
        
        _mockPubSubPushClassicHandler = Substitute.For<IConventionModeHandler>();
        _mockPubSubPushClassicHandler.SupportedMode.Returns(ConventionMode.PubSubPushClassic);
    }

    [Fact]
    public void Constructor_WithHandlers_RegistersAllHandlers()
    {
        // Arrange
        var handlers = new[]
        {
            _mockRequestResponseHandler,
            _mockPubSubPushJetStreamHandler,
            _mockPubSubPullJetStreamHandler,
            _mockPubSubPushClassicHandler
        };

        // Act
        var factory = new ConventionModeHandlerFactory(handlers, _mockLogger);

        // Assert
        var allHandlers = factory.GetAllHandlers();
        allHandlers.Count.ShouldBe(4);
        allHandlers[ConventionMode.RequestResponse].ShouldBe(_mockRequestResponseHandler);
        allHandlers[ConventionMode.PubSubPushJetStream].ShouldBe(_mockPubSubPushJetStreamHandler);
        allHandlers[ConventionMode.PubSubPullJetStream].ShouldBe(_mockPubSubPullJetStreamHandler);
        allHandlers[ConventionMode.PubSubPushClassic].ShouldBe(_mockPubSubPushClassicHandler);
    }

    [Fact]
    public void Constructor_WithEmptyHandlers_CreatesEmptyFactory()
    {
        // Arrange
        var handlers = Array.Empty<IConventionModeHandler>();

        // Act
        var factory = new ConventionModeHandlerFactory(handlers, _mockLogger);

        // Assert
        var allHandlers = factory.GetAllHandlers();
        allHandlers.Count.ShouldBe(0);
    }

    [Fact]
    public void GetHandler_WithRegisteredMode_ReturnsCorrectHandler()
    {
        // Arrange
        var handlers = new[] { _mockRequestResponseHandler };
        var factory = new ConventionModeHandlerFactory(handlers, _mockLogger);

        // Act
        var result = factory.GetHandler(ConventionMode.RequestResponse);

        // Assert
        result.ShouldBe(_mockRequestResponseHandler);
    }

    [Fact]
    public void GetHandler_WithUnregisteredMode_ThrowsNotSupportedException()
    {
        // Arrange
        var handlers = new[] { _mockRequestResponseHandler };
        var factory = new ConventionModeHandlerFactory(handlers, _mockLogger);

        // Act & Assert
        var exception = Should.Throw<NotSupportedException>(() =>
            factory.GetHandler(ConventionMode.PubSubPushJetStream));
        
        exception.Message.ShouldContain("No handler found for convention mode: PubSubPushJetStream");
        exception.Message.ShouldContain("Supported modes: RequestResponse");
    }

    [Fact]
    public void RegisterHandler_WithValidHandler_AddsToHandlers()
    {
        // Arrange
        var factory = new ConventionModeHandlerFactory(Array.Empty<IConventionModeHandler>(), _mockLogger);

        // Act
        factory.RegisterHandler(_mockRequestResponseHandler);

        // Assert
        var allHandlers = factory.GetAllHandlers();
        allHandlers.Count.ShouldBe(1);
        allHandlers[ConventionMode.RequestResponse].ShouldBe(_mockRequestResponseHandler);
    }

    [Fact]
    public void RegisterHandler_WithNullHandler_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new ConventionModeHandlerFactory(Array.Empty<IConventionModeHandler>(), _mockLogger);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            factory.RegisterHandler(null!));
    }

    [Fact]
    public void RegisterHandler_WithDuplicateMode_ReplacesExistingHandler()
    {
        // Arrange
        var handlers = new[] { _mockRequestResponseHandler };
        var factory = new ConventionModeHandlerFactory(handlers, _mockLogger);
        
        var newHandler = Substitute.For<IConventionModeHandler>();
        newHandler.SupportedMode.Returns(ConventionMode.RequestResponse);

        // Act
        factory.RegisterHandler(newHandler);

        // Assert
        var allHandlers = factory.GetAllHandlers();
        allHandlers.Count.ShouldBe(1);
        allHandlers[ConventionMode.RequestResponse].ShouldBe(newHandler);
        allHandlers[ConventionMode.RequestResponse].ShouldNotBe(_mockRequestResponseHandler);
    }

    [Fact]
    public void RegisterHandler_WithDuplicateMode_LogsWarning()
    {
        // Arrange
        var handlers = new[] { _mockRequestResponseHandler };
        var factory = new ConventionModeHandlerFactory(handlers, _mockLogger);
        
        // Clear any previous calls from constructor
        _mockLogger.ClearReceivedCalls();
        
        var newHandler = Substitute.For<IConventionModeHandler>();
        newHandler.SupportedMode.Returns(ConventionMode.RequestResponse);

        // Act
        factory.RegisterHandler(newHandler);

        // Assert
        // Should log both warning about replacement and debug message for registration
        _mockLogger.ReceivedWithAnyArgs().LogWarning(default(string), default(object?[]));
        _mockLogger.ReceivedWithAnyArgs().LogDebug(default(string), default(object?[]));
    }

    [Fact]
    public void IsHandlerRegistered_WithRegisteredMode_ReturnsTrue()
    {
        // Arrange
        var handlers = new[] { _mockRequestResponseHandler };
        var factory = new ConventionModeHandlerFactory(handlers, _mockLogger);

        // Act
        var result = factory.IsHandlerRegistered(ConventionMode.RequestResponse);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsHandlerRegistered_WithUnregisteredMode_ReturnsFalse()
    {
        // Arrange
        var handlers = new[] { _mockRequestResponseHandler };
        var factory = new ConventionModeHandlerFactory(handlers, _mockLogger);

        // Act
        var result = factory.IsHandlerRegistered(ConventionMode.PubSubPushJetStream);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void GetAllHandlers_ReturnsReadOnlyDictionary()
    {
        // Arrange
        var handlers = new[] { _mockRequestResponseHandler, _mockPubSubPushJetStreamHandler };
        var factory = new ConventionModeHandlerFactory(handlers, _mockLogger);

        // Act
        var result = factory.GetAllHandlers();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ContainsKey(ConventionMode.RequestResponse).ShouldBeTrue();
        result.ContainsKey(ConventionMode.PubSubPushJetStream).ShouldBeTrue();
        
        // Verify it's read-only
        result.ShouldBeAssignableTo<IReadOnlyDictionary<ConventionMode, IConventionModeHandler>>();
    }

    [Fact]
    public void Constructor_LogsInitializationMessage()
    {
        // Arrange
        var handlers = new[] { _mockRequestResponseHandler, _mockPubSubPushJetStreamHandler };

        // Act
        var factory = new ConventionModeHandlerFactory(handlers, _mockLogger);

        // Assert
        // Should log initialization message plus debug messages for each handler registration
        _mockLogger.ReceivedWithAnyArgs().LogInformation(default(string), default(object?[]));
        _mockLogger.ReceivedWithAnyArgs().LogDebug(default(string), default(object?[]));
    }

    [Fact]
    public void GetHandler_LogsDebugMessage()
    {
        // Arrange
        var handlers = new[] { _mockRequestResponseHandler };
        var factory = new ConventionModeHandlerFactory(handlers, _mockLogger);
        
        // Clear any previous calls from constructor
        _mockLogger.ClearReceivedCalls();

        // Act
        factory.GetHandler(ConventionMode.RequestResponse);

        // Assert
        _mockLogger.ReceivedWithAnyArgs(1).LogDebug(default(string), default(object?[]));
    }

    [Fact]
    public void GetHandler_WithUnsupportedMode_LogsError()
    {
        // Arrange
        var handlers = new[] { _mockRequestResponseHandler };
        var factory = new ConventionModeHandlerFactory(handlers, _mockLogger);
        
        // Clear any previous calls from constructor
        _mockLogger.ClearReceivedCalls();

        // Act & Assert
        Should.Throw<NotSupportedException>(() =>
            factory.GetHandler(ConventionMode.PubSubPushJetStream));
        
        _mockLogger.ReceivedWithAnyArgs(1).LogError(default(string));
    }

    [Fact]
    public void RegisterHandler_LogsDebugMessage()
    {
        // Arrange
        var factory = new ConventionModeHandlerFactory(Array.Empty<IConventionModeHandler>(), _mockLogger);
        
        // Clear any previous calls from constructor
        _mockLogger.ClearReceivedCalls();

        // Act
        factory.RegisterHandler(_mockRequestResponseHandler);

        // Assert
        _mockLogger.ReceivedWithAnyArgs(1).LogDebug(default(string), default(object?[]));
    }

    [Theory]
    [InlineData(ConventionMode.RequestResponse)]
    [InlineData(ConventionMode.PubSubPushJetStream)]
    [InlineData(ConventionMode.PubSubPullJetStream)]
    [InlineData(ConventionMode.PubSubPushClassic)]
    public void GetHandler_WithAllConventionModes_ReturnsCorrectHandler(ConventionMode mode)
    {
        // Arrange
        var handlers = new[]
        {
            _mockRequestResponseHandler,
            _mockPubSubPushJetStreamHandler,
            _mockPubSubPullJetStreamHandler,
            _mockPubSubPushClassicHandler
        };
        var factory = new ConventionModeHandlerFactory(handlers, _mockLogger);

        // Act
        var result = factory.GetHandler(mode);

        // Assert
        result.ShouldNotBeNull();
        result.SupportedMode.ShouldBe(mode);
    }

    [Fact]
    public void Factory_WithMultipleHandlersOfSameType_WorksCorrectly()
    {
        // Arrange
        var handler1 = Substitute.For<IConventionModeHandler>();
        handler1.SupportedMode.Returns(ConventionMode.RequestResponse);
        
        var handler2 = Substitute.For<IConventionModeHandler>();
        handler2.SupportedMode.Returns(ConventionMode.RequestResponse);
        
        var handlers = new[] { handler1, handler2 };

        // Act
        var factory = new ConventionModeHandlerFactory(handlers, _mockLogger);

        // Assert
        var allHandlers = factory.GetAllHandlers();
        allHandlers.Count.ShouldBe(1); // Second handler should replace the first
        allHandlers[ConventionMode.RequestResponse].ShouldBe(handler2); // Last one wins
    }
}