using System.Reflection;


using EdgeSync.ServiceFramework.AspNetCore.Mvc.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Extensions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.RouteBuilders;

public class DefaultAutoConventionRouteBuilder : IAutoConventionRouteBuilder
{
    private readonly string[] _prefixes = ["Nats, Application, App"];
    private readonly string[] _postfixes = ["ApplicationService, AppService", "NatsService", "Service"];

    public string GetServiceName(string serviceName)
    {
        var name = serviceName
            .RemovePrefixes(_prefixes)
            .RemovePostfixes(_postfixes);

        return name;
    }

    public string BuildControllerRoute(Type serviceType, string controllerName, AutoConventionSetting setting)
    {
        var routePrefix = setting.RoutePrefix ?? "api";
        var routeName = controllerName.ToLowerInvariant();
        return $"/{routePrefix}/{routeName}";
    }

    public string BuildActionRoute(ControllerModel controllerModel, string controllerRoute, MethodInfo method, string? endpointName, AutoConventionSetting setting)
    {
        if (!string.IsNullOrEmpty(endpointName))
        {
            if (endpointName.StartsWith("/"))
            {
                return endpointName.TrimStart('/');
            }
            else
            {
                var routePrefix = setting.RoutePrefix ?? "api";
                return $"{routePrefix}/{endpointName}";
            }
        }

        // Generate route from method name
        var methodName = method.Name
            .RemovePrefixes(["Get", "Create", "Update", "Delete"]);

        var route = methodName.ToLowerInvariant();

        // Add parameters to route if needed
        var parameters = method.GetParameters();
        var routeParams = parameters
            .Where(p => p.ParameterType == typeof(Guid) || p.ParameterType == typeof(int) || p.ParameterType == typeof(long))
            .Select(p => $"{{{p.Name}}}")
            .ToList();

        if (routeParams.Any())
        {
            route = string.IsNullOrEmpty(route)
                ? string.Join("/", routeParams)
                : $"{route}/{string.Join("/", routeParams)}";
        }

        return route;
    }
}