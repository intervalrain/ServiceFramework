using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Extensions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Decisions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Handlers;
using EdgeSync.ServiceFramework.Attributes;
using ErrorOr;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Conventions;

/// <summary>
/// Convention for automatically creating controllers from NatsService classes
/// </summary>
public class ApplicationServiceConvention : IApplicationModelConvention
{
    private readonly AutoConventionOptions _options;
    private readonly IAutoConventionRouteBuilder _routeBuilder;
    private readonly IConventionDecisionMaker _decisionMaker;
    private readonly ConventionHandlerFactory _handlerFactory;

    public ApplicationServiceConvention(
        IOptions<AutoConventionOptions> options,
        IAutoConventionRouteBuilder routeBuilder,
        IConventionDecisionMaker decisionMaker)
    {
        _options = options.Value;
        _routeBuilder = routeBuilder;
        _decisionMaker = decisionMaker;
        _handlerFactory = new ConventionHandlerFactory(routeBuilder);
    }

    public void Apply(ApplicationModel application)
    {
        var controllers = CreateNatsServiceModels(application);

        foreach (var controller in controllers)
        {
            application.Controllers.Add(controller);
        }
    }

    #region Controller Creation for NatsService
    private IEnumerable<ControllerModel> CreateNatsServiceModels(ApplicationModel application)
    {
        foreach (var setting in _options.Settings!)
        {
            var natsServiceTypes = setting.Assembly.GetTypes()
                .Where(t => typeof(NatsService).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                .WhereIf(setting.TypePredicate != null, setting.TypePredicate!);

            foreach (var serviceType in natsServiceTypes)
            {
                yield return CreateNatsControllerModel(serviceType, application, _routeBuilder.GetServiceName(serviceType.Name), setting);
            }
        }
    }

    private ControllerModel CreateNatsControllerModel(Type serviceType, ApplicationModel application, string controllerName, AutoConventionSetting setting)
    {
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
        var natsHandlerMethods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.DeclaringType == serviceType &&
                       !m.IsSpecialName &&
                       m.GetCustomAttribute<SubjectAttribute>() != null);

        foreach (var method in natsHandlerMethods)
        {
            var subjectAttribute = method.GetCustomAttribute<SubjectAttribute>();
            if (subjectAttribute != null)
            {
                controllerModel.Actions.Add(CreateNatsActionModel(controllerModel, method, subjectAttribute, setting, controllerRoute));
            }
        }

        return controllerModel;
    }

    private string BuildNatsControllerRoute(Type serviceType, string controllerName, AutoConventionSetting setting)
        => _routeBuilder.BuildControllerRoute(serviceType, controllerName, setting);
    

    private ActionModel CreateNatsActionModel(
        ControllerModel controllerModel, 
        MethodInfo method, 
        SubjectAttribute subjectAttribute, 
        AutoConventionSetting setting, 
        string controllerRoute)
    {
        // Use new decision-making architecture
        var context = ConventionDecisionContextBuilder.Build(method, _options);
        var decisionResult = _decisionMaker.MakeDecision(context);

        // Handle decision errors
        if (decisionResult.IsError)
        {
            throw new InvalidOperationException(
                $"Auto-convention decision failed for method {method.Name}: {decisionResult.ErrorMessage}");
        }

        // Get appropriate handler for the decided mode
        var handler = _handlerFactory.GetHandler(decisionResult.Mode);

        // Create action model with basic attributes
        var attributes = new List<Attribute> { subjectAttribute! };
        var actionModel = new ActionModel(method, attributes)
        {
            Controller = controllerModel,
            ActionName = GetActionName(method, subjectAttribute?.EndpointName)
        };

        // Add metadata for Swagger documentation
        actionModel.Properties["ServiceType"] = controllerModel.ControllerType.AsType();
        actionModel.Properties["MethodName"] = method.Name;
        actionModel.Properties["OriginalMethod"] = method;
        actionModel.Properties["ConventionMode"] = decisionResult.Mode;

        // Configure action using the appropriate handler
        handler.ConfigureAction(actionModel, method, subjectAttribute!, setting, controllerRoute);

        actionModel.ApiExplorer.IsVisible = true;

        // Add ErrorOr handling if enabled
        if (_options.UseExceptionHandler && IsErrorOrReturnType(method.ReturnType))
        {
            AddErrorOrHandling(actionModel);
        }

        return actionModel;
    }


    #endregion

    #region Action and Route Helpers

    private string GetActionName(MethodInfo method, string? endpointName)
    {
        if (!string.IsNullOrEmpty(endpointName))
        {
            // Remove route parameters to get clean action name
            var cleanEndpoint = endpointName.Split('/')[0];
            return cleanEndpoint;
        }

        return method.Name;
    }

    #endregion

    #region Common Helper Methods

    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || 
               type == typeof(DateTime) || type == typeof(Guid) || type.IsEnum;
    }

    private static Attribute? ToAttribute(BindingSource bindingSource)
    {
        if (bindingSource == BindingSource.Body)
        {
            return new FromBodyAttribute();
        }
        else if (bindingSource == BindingSource.Query)
        {
            return new FromQueryAttribute();
        }
        else if (bindingSource == BindingSource.Path)
        {
            return new FromRouteAttribute();
        }
        return null;
    }

    private bool IsErrorOrReturnType(Type returnType)
    {
        if (returnType.IsGenericType)
        {
            var genericType = returnType.GetGenericTypeDefinition();

            if (genericType == typeof(ErrorOr<>))
            {
                return true;
            }

            if ((genericType == typeof(Task<>) || genericType == typeof(ValueTask<>)) && 
                returnType.GetGenericArguments()[0].IsGenericType)
            {
                var taskArgument = returnType.GetGenericArguments()[0];
                if (taskArgument.IsGenericType)
                {
                    return taskArgument.GetGenericTypeDefinition() == typeof(ErrorOr<>);
                }
            }
        }

        return false;
    }

    private void AddErrorOrHandling(ActionModel actionModel)
    {
        actionModel.Filters.Add(new ErrorOrResultFilter());
    }

    #endregion
}