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
/// Handler for Pub/Sub Push Mode classic (without JetStream)
/// </summary>
public class PubSubPushClassicHandler : BaseConventionHandler
{
    public PubSubPushClassicHandler(IAutoConventionRouteBuilder routeBuilder) : base(routeBuilder)
    {
    }

    public override ConventionMode SupportedMode => ConventionMode.PubSubPushClassic;

    protected override HttpMethodAttribute GetHttpMethodAttribute(MethodInfo method, string route)
    {
        // Classic Pub/Sub 模式統一使用 POST
        return new HttpPostAttribute(route);
    }

    protected override BindingSource DetermineBindingSource(ParameterInfo parameter)
    {
        // 檢查顯式綁定屬性
        if (parameter.GetCustomAttribute<FromBodyAttribute>() != null)
            return BindingSource.Body;
        if (parameter.GetCustomAttribute<FromQueryAttribute>() != null)
            return BindingSource.Query;
        if (parameter.GetCustomAttribute<FromRouteAttribute>() != null)
            return BindingSource.Path;

        // Classic 模式主要使用 Body 綁定
        return IsSimpleType(parameter.ParameterType) ? BindingSource.Query : BindingSource.Body;
    }

    protected override void ConfigureModeSpecific(
        ActionModel actionModel,
        MethodInfo method,
        SubjectAttribute subjectAttribute,
        AutoConventionSetting setting,
        string controllerRoute)
    {
        // Classic Push 特定配置
        // 不使用 JetStream，使用傳統的 NATS pub/sub
        
        actionModel.Properties["JetStreamEnabled"] = false;
        actionModel.Properties["PubSubMode"] = "Push";
        actionModel.Properties["ClassicMode"] = true;
    }
}