using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Abstractions;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Handlers;

/// <summary>
/// Base class for convention handlers with common functionality
/// </summary>
public abstract class BaseConventionHandler : IConventionHandler
{
    protected readonly IAutoConventionRouteBuilder _routeBuilder;

    protected BaseConventionHandler(IAutoConventionRouteBuilder routeBuilder)
    {
        _routeBuilder = routeBuilder;
    }

    public abstract ConventionMode SupportedMode { get; }

    public virtual void ConfigureAction(
        ActionModel actionModel,
        MethodInfo method,
        SubjectAttribute subjectAttribute,
        AutoConventionSetting setting,
        string controllerRoute)
    {
        // Common configuration
        var actionRoute = _routeBuilder.BuildActionRoute(
            actionModel.Controller, 
            controllerRoute, 
            method, 
            subjectAttribute.EndpointName, 
            setting);

        // Configure HTTP method
        var httpMethodAttribute = GetHttpMethodAttribute(method, actionRoute);
        actionModel.Selectors.Clear();
        
        var selectorModel = new SelectorModel();
        selectorModel.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(actionRoute));
        selectorModel.ActionConstraints.Add(new Microsoft.AspNetCore.Mvc.ActionConstraints.HttpMethodActionConstraint(httpMethodAttribute.HttpMethods));
        actionModel.Selectors.Add(selectorModel);

        // Configure parameters
        ConfigureParameters(actionModel, method, actionRoute);

        // Mode-specific configuration
        ConfigureModeSpecific(actionModel, method, subjectAttribute, setting, controllerRoute);
    }

    protected abstract HttpMethodAttribute GetHttpMethodAttribute(MethodInfo method, string route);
    
    protected abstract void ConfigureModeSpecific(
        ActionModel actionModel,
        MethodInfo method,
        SubjectAttribute subjectAttribute,
        AutoConventionSetting setting,
        string controllerRoute);

    protected virtual void ConfigureParameters(ActionModel actionModel, MethodInfo method, string routeTemplate)
    {
        foreach (var parameter in method.GetParameters())
        {
            var parameterModel = CreateParameterModel(parameter, routeTemplate);
            actionModel.Parameters.Add(parameterModel);
        }
    }

    protected virtual ParameterModel CreateParameterModel(ParameterInfo parameter, string routeTemplate)
    {
        var parameterName = parameter.Name!;
        var bindingSource = routeTemplate.Contains("{" + parameterName + "}", StringComparison.OrdinalIgnoreCase)
            ? BindingSource.Path
            : DetermineBindingSource(parameter);

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

    protected abstract BindingSource DetermineBindingSource(ParameterInfo parameter);

    protected static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || 
               type == typeof(DateTime) || type == typeof(Guid) || type.IsEnum;
    }

    protected static Attribute? ToAttribute(BindingSource bindingSource)
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
}