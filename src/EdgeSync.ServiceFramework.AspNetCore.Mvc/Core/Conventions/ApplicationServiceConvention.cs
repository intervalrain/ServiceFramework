using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Logging;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Conventions;

/// <summary>
/// Convention for automatically creating controllers from NatsService classes
/// Refactored to coordinator pattern following SOLID principles
/// </summary>
public class ApplicationServiceConvention : IApplicationModelConvention
{
    private readonly IControllerModelBuilder _controllerModelBuilder;
    private readonly ILogger<ApplicationServiceConvention> _logger;

    public ApplicationServiceConvention(
        IControllerModelBuilder controllerModelBuilder,
        ILogger<ApplicationServiceConvention> logger)
    {
        _controllerModelBuilder = controllerModelBuilder;
        _logger = logger;
    }

    public void Apply(ApplicationModel application)
    {
        ArgumentNullException.ThrowIfNull(application);
        
        _logger.LogDebug("Applying ApplicationServiceConvention to application model");

        var controllers = _controllerModelBuilder.CreateNatsServiceModels(application);
        
        if (controllers == null)
        {
            throw new ArgumentNullException(nameof(controllers), "Controller model builder returned null controllers");
        }

        var controllerCount = 0;
        foreach (var controller in controllers)
        {
            application.Controllers.Add(controller);
            controllerCount++;
        }

        _logger.LogDebug("ApplicationServiceConvention applied: {ControllerCount} controllers added", controllerCount);
    }

}