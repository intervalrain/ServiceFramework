using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Attributes;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Handlers;

/// <summary>
/// Interface for convention handlers
/// </summary>
public interface IConventionHandler
{
    /// <summary>
    /// The convention mode this handler supports
    /// </summary>
    ConventionMode SupportedMode { get; }
    
    /// <summary>
    /// Configure the action model based on the convention mode
    /// </summary>
    /// <param name="actionModel">Action model to configure</param>
    /// <param name="method">Method information</param>
    /// <param name="subjectAttribute">Subject attribute</param>
    /// <param name="setting">Auto convention setting</param>
    /// <param name="controllerRoute">Controller route</param>
    void ConfigureAction(
        ActionModel actionModel,
        MethodInfo method,
        SubjectAttribute subjectAttribute,
        AutoConventionSetting setting,
        string controllerRoute);
}