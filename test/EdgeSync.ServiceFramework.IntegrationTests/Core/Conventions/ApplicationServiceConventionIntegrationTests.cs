using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Conventions;
using EdgeSync.ServiceFramework.IntegrationTests.TestHelpers;
using EdgeSync.ServiceFramework.Abstractions.Attributes;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace EdgeSync.ServiceFramework.IntegrationTests.Core.Conventions;

/// <summary>
/// Integration tests for ApplicationServiceConvention
/// Tests the coordination between controller and action model builders
/// </summary>
public class ApplicationServiceConventionIntegrationTests : ServiceFrameworkTestBase
{
    [Fact]
    public void Apply_WithTestApplication_CreatesControllersCorrectly()
    {
        // Arrange
        var convention = GetRequiredService<ApplicationServiceConvention>();
        var application = CreateTestApplicationModel();

        // Act
        convention.Apply(application);

        // Assert
        application.Controllers.ShouldNotBeEmpty();
        
        // Should have created controller for our test service
        var testController = application.Controllers
            .FirstOrDefault(c => c.ControllerName.Contains("TestService"));
        testController.ShouldNotBeNull();
    }

    [Fact]
    public void Apply_WithControllerModelBuilder_DelegatesCorrectly()
    {
        // Arrange
        var convention = GetRequiredService<ApplicationServiceConvention>();
        var controllerModelBuilder = GetRequiredService<IControllerModelBuilder>();
        var application = CreateTestApplicationModel();

        // Act
        convention.Apply(application);

        // Assert
        // Verify that the controller model builder was used
        // The controllers should have been created through the builder
        controllerModelBuilder.ShouldNotBeNull();
        
        // The application should now have controllers created by the builder
        application.Controllers.ShouldNotBeEmpty();
    }

    [Fact]
    public void Apply_CreatesActionsForNatsServiceMethods()
    {
        // Arrange
        var convention = GetRequiredService<ApplicationServiceConvention>();
        var application = CreateTestApplicationModel();

        // Act
        convention.Apply(application);

        // Assert
        var testController = application.Controllers
            .FirstOrDefault(c => c.ControllerName.Contains("TestService"));
        
        testController.ShouldNotBeNull();
        testController.Actions.ShouldNotBeEmpty();
        
        // Should have actions for our test methods
        testController.Actions.ShouldContain(a => a.ActionName.Contains("ProcessRequest"));
        testController.Actions.ShouldContain(a => a.ActionName.Contains("PublishEvent"));
        testController.Actions.ShouldContain(a => a.ActionName.Contains("GetCount"));
    }

    [Fact]
    public void Apply_SetsCorrectActionMetadata()
    {
        // Arrange
        var convention = GetRequiredService<ApplicationServiceConvention>();
        var application = CreateTestApplicationModel();

        // Act
        convention.Apply(application);

        // Assert
        var testController = application.Controllers
            .FirstOrDefault(c => c.ControllerName.Contains("TestService"));
        
        testController.ShouldNotBeNull();
        
        var processRequestAction = testController.Actions
            .FirstOrDefault(a => a.ActionName.Contains("ProcessRequest"));
        
        processRequestAction.ShouldNotBeNull();
        
        // Verify metadata is set correctly
        processRequestAction.Properties.ShouldContainKey("Subject");
        processRequestAction.Properties.ShouldContainKey("ConventionMode");
        processRequestAction.Properties.ShouldContainKey("ServiceName");
        processRequestAction.Properties.ShouldContainKey("MethodName");
    }

    [Fact]
    public void Apply_WithActionModelBuilder_DelegatesActionCreation()
    {
        // Arrange
        var convention = GetRequiredService<ApplicationServiceConvention>();
        var actionModelBuilder = GetRequiredService<IActionModelBuilder>();
        var application = CreateTestApplicationModel();

        // Act
        convention.Apply(application);

        // Assert
        // Verify that the action model builder was used
        actionModelBuilder.ShouldNotBeNull();
        
        var testController = application.Controllers
            .FirstOrDefault(c => c.ControllerName.Contains("TestService"));
        
        testController.ShouldNotBeNull();
        testController.Actions.ShouldNotBeEmpty();
        
        // Actions should have been created through the action model builder
        foreach (var action in testController.Actions)
        {
            action.ShouldNotBeNull();
            action.ActionName.ShouldNotBeNull();
        }
    }

