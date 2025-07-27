using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Handlers;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.IntegrationTests.TestHelpers;
using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.IntegrationTests.Core.Handlers;

/// <summary>
/// Integration tests for ConventionModeHandlerFactory
/// Tests the factory pattern integration with all registered handlers
/// </summary>
public class ConventionModeHandlerFactoryIntegrationTests : ServiceFrameworkTestBase
{
    [Fact]
    public void GetHandler_WithRequestResponseMode_ReturnsNatsRequestResponseHandler()
    {
        // Arrange
        var factory = GetRequiredService<IConventionModeHandlerFactory>();

        // Act
        var handler = factory.GetHandler(ConventionMode.RequestResponse);

        // Assert
        handler.ShouldNotBeNull();
        handler.SupportedMode.ShouldBe(ConventionMode.RequestResponse);
        handler.ShouldBeOfType<NatsRequestResponseHandler>();
    }

    [Fact]
    public void GetHandler_WithPubSubPushClassicMode_ReturnsPubSubHandler()
    {
        // Arrange
        var factory = GetRequiredService<IConventionModeHandlerFactory>();

        // Act
        var handler = factory.GetHandler(ConventionMode.PubSubPushClassic);

        // Assert
        handler.ShouldNotBeNull();
        handler.SupportedMode.ShouldHaveFlag(ConventionMode.PubSubPushClassic);
        handler.ShouldBeOfType<PubSubHandler>();
    }

    [Fact]
    public void GetHandler_WithPubSubPushJetStreamMode_ReturnsPubSubHandler()
    {
        // Arrange
        var factory = GetRequiredService<IConventionModeHandlerFactory>();

        // Act
        var handler = factory.GetHandler(ConventionMode.PubSubPushJetStream);

        // Assert
        handler.ShouldNotBeNull();
        handler.SupportedMode.ShouldHaveFlag(ConventionMode.PubSubPushJetStream);
        handler.ShouldBeOfType<PubSubHandler>();
    }

    [Fact]
    public void GetHandler_WithPubSubPullJetStreamMode_ReturnsPubSubHandler()
    {
        // Arrange
        var factory = GetRequiredService<IConventionModeHandlerFactory>();

        // Act
        var handler = factory.GetHandler(ConventionMode.PubSubPullJetStream);

        // Assert
        handler.ShouldNotBeNull();
        handler.SupportedMode.ShouldHaveFlag(ConventionMode.PubSubPullJetStream);
        handler.ShouldBeOfType<PubSubHandler>();
    }

    [Fact]
    public void GetHandler_WithUnsupportedMode_ThrowsNotSupportedException()
    {
        // Arrange
        var factory = GetRequiredService<IConventionModeHandlerFactory>();

        // Act & Assert
        var exception = Should.Throw<NotSupportedException>(() => 
            factory.GetHandler((ConventionMode)999));
            
        exception.Message.ShouldContain("No handler found for convention mode");
    }

    [Fact]
    public void Factory_RegistersAllExpectedHandlers()
    {
        // Arrange
        var factory = GetRequiredService<IConventionModeHandlerFactory>();
        var handlerServices = GetServices<IConventionModeHandler>().ToList();

        // Act & Assert
        handlerServices.ShouldNotBeEmpty();
        handlerServices.Count.ShouldBeGreaterThanOrEqualTo(2); // At least RequestResponse and PubSub handlers
        
        // Verify we have the expected handler types
        handlerServices.ShouldContain(h => h is NatsRequestResponseHandler);
        handlerServices.ShouldContain(h => h is PubSubHandler);
    }

    [Fact]
    public void Factory_HandlerRegistration_IsConsistent()
    {
        // Arrange
        var factory = GetRequiredService<IConventionModeHandlerFactory>();

        // Act - Get the same handler multiple times
        var handler1 = factory.GetHandler(ConventionMode.RequestResponse);
        var handler2 = factory.GetHandler(ConventionMode.RequestResponse);

        // Assert - Should return the same handler type (factory uses DI, so instances may differ but type should be consistent)
        handler1.GetType().ShouldBe(handler2.GetType());
        handler1.SupportedMode.ShouldBe(handler2.SupportedMode);
    }

    [Fact]
    public void Factory_SupportsAllDefinedConventionModes()
    {
        // Arrange
        var factory = GetRequiredService<IConventionModeHandlerFactory>();
        var allModes = Enum.GetValues<ConventionMode>()
            .Where(mode => mode != (ConventionMode)0) // Skip None/default value
            .ToList();

        // Act & Assert
        foreach (var mode in allModes)
        {
            if (mode == ConventionMode.RequestResponse)
            {
                // Should have dedicated handler
                Should.NotThrow(() => factory.GetHandler(mode));
            }
            else if (mode.HasFlag(ConventionMode.PubSubPushClassic) || 
                     mode.HasFlag(ConventionMode.PubSubPushJetStream) || 
                     mode.HasFlag(ConventionMode.PubSubPullJetStream))
            {
                // Should be handled by PubSubHandler
                Should.NotThrow(() => factory.GetHandler(mode));
            }
        }
    }

    [Theory]
    [InlineData(ConventionMode.RequestResponse)]
    [InlineData(ConventionMode.PubSubPushClassic)]
    [InlineData(ConventionMode.PubSubPushJetStream)]
    [InlineData(ConventionMode.PubSubPullJetStream)]
    public void GetHandler_WithValidMode_ReturnsHandlerWithMatchingSupportedMode(ConventionMode mode)
    {
        // Arrange
        var factory = GetRequiredService<IConventionModeHandlerFactory>();

        // Act
        var handler = factory.GetHandler(mode);

        // Assert
        handler.ShouldNotBeNull();
        
        // For flag-based enums, the handler's supported mode should include the requested mode
        if (mode == ConventionMode.RequestResponse)
        {
            handler.SupportedMode.ShouldBe(mode);
        }
        else
        {
            handler.SupportedMode.ShouldHaveFlag(mode);
        }
    }

    [Fact]
    public void Factory_Integration_WithDependencyInjection_ResolvesHandlersCorrectly()
    {
        // Arrange
        var factory = GetRequiredService<IConventionModeHandlerFactory>();
        
        // Act - Get handlers through factory
        var requestResponseHandler = factory.GetHandler(ConventionMode.RequestResponse);
        var pubSubHandler = factory.GetHandler(ConventionMode.PubSubPushClassic);

        // Assert - Verify the handlers have their dependencies injected
        requestResponseHandler.ShouldNotBeNull();
        pubSubHandler.ShouldNotBeNull();
        
        // Verify handler types are correctly resolved
        requestResponseHandler.ShouldBeOfType<NatsRequestResponseHandler>();
        pubSubHandler.ShouldBeOfType<PubSubHandler>();
        
        // Verify each handler supports the expected modes
        requestResponseHandler.SupportedMode.ShouldBe(ConventionMode.RequestResponse);
        pubSubHandler.SupportedMode.ShouldHaveFlag(ConventionMode.PubSubPushClassic);
    }
}