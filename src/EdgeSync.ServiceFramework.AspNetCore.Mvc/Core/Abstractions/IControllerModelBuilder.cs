using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;

/// <summary>
/// Interface for building controller models from NATS services
/// </summary>
public interface IControllerModelBuilder
{
    /// <summary>
    /// Creates NATS service models from application configuration
    /// </summary>
    /// <param name="application">Application model</param>
    /// <returns>Collection of controller models</returns>
    IEnumerable<ControllerModel> CreateNatsServiceModels(ApplicationModel application);
    
    /// <summary>
    /// Creates a controller model for a specific NATS service
    /// </summary>
    /// <param name="serviceType">Service type</param>
    /// <param name="application">Application model</param>
    /// <param name="controllerName">Controller name</param>
    /// <param name="setting">Auto convention setting</param>
    /// <returns>Controller model</returns>
    ControllerModel CreateNatsControllerModel(Type serviceType, ApplicationModel application,
        string controllerName, AutoConventionSetting setting);
}