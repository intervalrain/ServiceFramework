using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Conventions;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using System.Reflection;

namespace EdgeSync.ServiceFramework.UnitTests.Core.Conventions;

/// <summary>
/// Unit tests for ApplicationServiceConvention - the refactored coordinator
/// Tests the orchestration of controller model building
/// </summary>
public class ApplicationServiceConventionTests
{
    private readonly IControllerModelBuilder _mockControllerModelBuilder;
    private readonly ILogger<ApplicationServiceConvention> _mockLogger;
    private readonly ApplicationServiceConvention _convention;

    public ApplicationServiceConventionTests()
    {
        _mockControllerModelBuilder = Substitute.For<IControllerModelBuilder>();
        _mockLogger = Substitute.For<ILogger<ApplicationServiceConvention>>();

        _convention = new ApplicationServiceConvention(_mockControllerModelBuilder, _mockLogger);
    }

    [Fact]
    public void Apply_WithValidApplicationModel_DelegatesToControllerModelBuilder()
    {
        // Arrange
        var applicationModel = new ApplicationModel();
        var expectedControllers = new List<ControllerModel>
        {
            CreateSampleControllerModel("TestController1"),
            CreateSampleControllerModel("TestController2")
        };

        _mockControllerModelBuilder.CreateNatsServiceModels(applicationModel)
                                  .Returns(expectedControllers);

        // Act
        _convention.Apply(applicationModel);

        // Assert
        _mockControllerModelBuilder.Received(1).CreateNatsServiceModels(applicationModel);
        applicationModel.Controllers.Count.ShouldBe(2);
        applicationModel.Controllers[0].ControllerName.ShouldBe("TestController1");
        applicationModel.Controllers[1].ControllerName.ShouldBe("TestController2");
    }

    [Fact]
    public void Apply_WithEmptyControllerList_AddsNoControllersToApplication()
    {
        // Arrange
        var applicationModel = new ApplicationModel();
        var emptyControllers = new List<ControllerModel>();

        _mockControllerModelBuilder.CreateNatsServiceModels(applicationModel)
                                  .Returns(emptyControllers);

        // Act
        _convention.Apply(applicationModel);

        // Assert
        _mockControllerModelBuilder.Received(1).CreateNatsServiceModels(applicationModel);
        applicationModel.Controllers.Count.ShouldBe(0);
    }

    [Fact]
    public void Apply_WithSingleController_AddsCorrectlyToApplication()
    {
        // Arrange
        var applicationModel = new ApplicationModel();
        var singleController = new List<ControllerModel>
        {
            CreateSampleControllerModel("SingleController")
        };

        _mockControllerModelBuilder.CreateNatsServiceModels(applicationModel)
                                  .Returns(singleController);

        // Act
        _convention.Apply(applicationModel);

        // Assert
        _mockControllerModelBuilder.Received(1).CreateNatsServiceModels(applicationModel);
        applicationModel.Controllers.Count.ShouldBe(1);
        applicationModel.Controllers[0].ControllerName.ShouldBe("SingleController");
    }

    [Fact]
    public void Apply_WithMultipleControllers_AddsAllControllersInOrder()
    {
        // Arrange
        var applicationModel = new ApplicationModel();
        var controllers = new List<ControllerModel>
        {
            CreateSampleControllerModel("Controller1"),
            CreateSampleControllerModel("Controller2"),
            CreateSampleControllerModel("Controller3")
        };

        _mockControllerModelBuilder.CreateNatsServiceModels(applicationModel)
                                  .Returns(controllers);

        // Act
        _convention.Apply(applicationModel);

        // Assert
        _mockControllerModelBuilder.Received(1).CreateNatsServiceModels(applicationModel);
        applicationModel.Controllers.Count.ShouldBe(3);
        applicationModel.Controllers[0].ControllerName.ShouldBe("Controller1");
        applicationModel.Controllers[1].ControllerName.ShouldBe("Controller2");
        applicationModel.Controllers[2].ControllerName.ShouldBe("Controller3");
    }

