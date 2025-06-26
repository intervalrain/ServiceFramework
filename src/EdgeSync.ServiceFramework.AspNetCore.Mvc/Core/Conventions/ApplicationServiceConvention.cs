using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Extensions;
using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.Abstractions.Attributes;
using ErrorOr;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Conventions;

/// <summary>
/// Convention for automatically creating controllers from NatsService classes
/// </summary>
public class ApplicationServiceConvention : IApplicationModelConvention
{
    private readonly AutoConventionOptions _options;
    private readonly IAutoConventionRouteBuilder _routeBuilder;

    public ApplicationServiceConvention(
        IOptions<AutoConventionOptions> options,
        IAutoConventionRouteBuilder routeBuilder)
    {
        _options = options.Value;
        _routeBuilder = routeBuilder;
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
        var attributes = new List<Attribute>();

        // Determine communication mode based on return type and JetStream attribute
        var communicationMode = DetermineCommunicationMode(method.ReturnType, method);

        // Build route based on endpoint name
        var actionRoute = _routeBuilder.BuildActionRoute(controllerModel, controllerRoute, method, subjectAttribute?.EndpointName, setting);

        // subjectAttribute ??= new SubjectAttribute(controllerModel.ControllerName, subjectAttribute.CustomSubject);
        
        // Select HTTP method based on communication mode
        var httpMethodAttribute = SelectHttpMethodForNats(communicationMode, method, actionRoute);

        var routeAttribute = new RouteAttribute(actionRoute);
        attributes.Add(routeAttribute);
        attributes.Add(httpMethodAttribute);

        // Add NATS-specific metadata
        attributes.Add(subjectAttribute!);

        // Add API description
        var description = GetNatsMethodDescription(method, subjectAttribute!);
        if (!string.IsNullOrEmpty(description))
        {
            // Store description in properties since we don't have ApiDescriptionAttribute
            // This can be used by Swagger filters
        }

        var actionModel = new ActionModel(method, attributes)
        {
            Controller = controllerModel,
            ActionName = GetActionName(method, subjectAttribute?.EndpointName)
        };

        // Add metadata for Swagger documentation
        actionModel.Properties["ServiceType"] = controllerModel.ControllerType.AsType();
        actionModel.Properties["MethodName"] = method.Name;
        actionModel.Properties["OriginalMethod"] = method;

        var selectorModel = new SelectorModel();

        var foundRouteAttribute = attributes.OfType<RouteAttribute>().FirstOrDefault();
        if (foundRouteAttribute != null)
        {
            selectorModel.AttributeRouteModel = new AttributeRouteModel(foundRouteAttribute);
        }

        selectorModel.ActionConstraints.Add(new HttpMethodActionConstraint(httpMethodAttribute.HttpMethods));
        actionModel.Selectors.Add(selectorModel);
        actionModel.ApiExplorer.IsVisible = true;

        // Handle parameters from original method
        foreach (var parameter in method.GetParameters())
        {
            var parameterModel = CreateNatsParameterModel(parameter, communicationMode, actionRoute);
            actionModel.Parameters.Add(parameterModel);
        }

        // Remove execution filter - using direct method call

        // Add ErrorOr handling if enabled
        if (_options.UseExceptionHandler && IsErrorOrReturnType(method.ReturnType))
        {
            AddErrorOrHandling(actionModel);
        }

        return actionModel;
    }

    private ParameterModel CreateNatsParameterModel(ParameterInfo parameter, CommunicationMode communicationMode, string routeTemplate)
    {
        var parameterName = parameter.Name!;
        BindingSource bindingSource = routeTemplate.Contains("{" + parameterName + "}", StringComparison.OrdinalIgnoreCase)
            ? BindingSource.Path
            : DetermineNatsBindingSource(parameter, communicationMode, "POST");

        var parameterAttributes = new List<Attribute>();
        var bindingSourceAttribute = ToAttribute(bindingSource);
        if (bindingSourceAttribute != null)
        {
            parameterAttributes.Add(bindingSourceAttribute);
        }
        parameterAttributes.AddRange(parameter.GetCustomAttributes());

        return new ParameterModel(parameter, parameterAttributes)
        {
            ParameterName = parameterName,
            BindingInfo = new BindingInfo
            {
                BindingSource = bindingSource
            }
        };
    }

    #endregion

