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
/// Handler for Request/Response mode
/// </summary>
public class RequestResponseHandler : BaseConventionHandler
{
    public RequestResponseHandler(IAutoConventionRouteBuilder routeBuilder) : base(routeBuilder)
    {
    }

    public override ConventionMode SupportedMode => ConventionMode.RequestResponse;

    protected override HttpMethodAttribute GetHttpMethodAttribute(MethodInfo method, string route)
    {
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

    protected override BindingSource DetermineBindingSource(ParameterInfo parameter, HttpMethodAttribute httpMethodAttribute)
    {
        if (httpMethodAttribute is HttpGetAttribute)
        {
            return BindingSource.Query;
        }

        // 檢查顯式綁定屬性
        if (parameter.GetCustomAttribute<FromBodyAttribute>() != null)
            return BindingSource.Body;
        if (parameter.GetCustomAttribute<FromQueryAttribute>() != null)
            return BindingSource.Query;
        if (parameter.GetCustomAttribute<FromRouteAttribute>() != null)
            return BindingSource.Path;

        // Request/Response 模式的預設綁定邏輯
        // 這裡我們無法直接獲取方法名稱，所以使用通用邏輯
        return IsSimpleType(parameter.ParameterType) ? BindingSource.Query : BindingSource.Body;
    }

    protected override void ConfigureModeSpecific(
        ActionModel actionModel,
        MethodInfo method,
        SubjectAttribute subjectAttribute,
        AutoConventionSetting setting,
        string controllerRoute)
    {
        // Request/Response 特定配置
        // 可以添加額外的特定配置，如超時設定等
    }
}