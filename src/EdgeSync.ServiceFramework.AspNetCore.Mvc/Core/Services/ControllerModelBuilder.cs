using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Options;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Extensions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.Attributes;
using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;

/// <summary>
/// Builds controller models for NATS services following SRP
/// Extracted from ApplicationServiceConvention to improve maintainability
/// </summary>
public class ControllerModelBuilder : IControllerModelBuilder
{
    private readonly AutoConventionOptions _options;
    private readonly IAutoConventionRouteBuilder _routeBuilder;
    private readonly IActionModelBuilder _actionModelBuilder;
    private readonly ILogger<ControllerModelBuilder> _logger;

    public ControllerModelBuilder(
        IOptions<AutoConventionOptions> options,
        IAutoConventionRouteBuilder routeBuilder,
        IActionModelBuilder actionModelBuilder,
        ILogger<ControllerModelBuilder> logger)
    {
        _options = options.Value;
        _routeBuilder = routeBuilder;
        _actionModelBuilder = actionModelBuilder;
        _logger = logger;
    }

    /// <summary>
    /// Creates NATS service models from application configuration
    /// </summary>
    public IEnumerable<ControllerModel> CreateNatsServiceModels(ApplicationModel application)
    {
        _logger.LogDebug("Creating NATS service models from application configuration");

        foreach (var setting in _options.Settings!)
        {
            var natsServiceTypes = setting.Assembly.GetTypes()
                .Where(t => typeof(NatsService).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                .WhereIf(setting.TypePredicate != null, setting.TypePredicate!);

            foreach (var serviceType in natsServiceTypes)
            {
                _logger.LogDebug("Creating controller model for service type: {ServiceType}", serviceType.Name);
                
                var controllerName = _routeBuilder.GetServiceName(serviceType.Name);
                yield return CreateNatsControllerModel(serviceType, application, controllerName, setting);
            }
        }
    }

    /// <summary>
    /// Creates a controller model for a specific NATS service
    /// </summary>
    public ControllerModel CreateNatsControllerModel(Type serviceType, ApplicationModel application,
        string controllerName, AutoConventionSetting setting)
    {
        _logger.LogDebug("Creating NATS controller model for {ServiceType} with name {ControllerName}", 
            serviceType.Name, controllerName);

        var controllerRoute = BuildNatsControllerRoute(serviceType, controllerName, setting);

        var attributes = new List<Attribute>
        {
            new ApiControllerAttribute(),
            new RouteAttribute(controllerRoute)
        };

        var controllerModel = new ControllerModel(
            controllerType: serviceType.GetTypeInfo(),
            attributes: attributes)
        {
            ControllerName = controllerName,
            Application = application
        };

        // Only include methods with SubjectAttribute
        var natsHandlerMethods = GetNatsHandlerMethods(serviceType);

        foreach (var method in natsHandlerMethods)
        {
            var subjectAttribute = method.GetCustomAttribute<SubjectAttribute>();
            if (subjectAttribute != null)
            {
                _logger.LogDebug("Adding action for method {MethodName} in controller {ControllerName}", 
                    method.Name, controllerName);
                
                var actionModel = _actionModelBuilder.CreateNatsActionModel(
                    controllerModel, method, subjectAttribute, setting, controllerRoute);
                
                controllerModel.Actions.Add(actionModel);
            }
        }

        _logger.LogDebug("Created controller model {ControllerName} with {ActionCount} actions", 
            controllerName, controllerModel.Actions.Count);

        return controllerModel;
    }

    /// <summary>
    /// Builds the controller route using the route builder
    /// </summary>
    private string BuildNatsControllerRoute(Type serviceType, string controllerName, AutoConventionSetting setting)
    {
        return _routeBuilder.BuildControllerRoute(serviceType, controllerName, setting);
    }

    /// <summary>
    /// Gets methods eligible for NATS handling (those with SubjectAttribute)
    /// </summary>
    private static IEnumerable<MethodInfo> GetNatsHandlerMethods(Type serviceType)
    {
        return serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.DeclaringType == serviceType &&
                       !m.IsSpecialName &&
                       m.GetCustomAttribute<SubjectAttribute>() != null);
    }
}