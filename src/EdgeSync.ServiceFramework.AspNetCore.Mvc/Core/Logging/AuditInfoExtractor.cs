using EdgeSync.ServiceFramework.Data;

using NATS.Client.Services;

namespace EdgeSync.ServiceFramework.Core.Logging;

/// <summary>
/// Utility for extracting audit information from request and response DTOs
/// </summary>
public static class AuditInfoExtractor
{
    /// <summary>
    /// Extracts audit information from a request object
    /// </summary>
    public static bool ExtractFromRequest<T>(NatsSvcMsg<T> msg, out AuditInfo? info)
    {
        var request = msg.Data;
        if (request != null)
        {
            try
            {
                var requestType = request.GetType();

                // Check if it's a RequestDto<T>
                if (requestType.IsGenericType && requestType.GetGenericTypeDefinition() == typeof(RequestDto<>))
                {
                    var reqSeqIdProp = requestType.GetProperty("ReqSeqId");
                    var timestampProp = requestType.GetProperty("Timestamp");
                    var issuerProp = requestType.GetProperty("Issuer");
                    var metadataProp = requestType.GetProperty("Metadata");

                    var reqSeqId = reqSeqIdProp?.GetValue(request)?.ToString();
                    var timestamp = timestampProp?.GetValue(request)?.ToString();
                    var issuer = issuerProp?.GetValue(request)?.ToString();

                    // Extract audit info from metadata
                    string? correlationId = null;
                    string? userId = null;
                    string? tenantId = null;

                    var metadata = new Dictionary<string, string>();
                    if (metadataProp?.GetValue(request) is Dictionary<string, string> metadataDict)
                    {
                        metadata = metadataDict;
                        metadataDict.TryGetValue("CorrelationId", out correlationId);
                        metadataDict.TryGetValue("UserId", out userId);
                        metadataDict.TryGetValue("TenantId", out tenantId);
                    }

                    if (msg.Headers != null)
                    {
                        foreach (var header in msg.Headers)
                        {
                            metadata[header.Key] = header.Value!;
                        }
                    }

                    info = new AuditInfo(reqSeqId, timestamp, metadata);
                    return true;
                }
            }
            catch
            {
                // Ignore errors and return null
            }
        }

        info = null;

        return false;
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
                var timestampProp = responseType.GetProperty("Timestamp");
                var issuerProp = responseType.GetProperty("Issuer");
                var metadataProp = responseType.GetProperty("Metadata");

                var reqSeqId = reqSeqIdProp?.GetValue(response)?.ToString();
                var rspSeqId = rspSeqIdProp?.GetValue(response)?.ToString();
                var timestamp = timestampProp?.GetValue(response)?.ToString();
                var issuer = issuerProp?.GetValue(response)?.ToString();

                // Extract audit info from metadata
                string? correlationId = null;
                string? userId = null;
                string? tenantId = null;

                Dictionary<string, string>? metadata = null;
                if (metadataProp?.GetValue(response) is Dictionary<string, string> metadataDict)
                {
                    metadata = metadataDict;
                    metadataDict.TryGetValue("CorrelationId", out correlationId);
                    metadataDict.TryGetValue("UserId", out userId);
                    metadataDict.TryGetValue("TenantId", out tenantId);
                }

                // For response, we use RspSeqId as the primary sequence ID
                return new AuditInfo(rspSeqId ?? reqSeqId, timestamp, metadata);
            }
        }
        catch
        {
            // Ignore errors and return null
        }

        return null;
    }
}