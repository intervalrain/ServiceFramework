using System.Reflection;

using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Abstractions;

public interface IAutoConventionRouteBuilder
{
    string GetServiceName(string serviceName);
    string BuildControllerRoute(Type serviceType, string controllerName, AutoConventionSetting setting);
    string BuildActionRoute(ControllerModel controllerModel, string controllerRoute, MethodInfo method, string? endpointName, AutoConventionSetting setting);
}