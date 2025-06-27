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
/// Handler for Pub/Sub Pull Mode with JetStream
/// </summary>
public class PubSubPullJetStreamHandler : BaseConventionHandler
{
    public PubSubPullJetStreamHandler(IAutoConventionRouteBuilder routeBuilder) : base(routeBuilder)
    {
    }

    public override ConventionMode SupportedMode => ConventionMode.PubSubPullJetStream;

    protected override HttpMethodAttribute GetHttpMethodAttribute(MethodInfo method, string route)
    {
        // Pull 模式統一使用 POST
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

        // Pull 模式主要使用 Body 綁定
        return IsSimpleType(parameter.ParameterType) ? BindingSource.Query : BindingSource.Body;
    }

    protected override void ConfigureModeSpecific(
        ActionModel actionModel,
        MethodInfo method,
        SubjectAttribute subjectAttribute,
        AutoConventionSetting setting,
        string controllerRoute)
    {
        // JetStream Pull 特定配置
        var pullAttribute = method.GetCustomAttribute<JetStreamPullAttribute>();
        
        // 添加 JetStream 標示
        actionModel.Properties["JetStreamEnabled"] = true;
        actionModel.Properties["PubSubMode"] = "Pull";
        actionModel.Properties["RequiresConsumerGroup"] = true;
        
        // 如果有 JetStreamPullAttribute，添加其設定
        if (pullAttribute != null)
        {
            actionModel.Properties["PullConsumerName"] = pullAttribute.ConsumerName;
            actionModel.Properties["PullConsumerGroup"] = pullAttribute.ConsumerGroup;
            actionModel.Properties["PullMaxMessages"] = pullAttribute.MaxMessages;
            actionModel.Properties["PullAckPolicy"] = pullAttribute.AckPolicy;
            actionModel.Properties["PullCreateConsumerIfNotExists"] = pullAttribute.CreateConsumerIfNotExists;
        }
        else
        {
            // 預設 Pull 模式設定
            actionModel.Properties["PullMaxMessages"] = 1;
            actionModel.Properties["PullAckPolicy"] = "Explicit";
            actionModel.Properties["PullCreateConsumerIfNotExists"] = true;
        }
    }
}