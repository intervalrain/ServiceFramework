using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;

/// <summary>
/// Implementation of audit handler for wrapping requests and extracting audit information
/// </summary>
public class AuditHandler : IAuditHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditHandler> _logger;

    public AuditHandler(
        IServiceProvider serviceProvider,
        ILogger<AuditHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public object? WrapRequestWithAudit(object? requestData, HttpContext httpContext)
    {
        // Check if audit wrapper is enabled
        var autoConventionOptions = _serviceProvider.GetService<IOptions<AutoConventionOptions>>()?.Value;

        if (autoConventionOptions?.EnableAuditWrapper == true)
        {
            // Extract user context information from HTTP context
            var userId = httpContext.User?.Identity?.Name;
            var tenantId = httpContext.Request.Headers["TenantId"].FirstOrDefault();
            var correlationId = httpContext.Request.Headers["CorrelationId"].FirstOrDefault() ?? Guid.NewGuid().ToString();

            if (requestData != null)
            {
                // Wrap in RequestDto if not already wrapped
                var dataType = requestData.GetType();
                if (!(dataType.IsGenericType && dataType.GetGenericTypeDefinition() == typeof(RequestDto<>)))
                {
                    var requestDtoType = typeof(RequestDto<>).MakeGenericType(dataType);
                    var createMethod = requestDtoType.GetMethod("Create", [dataType, typeof(string), typeof(string), typeof(string)]);

                    if (createMethod != null)
                    {
                        return createMethod.Invoke(null, [requestData, userId, tenantId, correlationId]);
                    }
                }
                return requestData;
            }
            else
            {
                // For null requestData (parameterless methods), create RequestDto<object>
                var requestDtoType = typeof(RequestDto<>).MakeGenericType(typeof(object));
                var createMethod = requestDtoType.GetMethod("Create", [typeof(object), typeof(string), typeof(string), typeof(string)]);

                if (createMethod != null)
                {
                    return createMethod.Invoke(null, [new object(), userId, tenantId, correlationId]);
                }
            }
        }

        return requestData;
    }

    public (string ReqSeqId, string Timestamp)? ExtractAuditInfo(object? wrappedRequest)
    {
        if (wrappedRequest == null) return null;

        try
        {
            var requestType = wrappedRequest.GetType();

            // Check if it's a RequestDto<T>
            if (requestType.IsGenericType && requestType.GetGenericTypeDefinition() == typeof(RequestDto<>))
            {
                var reqSeqIdProp = requestType.GetProperty("ReqSeqId");
                var timestampProp = requestType.GetProperty("Timestamp");

                var reqSeqId = reqSeqIdProp?.GetValue(wrappedRequest)?.ToString() ?? "N/A";
                var timestamp = timestampProp?.GetValue(wrappedRequest)?.ToString() ?? "N/A";

                return (reqSeqId, timestamp);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract audit info from wrapped request");
        }

        return null;
    }

    public (string ReqSeqId, string RspSeqId, string Timestamp)? ExtractResponseAuditInfo(object? response)
    {
        if (response == null) return null;

        try
        {
            var responseType = response.GetType();

            // Check if it's a ResponseDto<T>
            if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(ResponseDto<>))
            {
                var reqSeqIdProp = responseType.GetProperty("ReqSeqId");
                var rspSeqIdProp = responseType.GetProperty("RspSeqId");
                var timestampProp = responseType.GetProperty("Timestamp");

                var reqSeqId = reqSeqIdProp?.GetValue(response)?.ToString() ?? "N/A";
                var rspSeqId = rspSeqIdProp?.GetValue(response)?.ToString() ?? "N/A";
                var timestamp = timestampProp?.GetValue(response)?.ToString() ?? "N/A";

                return (reqSeqId, rspSeqId, timestamp);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract response audit info");
        }

        return null;
    }
}