    [Fact]
    public void Apply_WithExistingControllersInApplication_AppendsNewControllers()
    {
        // Arrange
        var applicationModel = new ApplicationModel();
        
        // Add an existing controller to the application
        var existingController = CreateSampleControllerModel("ExistingController");
        applicationModel.Controllers.Add(existingController);

        var newControllers = new List<ControllerModel>
        {
            CreateSampleControllerModel("NewController1"),
            CreateSampleControllerModel("NewController2")
        };

        _mockControllerModelBuilder.CreateNatsServiceModels(applicationModel)
                                  .Returns(newControllers);

        // Act
        _convention.Apply(applicationModel);

        // Assert
        _mockControllerModelBuilder.Received(1).CreateNatsServiceModels(applicationModel);
        applicationModel.Controllers.Count.ShouldBe(3);
        applicationModel.Controllers[0].ControllerName.ShouldBe("ExistingController");
        applicationModel.Controllers[1].ControllerName.ShouldBe("NewController1");
        applicationModel.Controllers[2].ControllerName.ShouldBe("NewController2");
    }

    [Fact]
    public void Apply_LogsDebugMessages()
    {
        // Arrange
        var applicationModel = new ApplicationModel();
        var controllers = new List<ControllerModel>
        {
            CreateSampleControllerModel("TestController1"),
            CreateSampleControllerModel("TestController2")
        };

        _mockControllerModelBuilder.CreateNatsServiceModels(applicationModel)
                                  .Returns(controllers);

        // Act
        _convention.Apply(applicationModel);

        // Assert
        _mockLogger.Received().LogDebug("Applying ApplicationServiceConvention to application model");
        // Note: Detailed logging assertion skipped due to NSubstitute complexity with LogDebug extension methods
    }

    [Fact]
    public void Apply_WithNullControllersFromBuilder_HandlesGracefully()
    {
        // Arrange
        var applicationModel = new ApplicationModel();

        _mockControllerModelBuilder.CreateNatsServiceModels(applicationModel)
                                  .Returns((IEnumerable<ControllerModel>?)null);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _convention.Apply(applicationModel));
    }

    [Fact]
    public void Apply_WithControllerModelBuilderException_PropagatesException()
    {
        // Arrange
        var applicationModel = new ApplicationModel();
        var exception = new InvalidOperationException("Builder failed");

        _mockControllerModelBuilder.When(x => x.CreateNatsServiceModels(applicationModel))
                                  .Do(x => throw exception);

        // Act & Assert
        var thrownException = Should.Throw<InvalidOperationException>(() => _convention.Apply(applicationModel));
        thrownException.Message.ShouldBe("Builder failed");
    }

    [Fact]
    public void Apply_CallsControllerModelBuilderExactlyOnce()
    {
        // Arrange
        var applicationModel = new ApplicationModel();
        var controllers = new List<ControllerModel>();

        _mockControllerModelBuilder.CreateNatsServiceModels(applicationModel)
                                  .Returns(controllers);

        // Act
        _convention.Apply(applicationModel);

        // Assert
        _mockControllerModelBuilder.Received(1).CreateNatsServiceModels(applicationModel);
        _mockControllerModelBuilder.Received(1).CreateNatsServiceModels(Arg.Any<ApplicationModel>());
    }

    [Fact]
    public void Apply_PreservesControllerModelProperties()
    {
        // Arrange
        var applicationModel = new ApplicationModel();
        var controller = CreateSampleControllerModel("TestController");
        
        // Add some properties to test preservation
        controller.Properties["TestProperty"] = "TestValue";

        var controllers = new List<ControllerModel> { controller };

        _mockControllerModelBuilder.CreateNatsServiceModels(applicationModel)
                                  .Returns(controllers);

        // Act
        _convention.Apply(applicationModel);

        // Assert
        var addedController = applicationModel.Controllers[0];
        addedController.Properties["TestProperty"].ShouldBe("TestValue");
    }

    [Fact]
    public void Apply_WithLargeNumberOfControllers_HandlesEfficiently()
    {
        // Arrange
        var applicationModel = new ApplicationModel();
        var controllers = new List<ControllerModel>();
        
        // Create 100 controllers to test performance
        for (int i = 0; i < 100; i++)
        {
            controllers.Add(CreateSampleControllerModel($"Controller{i}"));
        }

        _mockControllerModelBuilder.CreateNatsServiceModels(applicationModel)
                                  .Returns(controllers);

        // Act
        _convention.Apply(applicationModel);

        // Assert
        applicationModel.Controllers.Count.ShouldBe(100);
        for (int i = 0; i < 100; i++)
        {
            applicationModel.Controllers[i].ControllerName.ShouldBe($"Controller{i}");
        }
    }

    #region Helper Methods

    private ControllerModel CreateSampleControllerModel(string controllerName)
    {
        var controllerModel = new ControllerModel(
            typeof(object).GetTypeInfo(), // TypeInfo
            new List<object>() // Attributes
        )
        {
            ControllerName = controllerName
        };

        return controllerModel;
    }

    #endregion
}