using EdgeSync.ServiceFramework.Core.Logging;
using EdgeSync.ServiceFramework.Data;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Logging;

/// <summary>
/// Utility for extracting audit information from request and response DTOs
/// </summary>
public static class AuditInfoExtractor
{
    /// <summary>
    /// Extracts audit information from a request object
    /// </summary>
    public static AuditInfo? ExtractFromRequest(object? request)
    {
        if (request == null) return null;

        try
        {
            var requestType = request.GetType();

            // Check if it's a RequestDto<T>
            if (requestType.IsGenericType && requestType.GetGenericTypeDefinition() == typeof(RequestDto<>))
            {
                var reqSeqIdProp = requestType.GetProperty("ReqSeqId");
                var correlationIdProp = requestType.GetProperty("CorrelationId");
                var userIdProp = requestType.GetProperty("UserId");
                var tenantIdProp = requestType.GetProperty("TenantId");
                var timestampProp = requestType.GetProperty("Timestamp");

                var reqSeqId = reqSeqIdProp?.GetValue(request)?.ToString();
                var correlationId = correlationIdProp?.GetValue(request)?.ToString();
                var userId = userIdProp?.GetValue(request)?.ToString();
                var tenantId = tenantIdProp?.GetValue(request)?.ToString();
                var timestamp = timestampProp?.GetValue(request)?.ToString();

                return new AuditInfo(reqSeqId, correlationId, userId, tenantId, timestamp);
            }
        }
        catch
        {
            // Ignore errors and return null
        }

        return null;
    }

    /// <summary>
    /// Extracts audit information from a response object
    /// </summary>
    public static AuditInfo? ExtractFromResponse(object? response)
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
                var correlationIdProp = responseType.GetProperty("CorrelationId");
                var userIdProp = responseType.GetProperty("UserId");
                var tenantIdProp = responseType.GetProperty("TenantId");
                var timestampProp = responseType.GetProperty("Timestamp");

                var reqSeqId = reqSeqIdProp?.GetValue(response)?.ToString();
                var rspSeqId = rspSeqIdProp?.GetValue(response)?.ToString();
                var correlationId = correlationIdProp?.GetValue(response)?.ToString();
                var userId = userIdProp?.GetValue(response)?.ToString();
                var tenantId = tenantIdProp?.GetValue(response)?.ToString();
                var timestamp = timestampProp?.GetValue(response)?.ToString();

                // For response, we use RspSeqId as the primary sequence ID
                return new AuditInfo(rspSeqId ?? reqSeqId, correlationId, userId, tenantId, timestamp);
            }
        }
        catch
        {
            // Ignore errors and return null
        }

        return null;
    }

    /// <summary>
    /// Extracts audit information from either request or response object
    /// </summary>
    public static AuditInfo? ExtractFromObject(object? obj)
    {
        if (obj == null) return null;

        // Try request first, then response
        return ExtractFromRequest(obj) ?? ExtractFromResponse(obj);
    }
}