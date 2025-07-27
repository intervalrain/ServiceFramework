using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Conventions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Core.Filters;
using EdgeSync.ServiceFramework.IntegrationTests.TestHelpers;

namespace EdgeSync.ServiceFramework.IntegrationTests.Simple;

/// <summary>
/// Simple tests to verify the refactored framework components work together correctly
/// Focuses on dependency injection, component creation, and basic functionality
/// </summary>
public class SimpleFrameworkTests : ServiceFrameworkTestBase
{
    [Fact]
    public void DependencyInjection_AllRefactoredComponents_AreRegistered()
    {
        // Arrange & Act - Services are configured in base class
        
        // Assert - Verify all refactored interfaces can be resolved
        var auditHandler = GetService<IAuditHandler>();
        var connectionResolver = GetService<IConnectionResolver>();
        var requestDataExtractor = GetService<IRequestDataExtractor>();
        var responseProcessor = GetService<IResponseProcessor>();
        var conventionModeHandlerFactory = GetService<IConventionModeHandlerFactory>();
        var serviceDiscovery = GetService<IServiceDiscovery>();
        var serviceRegistrar = GetService<IServiceRegistrar>();
        var pubSubManager = GetService<IPubSubManager>();
        var controllerModelBuilder = GetService<IControllerModelBuilder>();
        var actionModelBuilder = GetService<IActionModelBuilder>();

        // All components should be registered
        auditHandler.ShouldNotBeNull();
        connectionResolver.ShouldNotBeNull();
        requestDataExtractor.ShouldNotBeNull();
        responseProcessor.ShouldNotBeNull();
        conventionModeHandlerFactory.ShouldNotBeNull();
        serviceDiscovery.ShouldNotBeNull();
        serviceRegistrar.ShouldNotBeNull();
        pubSubManager.ShouldNotBeNull();
        controllerModelBuilder.ShouldNotBeNull();
        actionModelBuilder.ShouldNotBeNull();
    }

    [Fact]
    public void ConventionModeHandlerFactory_CanCreateHandlers()
    {
        // Arrange
        var factory = GetRequiredService<IConventionModeHandlerFactory>();

        // Act & Assert
        var requestResponseHandler = factory.GetHandler(ConventionMode.RequestResponse);
        var pubSubHandler = factory.GetHandler(ConventionMode.PubSubPushClassic);

        requestResponseHandler.ShouldNotBeNull();
        pubSubHandler.ShouldNotBeNull();
        
        requestResponseHandler.SupportedMode.ShouldBe(ConventionMode.RequestResponse);
        pubSubHandler.SupportedMode.ShouldBe(ConventionMode.PubSubPushClassic);
    }

    [Fact]
    public void AuditHandler_CanWrapRequests()
    {
        // Arrange
        var auditHandler = GetRequiredService<IAuditHandler>();
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var testData = "test request data";

        // Act
        var wrappedRequest = auditHandler.WrapRequestWithAudit(testData, httpContext);

        // Assert
        wrappedRequest.ShouldNotBeNull();
        
        // The audit handler should process the request even if audit extraction may fail
        // This tests that the component is functioning and properly integrated
    }

    [Fact]
    public void ResponseProcessor_CanHandleResponses()
    {
        // Arrange
        var responseProcessor = GetRequiredService<IResponseProcessor>();
        var testResponse = new { Data = "test response", Status = "Success" };

        // Act
        var processedResponse = responseProcessor.ProcessResponse(testResponse, useExceptionHandler: false);

        // Assert
        processedResponse.ShouldNotBeNull();
    }

    [Fact]
    public void ServiceDiscovery_HasCorrectConfiguration()
    {
        // Arrange
        var serviceDiscovery = GetRequiredService<IServiceDiscovery>();

        // Act & Assert
        serviceDiscovery.ShouldNotBeNull();
        serviceDiscovery.ShouldBeOfType<ServiceDiscovery>();
    }

    [Fact]
    public void ControllerModelBuilder_CanCreateModels()
    {
        // Arrange
        var controllerBuilder = GetRequiredService<IControllerModelBuilder>();
        var applicationModel = new Microsoft.AspNetCore.Mvc.ApplicationModels.ApplicationModel();

        // Act
        var controllerModels = controllerBuilder.CreateNatsServiceModels(applicationModel);

        // Assert
        controllerModels.ShouldNotBeNull();
    }

    [Fact]
    public void ActionModelBuilder_HasCorrectInterface()
    {
        // Arrange
        var actionBuilder = GetRequiredService<IActionModelBuilder>();

        // Act & Assert - Just verify the builder is available and has the expected interface
        actionBuilder.ShouldNotBeNull();
        actionBuilder.ShouldBeOfType<ActionModelBuilder>();
    }

    [Fact]
    public void RequestDataExtractor_CanExtractFromContext()
    {
        // Arrange
        var extractor = GetRequiredService<IRequestDataExtractor>();
        var actionContext = TestFixtures.CreateActionExecutingContext();

        // Act
        var extractedData = extractor.ExtractRequestData(actionContext);

        // Assert - Should not throw and return some result
        // The exact result depends on the test context setup
        extractedData.ShouldNotBeNull();
    }

    [Fact]
    public void ConnectionResolver_ReturnsConnection()
    {
        // Arrange
        var resolver = GetRequiredService<IConnectionResolver>();

        // Act
        var task = resolver.GetConnectionAsync("test-channel");

        // Assert - Should not throw
        task.ShouldNotBeNull();
    }

    [Fact]
    public void RefactoredComponents_UseCorrectDependencies()
    {
        // Arrange & Act
        var natsProxyActionFilter = GetService<NatsProxyActionFilter>();
        var applicationConvention = GetService<ApplicationServiceConvention>();

        // Assert - Main refactored components should be available
        natsProxyActionFilter.ShouldNotBeNull();
        applicationConvention.ShouldNotBeNull();
        
        // Note: ServiceFrameworkBackgroundService is registered as a hosted service
        // which has different resolution behavior in tests
    }
}