    #region NATS-Specific Logic

    private CommunicationMode DetermineCommunicationMode(Type returnType, MethodInfo method)
    {
        // Check JetStream attribute first
        var jetStreamAttr = method.GetCustomAttribute<JetStreamAttribute>();
        
        // Remove ErrorOr wrapper
        var actualType = returnType;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ErrorOr<>))
        {
            actualType = returnType.GetGenericArguments()[0];
        }

        // Check if it's a Task or ValueTask
        if (actualType.IsGenericType)
        {
            var genericDef = actualType.GetGenericTypeDefinition();
            if (genericDef == typeof(Task<>) || genericDef == typeof(ValueTask<>))
            {
                actualType = actualType.GetGenericArguments()[0];
            }
        }

        bool hasReturnValue = !(actualType == typeof(Task) || actualType == typeof(void) || actualType == typeof(ValueTask));
        
        // If JetStream is explicitly disabled, force PubSub mode
        if (jetStreamAttr?.Enable == false)
        {
            return CommunicationMode.PubSub;
        }
        
        // If JetStream is enabled (default or explicit), use return type to determine mode
        // Task, void, ValueTask (no return value) = PubSub
        // Other with return value = RequestResponse
        return hasReturnValue ? CommunicationMode.RequestResponse : CommunicationMode.PubSub;
    }

    private HttpMethodAttribute SelectHttpMethodForNats(CommunicationMode communicationMode, MethodInfo method, string route)
    {
        // 如果是 Pub/Sub 模式，統一使用 POST
        if (communicationMode == CommunicationMode.PubSub)
        {
            return new HttpPostAttribute(route);
        }

        // Request/Response 模式根據方法名稱判斷 HTTP 方法
        var methodName = method.Name.ToLowerInvariant();
        
        if (methodName.StartsWith("get") || methodName.StartsWith("find") || methodName.StartsWith("search"))
            return new HttpGetAttribute(route);
        
        if (methodName.StartsWith("create") || methodName.StartsWith("add"))
            return new HttpPostAttribute(route);
        
        if (methodName.StartsWith("update") || methodName.StartsWith("modify"))
            return new HttpPutAttribute(route);
        
        if (methodName.StartsWith("delete") || methodName.StartsWith("remove"))
            return new HttpDeleteAttribute(route);
        
        // 預設使用 POST
        return new HttpPostAttribute(route);
    }

    private BindingSource DetermineNatsBindingSource(ParameterInfo parameter, CommunicationMode communicationMode, string httpMethod)
    {
        // 檢查顯式綁定屬性
        if (parameter.GetCustomAttribute<FromBodyAttribute>() != null)
            return BindingSource.Body;
        if (parameter.GetCustomAttribute<FromQueryAttribute>() != null)
            return BindingSource.Query;
        if (parameter.GetCustomAttribute<FromRouteAttribute>() != null)
            return BindingSource.Path;

        // 根據 HTTP 方法決定綁定來源
        return httpMethod switch
        {
            "GET" or "DELETE" => IsSimpleType(parameter.ParameterType) ? BindingSource.Query : BindingSource.Query,
            "POST" or "PUT" or "PATCH" => IsSimpleType(parameter.ParameterType) ? BindingSource.Query : BindingSource.Body,
            _ => BindingSource.Body
        };
    }

    private string? GetNatsMethodDescription(MethodInfo method, SubjectAttribute subjectAttribute)
    {
        // Generate description from method name and NATS info
        var baseDescription = GenerateDescriptionFromMethodName(method.Name);
        return $"{baseDescription} via NATS subject '{subjectAttribute.CustomSubject}'";
    }

    private string GenerateDescriptionFromMethodName(string methodName)
    {
        var normalizedName = methodName.ToLowerInvariant();

        if (normalizedName.Contains("create"))
            return $"Create operation";
        if (normalizedName.Contains("get") || normalizedName.Contains("find") || normalizedName.Contains("search"))
            return $"Query operation";
        if (normalizedName.Contains("update"))
            return $"Update operation";
        if (normalizedName.Contains("delete"))
            return $"Delete operation";
        if (normalizedName.Contains("publish") || normalizedName.Contains("send"))
            return $"Event publication";
        if (normalizedName.Contains("handle") || normalizedName.Contains("process"))
            return $"Event handling";

        return $"NATS operation";
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