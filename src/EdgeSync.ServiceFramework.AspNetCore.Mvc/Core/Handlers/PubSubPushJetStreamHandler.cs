using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.Abstractions.Attributes;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Abstractions;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Handlers;

/// <summary>
/// Handler for Pub/Sub Push Mode with JetStream
/// </summary>
public class PubSubPushJetStreamHandler : BaseConventionHandler
{
    public PubSubPushJetStreamHandler(IAutoConventionRouteBuilder routeBuilder) : base(routeBuilder)
    {
    }

    public override ConventionMode SupportedMode => ConventionMode.PubSubPushJetStream;

    protected override HttpMethodAttribute GetHttpMethodAttribute(MethodInfo method, string route)
    {
        // Pub/Sub 模式統一使用 POST
        return new HttpPostAttribute(route);
    }

    protected override BindingSource DetermineBindingSource(ParameterInfo parameter, HttpMethodAttribute httpMethodAttribute)
    {
        // 檢查顯式綁定屬性
        if (parameter.GetCustomAttribute<FromBodyAttribute>() != null)
            return BindingSource.Body;
        if (parameter.GetCustomAttribute<FromQueryAttribute>() != null)
            return BindingSource.Query;
        if (parameter.GetCustomAttribute<FromRouteAttribute>() != null)
            return BindingSource.Path;

        // Pub/Sub 模式主要使用 Body 綁定
        return IsSimpleType(parameter.ParameterType) ? BindingSource.Query : BindingSource.Body;
    }

    protected override void ConfigureModeSpecific(
        ActionModel actionModel,
        MethodInfo method,
        SubjectAttribute subjectAttribute,
        AutoConventionSetting setting,
        string controllerRoute)
    {
        // JetStream Push 特定配置
        var jetStreamAttribute = method.GetCustomAttribute<JetStreamAttribute>();
        
        // 添加 JetStream 標示
        actionModel.Properties["JetStreamEnabled"] = true;
        actionModel.Properties["PubSubMode"] = "Push";
        
        // 如果有 JetStreamAttribute，可以添加其他設定
        if (jetStreamAttribute != null)
        {
            actionModel.Properties["JetStreamExplicitlyEnabled"] = jetStreamAttribute.Enable;
        }
        
        // Push 模式可能需要的其他設定
        actionModel.Properties["RequiresStream"] = true;
    }
}