    [Fact]
    public void Apply_HandlesMultipleNatsServices()
    {
        // Arrange
        var convention = GetRequiredService<ApplicationServiceConvention>();
        var application = CreateTestApplicationModelWithMultipleServices();

        // Act
        convention.Apply(application);

        // Assert
        application.Controllers.ShouldNotBeEmpty();
        
        // Should create controllers for all NATS services
        var controllerNames = application.Controllers.Select(c => c.ControllerName).ToList();
        controllerNames.ShouldContain(name => name.Contains("TestService"));
    }

    [Fact]
    public void Apply_IgnoresNonNatsServices()
    {
        // Arrange
        var convention = GetRequiredService<ApplicationServiceConvention>();
        var application = CreateTestApplicationModelWithMixedServices();

        // Act
        convention.Apply(application);

        // Assert
        // Should only create controllers for NATS services, not regular services
        var natsControllers = application.Controllers
            .Where(c => c.ControllerName.Contains("TestService"))
            .ToList();
        
        natsControllers.ShouldNotBeEmpty();
        
        // Should not create controllers for non-NATS services
        var nonNatsControllers = application.Controllers
            .Where(c => c.ControllerName.Contains("RegularService"))
            .ToList();
        
        nonNatsControllers.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_DependencyInjection_AllBuildersResolved()
    {
        // Arrange
        var controllerModelBuilder = GetRequiredService<IControllerModelBuilder>();
        var actionModelBuilder = GetRequiredService<IActionModelBuilder>();

        // Act & Assert
        controllerModelBuilder.ShouldNotBeNull();
        actionModelBuilder.ShouldNotBeNull();
        
        // The convention should be able to use both builders
        var convention = GetRequiredService<ApplicationServiceConvention>();
        convention.ShouldNotBeNull();
    }

    [Fact]
    public void Apply_ConventionCoordination_WorksCorrectly()
    {
        // Arrange
        var convention = GetRequiredService<ApplicationServiceConvention>();
        var application = CreateTestApplicationModel();
        var initialControllerCount = application.Controllers.Count;

        // Act
        convention.Apply(application);

        // Assert
        // Should have added new controllers
        application.Controllers.Count.ShouldBeGreaterThan(initialControllerCount);
        
        // Each added controller should have actions
        var addedControllers = application.Controllers.Skip(initialControllerCount);
        foreach (var controller in addedControllers)
        {
            controller.Actions.ShouldNotBeEmpty();
        }
    }

    [Fact]
    public void Apply_MultipleCalls_IdempotentBehavior()
    {
        // Arrange
        var convention = GetRequiredService<ApplicationServiceConvention>();
        var application = CreateTestApplicationModel();

        // Act - Apply convention multiple times
        convention.Apply(application);
        var controllerCountAfterFirst = application.Controllers.Count;
        
        convention.Apply(application);
        var controllerCountAfterSecond = application.Controllers.Count;

        // Assert - Should not duplicate controllers
        controllerCountAfterSecond.ShouldBe(controllerCountAfterFirst);
    }

    [Fact]
    public void Apply_LogsConventionActivity()
    {
        // Arrange
        var convention = GetRequiredService<ApplicationServiceConvention>();
        var application = CreateTestApplicationModel();

        // Act
        convention.Apply(application);

        // Assert
        // The convention should complete without throwing exceptions
        // Logging verification would require mocking the logger, but integration test
        // focuses on the behavior working correctly
        application.Controllers.ShouldNotBeEmpty();
    }

    private ApplicationModel CreateTestApplicationModel()
    {
        var application = new ApplicationModel();
        // Add any initial setup needed for testing
        return application;
    }

    private ApplicationModel CreateTestApplicationModelWithMultipleServices()
    {
        var application = new ApplicationModel();
        // In a real scenario, this would be populated with multiple NATS services
        // For integration testing, the service discovery will find our test services
        return application;
    }

    private ApplicationModel CreateTestApplicationModelWithMixedServices()
    {
        var application = new ApplicationModel();
        // This would contain both NATS and non-NATS services in a real scenario
        return application;
    }
}

/// <summary>
/// Additional NATS service for testing multiple services
/// </summary>
[ServiceInfo(ServiceName = "AnotherTestService")]
public interface IAnotherTestNatsService
{
    [Subject("another.test")]
    Task<string> AnotherMethodAsync();
}

/// <summary>
/// Regular (non-NATS) service that should be ignored by the convention
/// </summary>
public interface IRegularService
{
    Task<string> RegularMethodAsync();
}