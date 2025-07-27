using System.Reflection;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Attributes;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;

/// <summary>
/// Interface for building action models from NATS service methods
/// </summary>
public interface IActionModelBuilder
{
    /// <summary>
    /// Creates an action model for a NATS service method
    /// </summary>
    /// <param name="controllerModel">Parent controller model</param>
    /// <param name="method">Method information</param>
    /// <param name="subjectAttribute">Subject attribute</param>
    /// <param name="setting">Auto convention setting</param>
    /// <param name="controllerRoute">Controller route</param>
    /// <returns>Action model</returns>
    ActionModel CreateNatsActionModel(ControllerModel controllerModel, MethodInfo method,
        SubjectAttribute subjectAttribute, AutoConventionSetting setting, string controllerRoute);
}