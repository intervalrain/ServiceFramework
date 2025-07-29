using Microsoft.Extensions.Logging;
using System.Text;

namespace EdgeSync.ServiceFramework.Core.Logging;

/// <summary>
/// Formatted and colorized logger for ServiceFramework operations
/// </summary>
public static class ServiceFrameworkLogger
{
    private const string ResetColor = "\u001b[0m";
    private const string BoldText = "\u001b[1m";
    
    // Colors for different operation types
    private const string RequestResponseColor = "\u001b[36m"; // Cyan
    private const string PubSubColor = "\u001b[35m"; // Magenta
    private const string SuccessColor = "\u001b[32m"; // Green
    private const string ErrorColor = "\u001b[31m"; // Red
    private const string InfoColor = "\u001b[34m"; // Blue
    private const string WarningColor = "\u001b[33m"; // Yellow
    
    // Icons for different operations
    private const string RequestResponseIcon = "🔄";
    private const string PubSubIcon = "📤";
    private const string SuccessIcon = "✅";
    private const string ErrorIcon = "❌";
    private const string InfoIcon = "ℹ️";

    public static void LogRequestResponse(
        ILogger logger,
        string operation,
        string serviceName,
        string methodName,
        string subject,
        string? reqSeqId,
        string? correlationId,
        string? userId,
        string? tenantId,
        long durationMs,
        bool isSuccess = true,
        Exception? exception = null)
    {
        var sb = new StringBuilder();
        
        // Header with operation type and icon
        sb.AppendLine($"{BoldText}{RequestResponseColor}{RequestResponseIcon} REQUEST/RESPONSE - {operation.ToUpper()}{ResetColor}");
        
        // Service information
        sb.AppendLine($"{InfoColor}├─ Service: {BoldText}{serviceName}.{methodName}{ResetColor}");
        sb.AppendLine($"{InfoColor}├─ Subject: {subject}{ResetColor}");
        sb.AppendLine($"{InfoColor}├─ Duration: {durationMs}ms{ResetColor}");
        
        // Audit information
        if (!string.IsNullOrEmpty(reqSeqId))
            sb.AppendLine($"{InfoColor}├─ ReqSeqId: {reqSeqId}{ResetColor}");
        if (!string.IsNullOrEmpty(correlationId))
            sb.AppendLine($"{InfoColor}├─ CorrelationId: {correlationId}{ResetColor}");
        if (!string.IsNullOrEmpty(userId))
            sb.AppendLine($"{InfoColor}├─ UserId: {userId}{ResetColor}");
        if (!string.IsNullOrEmpty(tenantId))
            sb.AppendLine($"{InfoColor}├─ TenantId: {tenantId}{ResetColor}");
        
        // Status
        if (isSuccess)
        {
            sb.AppendLine($"{SuccessColor}└─ Status: {SuccessIcon} SUCCESS{ResetColor}");
            logger.LogInformation(sb.ToString());
        }
        else
        {
            sb.AppendLine($"{ErrorColor}└─ Status: {ErrorIcon} FAILED{ResetColor}");
            if (exception != null)
            {
                sb.AppendLine($"{ErrorColor}   Error: {exception.Message}{ResetColor}");
            }
            logger.LogError(exception, sb.ToString());
        }
    }

