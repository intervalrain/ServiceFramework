using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Data;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace EdgeSync.ServiceFramework.Core.Services;

/// <summary>
/// Implementation of request data extractor for action execution contexts
/// </summary>
public class RequestDataExtractor : IRequestDataExtractor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RequestDataExtractor> _logger;

    public RequestDataExtractor(
        IServiceProvider serviceProvider,
        ILogger<RequestDataExtractor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public object? ExtractRequestData(ActionExecutingContext context)
    {
        try
        {
            // Get the original method from action metadata
            var originalMethod = context.ActionDescriptor.Properties["OriginalMethod"] as MethodInfo;
            if (originalMethod == null)
            {
                return null;
            }

            var originalParameters = originalMethod.GetParameters();

            // Check if this is originally a parameterless method
            if (originalParameters.Length == 0)
            {
                // For originally parameterless methods, check if EnableAuditWrapper added a RequestDto<object> parameter
                var autoConventionOptions = _serviceProvider.GetService<IOptions<AutoConventionOptions>>()?.Value;
                if (autoConventionOptions?.EnableAuditWrapper == true)
                {
                    // When EnableAuditWrapper is true, parameterless methods get a RequestDto<object> parameter
                    // Look for this auto-generated parameter in ActionArguments
                    foreach (var arg in context.ActionArguments)
                    {
                        if (arg.Value?.GetType().IsGenericType == true &&
                            arg.Value.GetType().GetGenericTypeDefinition() == typeof(RequestDto<>) &&
                            arg.Value.GetType().GetGenericArguments()[0] == typeof(object))
                        {
                            // Found the auto-generated RequestDto<object>, return it
                            return arg.Value;
                        }
                    }
                }
                return null;
            }

            // For methods with original parameters, extract them normally
            if (originalParameters.Length == 1)
            {
                var parameterName = originalParameters[0].Name!;
                if (context.ActionArguments.TryGetValue(parameterName, out var value))
                {
                    return value;
                }
            }

            // For multiple parameters, create an object with parameter values
            var requestData = new Dictionary<string, object?>();
            foreach (var parameter in originalParameters)
            {
                var parameterName = parameter.Name!;
                if (context.ActionArguments.TryGetValue(parameterName, out var value))
                {
                    requestData[parameterName] = value;
                }
                else
                {
                    requestData[parameterName] = null;
                }
            }

            return requestData.Count > 0 ? requestData : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting request data from action context");
            return null;
        }
    }
}