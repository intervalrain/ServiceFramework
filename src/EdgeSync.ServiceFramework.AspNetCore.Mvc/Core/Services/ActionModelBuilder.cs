using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Options;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Decisions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Handlers;
using EdgeSync.ServiceFramework.Core.Filters;
using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Abstractions;
using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.Core.Services;

/// <summary>
/// Builds action models for NATS service methods following SRP
/// Extracted from ApplicationServiceConvention to improve maintainability
/// </summary>
public class ActionModelBuilder : IActionModelBuilder
{
    private readonly AutoConventionOptions _options;
    private readonly IConventionDecisionMaker _decisionMaker;
    private readonly ConventionHandlerFactory _handlerFactory;
    private readonly IChannelResolver _channelResolver;
    private readonly ILogger<ActionModelBuilder> _logger;

    public ActionModelBuilder(
        IOptions<AutoConventionOptions> options,
        IConventionDecisionMaker decisionMaker,
        IAutoConventionRouteBuilder routeBuilder,
        IChannelResolver channelResolver,
        ILogger<ActionModelBuilder> logger)
    {
        _options = options.Value;
        _decisionMaker = decisionMaker;
        _handlerFactory = new ConventionHandlerFactory(routeBuilder);
        _channelResolver = channelResolver;
        _logger = logger;
    }

    /// <summary>
    /// Creates an action model for a NATS service method
    /// </summary>
    public ActionModel CreateNatsActionModel(ControllerModel controllerModel, MethodInfo method,
        SubjectAttribute subjectAttribute, AutoConventionSetting setting, string controllerRoute)
    {
        _logger.LogDebug("Creating NATS action model for method {MethodName} in controller {ControllerName}", 
            method.Name, controllerModel.ControllerName);

        // Use new decision-making architecture
        var context = ConventionDecisionContextBuilder.Build(method, _options);
        var decisionResult = _decisionMaker.MakeDecision(context);

        // Handle decision errors
        if (decisionResult.IsError)
        {
            var errorMessage = $"Auto-convention decision failed for method {method.Name}: {decisionResult.ErrorMessage}";
            _logger.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogDebug("Convention mode decided for method {MethodName}: {ConventionMode}", 
            method.Name, decisionResult.Mode);

        // Get appropriate handler for the decided mode
        var handler = _handlerFactory.GetHandler(decisionResult.Mode);

        // Create action model with basic attributes
        var attributes = new List<Attribute> { subjectAttribute! };
        var actionModel = new ActionModel(method, attributes)
        {
            Controller = controllerModel,
            ActionName = GetActionName(method, subjectAttribute?.EndpointName)
        };

        // Add metadata for Swagger documentation and NATS proxy
        AddActionMetadata(actionModel, controllerModel, method, subjectAttribute, decisionResult.Mode);

        // Add the NATS proxy action filter to intercept execution
        actionModel.Filters.Add(new TypeFilterAttribute(typeof(NatsProxyActionFilter)));

        // Configure action using the appropriate handler
        handler.ConfigureAction(actionModel, method, subjectAttribute!, setting, controllerRoute);

        actionModel.ApiExplorer.IsVisible = true;

        _logger.LogDebug("Created action model {ActionName} for method {MethodName} with mode {ConventionMode}", 
            actionModel.ActionName, method.Name, decisionResult.Mode);

        return actionModel;
    }

    /// <summary>
    /// Adds metadata properties to the action model for NATS proxy and Swagger documentation
    /// </summary>
    private void AddActionMetadata(ActionModel actionModel, ControllerModel controllerModel, 
        MethodInfo method, SubjectAttribute subjectAttribute, ConventionMode conventionMode)
    {
        actionModel.Properties["ServiceType"] = controllerModel.ControllerType.AsType();
        actionModel.Properties["ServiceName"] = method.DeclaringType?.Name ?? "Unknown";
        actionModel.Properties["MethodName"] = method.Name;
        actionModel.Properties["OriginalMethod"] = method;
        actionModel.Properties["Subject"] = subjectAttribute?.CustomSubject;
        actionModel.Properties["ConventionMode"] = conventionMode;
        actionModel.Properties["ChannelName"] = _channelResolver.GetChannelName(
            controllerModel.ControllerType.AsType(), method);

        _logger.LogDebug("Added metadata to action {ActionName}: ServiceType={ServiceType}, ChannelName={ChannelName}", 
            actionModel.ActionName, controllerModel.ControllerType.AsType().Name, 
            actionModel.Properties["ChannelName"]);
    }

    /// <summary>
    /// Gets the action name from method or endpoint name
    /// </summary>
    private static string GetActionName(MethodInfo method, string? endpointName)
    {
        if (!string.IsNullOrEmpty(endpointName))
        {
            // Remove route parameters to get clean action name
            var cleanEndpoint = endpointName.Split('/')[0];
            return cleanEndpoint;
        }

        return method.Name;
    }
}