    public static void LogPubSub(
        ILogger logger,
        string operation,
        string serviceName,
        string methodName,
        string subject,
        string mode,
        string? reqSeqId,
        string? correlationId,
        string? userId,
        string? tenantId,
        long durationMs,
        bool isSuccess = true,
        Exception? exception = null)
    {
        var sb = new StringBuilder();
        
        // Header with operation type and icon
        sb.AppendLine($"{BoldText}{PubSubColor}{PubSubIcon} PUB/SUB - {operation.ToUpper()}{ResetColor}");
        
        // Service information
        sb.AppendLine($"{InfoColor}├─ Service: {BoldText}{serviceName}.{methodName}{ResetColor}");
        sb.AppendLine($"{InfoColor}├─ Subject: {subject}{ResetColor}");
        sb.AppendLine($"{InfoColor}├─ Mode: {mode}{ResetColor}");
        sb.AppendLine($"{InfoColor}├─ Duration: {durationMs}ms{ResetColor}");
        
        // Audit information
        if (!string.IsNullOrEmpty(reqSeqId))
            sb.AppendLine($"{InfoColor}├─ ReqSeqId: {reqSeqId}{ResetColor}");
        if (!string.IsNullOrEmpty(correlationId))
            sb.AppendLine($"{InfoColor}├─ CorrelationId: {correlationId}{ResetColor}");
        if (!string.IsNullOrEmpty(userId))
            sb.AppendLine($"{InfoColor}├─ UserId: {userId}{ResetColor}");
        if (!string.IsNullOrEmpty(tenantId))
            sb.AppendLine($"{InfoColor}├─ TenantId: {tenantId}{ResetColor}");
        
        // Status
        if (isSuccess)
        {
            sb.AppendLine($"{SuccessColor}└─ Status: {SuccessIcon} SUCCESS{ResetColor}");
            logger.LogInformation(sb.ToString());
        }
        else
        {
            sb.AppendLine($"{ErrorColor}└─ Status: {ErrorIcon} FAILED{ResetColor}");
            if (exception != null)
            {
                sb.AppendLine($"{ErrorColor}   Error: {exception.Message}{ResetColor}");
            }
            logger.LogError(exception, sb.ToString());
        }
    }

    public static void LogServiceFrameworkOperation(
        ILogger logger,
        string operationType,
        string details,
        LogLevel logLevel = LogLevel.Information)
    {
        var color = logLevel switch
        {
            LogLevel.Error => ErrorColor,
            LogLevel.Warning => WarningColor,
            LogLevel.Information => InfoColor,
            _ => InfoColor
        };
        
        var icon = logLevel switch
        {
            LogLevel.Error => ErrorIcon,
            LogLevel.Warning => "⚠️",
            LogLevel.Information => InfoIcon,
            _ => InfoIcon
        };

        var message = $"{BoldText}{color}{icon} SERVICEFRAMEWORK - {operationType.ToUpper()}{ResetColor}\n{color}└─ {details}{ResetColor}";
        
        logger.Log(logLevel, message);
    }
}

/// <summary>
/// Extension methods for easier logging
/// </summary>
public static class ServiceFrameworkLoggerExtensions
{
    public static void LogServiceFrameworkRequestResponse(
        this ILogger logger,
        string serviceName,
        string methodName,
        string subject,
        AuditInfo? auditInfo = null,
        long durationMs = 0,
        bool isSuccess = true,
        Exception? exception = null)
    {
        ServiceFrameworkLogger.LogRequestResponse(
            logger,
            isSuccess ? "completed" : "failed",
            serviceName,
            methodName,
            subject,
            auditInfo?.ReqSeqId,
            auditInfo?.CorrelationId,
            auditInfo?.UserId,
            auditInfo?.TenantId,
            durationMs,
            isSuccess,
            exception);
    }

    public static void LogServiceFrameworkPubSub(
        this ILogger logger,
        string serviceName,
        string methodName,
        string subject,
        string mode,
        AuditInfo? auditInfo = null,
        long durationMs = 0,
        bool isSuccess = true,
        Exception? exception = null)
    {
        ServiceFrameworkLogger.LogPubSub(
            logger,
            isSuccess ? "published" : "failed",
            serviceName,
            methodName,
            subject,
            mode,
            auditInfo?.ReqSeqId,
            auditInfo?.CorrelationId,
            auditInfo?.UserId,
            auditInfo?.TenantId,
            durationMs,
            isSuccess,
            exception);
    }
}

/// <summary>
/// Audit information extracted from request/response DTOs
/// </summary>
public record AuditInfo(
    string? ReqSeqId,
    string? CorrelationId,
    string? UserId,
    string? TenantId,
    string? Timestamp = null);