using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

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
        Dictionary<string, string>? metadata,
        long durationMs,
        bool isSuccess = true,
        Exception? exception = null,
        object? input = null,
        object? output = null,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var sb = new StringBuilder();

        // Header with operation type and icon
        sb.AppendLine();
        sb.AppendLine($"{BoldText}{RequestResponseColor}{RequestResponseIcon} REQUEST/RESPONSE - {operation.ToUpper()}{ResetColor}");
        
        // Service information
        sb.AppendLine($"{InfoColor}├─ Service: {BoldText}{serviceName}.{methodName}{ResetColor}");
        sb.AppendLine($"{InfoColor}├─ Subject: {subject}{ResetColor}");
        sb.AppendLine($"{InfoColor}├─ Duration: {durationMs}ms{ResetColor}");
        
        // Source location information - for errors, show where the error occurred
        if (!isSuccess && exception != null)
        {
            var stackFrame = GetErrorLocation(exception);
            if (stackFrame != null)
                sb.AppendLine($"{InfoColor}├─ Error Location: {stackFrame}{ResetColor}");
        }
        
        // Audit information
        if (!string.IsNullOrEmpty(reqSeqId))
            sb.AppendLine($"{InfoColor}├─ ReqSeqId: {reqSeqId}{ResetColor}");
        if (metadata != null && metadata.Count > 0)
        {
            sb.AppendLine($"{InfoColor}├─ Metadata:{ResetColor}");
            foreach (var kvp in metadata)
            {
                sb.AppendLine($"{InfoColor}│  ├─ {kvp.Key}: {kvp.Value}{ResetColor}");
            }
        }
        
        // Input/Output information for successful operations
        if (isSuccess)
        {
            if (input != null)
            {
                sb.AppendLine($"{InfoColor}├─ Input:{ResetColor}");
                sb.AppendLine($"{InfoColor}│  {FormatObject(input)}{ResetColor}");
            }
            if (output != null)
            {
                sb.AppendLine($"{InfoColor}├─ Output:{ResetColor}");
                sb.AppendLine($"{InfoColor}│  {FormatObject(output)}{ResetColor}");
            }
        }
        
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

        Dictionary<string, string>? metadata,
        long durationMs,
        bool isSuccess = true,
        Exception? exception = null,
        object? input = null,
        object? output = null,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var sb = new StringBuilder();
        
        // Header with operation type and icon
        sb.AppendLine($"{BoldText}{PubSubColor}{PubSubIcon} PUB/SUB - {operation.ToUpper()}{ResetColor}");
        
        // Service information
        sb.AppendLine($"{InfoColor}├─ Service: {BoldText}{serviceName}.{methodName}{ResetColor}");
        sb.AppendLine($"{InfoColor}├─ Subject: {subject}{ResetColor}");
        sb.AppendLine($"{InfoColor}├─ Mode: {mode}{ResetColor}");
        sb.AppendLine($"{InfoColor}├─ Duration: {durationMs}ms{ResetColor}");
        
        // Source location information - for errors, show where the error occurred
        if (!isSuccess && exception != null)
        {
            var stackFrame = GetErrorLocation(exception);
            if (stackFrame != null)
                sb.AppendLine($"{InfoColor}├─ Error Location: {stackFrame}{ResetColor}");
        }
        
        // Audit information
        if (!string.IsNullOrEmpty(reqSeqId))
            sb.AppendLine($"{InfoColor}├─ ReqSeqId: {reqSeqId}{ResetColor}");
        if (metadata != null && metadata.Count > 0)
        {
            sb.AppendLine($"{InfoColor}├─ Metadata:{ResetColor}");
            foreach (var kvp in metadata)
            {
                sb.AppendLine($"{InfoColor}│  ├─ {kvp.Key}: {kvp.Value}{ResetColor}");
            }
        }
        
        // Input/Output information for successful operations
        if (isSuccess)
        {
            if (input != null)
            {
                sb.AppendLine($"{InfoColor}├─ Input:{ResetColor}");
                sb.AppendLine($"{InfoColor}│  {FormatObject(input)}{ResetColor}");
            }
            if (output != null)
            {
                sb.AppendLine($"{InfoColor}├─ Output:{ResetColor}");
                sb.AppendLine($"{InfoColor}│  {FormatObject(output)}{ResetColor}");
            }
        }
        
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
        LogLevel logLevel = LogLevel.Information,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
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

        // Build message with source location
        var fileName = Path.GetFileName(callerFilePath);
        var sourceInfo = !string.IsNullOrEmpty(fileName) ? $" [{fileName}:{callerLineNumber}]" : "";
        
        var message = $"{BoldText}{color}{icon} SERVICEFRAMEWORK - {operationType.ToUpper()}{sourceInfo}{ResetColor}\n{color}└─ {details}{ResetColor}";
        
        logger.Log(logLevel, message);
    }

    /// <summary>
    /// Format an object for structured logging output
    /// </summary>
    private static string FormatObject(object obj)
    {
        try
        {
            if (obj is string str)
            {
                // Handle multiline strings
                var innerlines = str.Split('\n');
                if (innerlines.Length > 1)
                {
                    var innerformattedLines = innerlines.Select((line, index) => 
                        index == 0 ? line : $"{InfoColor}│  {line}{ResetColor}");
                    return string.Join('\n', innerformattedLines);
                }
                return str;
            }
                
            var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            // Add proper tree structure indentation with │ for multiline content
            var lines = json.Split('\n');
            var formattedLines = lines.Select((line, index) => 
            {
                if (string.IsNullOrWhiteSpace(line)) 
                    return line;
                    
                // First line doesn't need the tree prefix, subsequent lines do
                return index == 0 ? $"   {line}" : $"{InfoColor}│     {line}{ResetColor}";
            });
            
            return string.Join('\n', formattedLines);
        }
        catch
        {
            return obj.ToString() ?? "null";
        }
    }

    /// <summary>
    /// Extract error location from exception stack trace
    /// </summary>
    private static string? GetErrorLocation(Exception exception)
    {
        try
        {
            var stackTrace = new StackTrace(exception, true);
            
            // Find the first frame with file information that's not from system assemblies
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                if (frame == null) continue;
                
                var fileName = frame.GetFileName();
                var lineNumber = frame.GetFileLineNumber();
                
                if (!string.IsNullOrEmpty(fileName) && lineNumber > 0)
                {
                    // Skip system assemblies and framework code
                    if (!fileName.Contains("System.") && 
                        !fileName.Contains("Microsoft.") && 
                        !fileName.Contains("mscorlib") &&
                        !fileName.Contains("ServiceFrameworkLogger.cs"))
                    {
                        var shortFileName = Path.GetFileName(fileName);
                        var methodName = frame.GetMethod()?.Name ?? "Unknown";
                        return $"{shortFileName}:{lineNumber} in {methodName}()";
                    }
                }
            }
            
            // If no file information found, try to get at least the method name
            var topFrame = stackTrace.GetFrame(0);
            if (topFrame != null)
            {
                var method = topFrame.GetMethod();
                if (method != null)
                {
                    return $"{method.DeclaringType?.Name}.{method.Name}()";
                }
            }
        }
        catch
        {
            // If we can't get stack trace info, don't fail the logging
        }
        
        return null;
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
        Dictionary<string, string>? metadata = null,
        long durationMs = 0,
        bool isSuccess = true,
        Exception? exception = null,
        object? input = null,
        object? output = null)
    {
        ServiceFrameworkLogger.LogRequestResponse(
            logger,
            isSuccess ? "completed" : "failed",
            serviceName,
            methodName,
            subject,
            auditInfo?.ReqSeqId,
            metadata ?? auditInfo?.Metadata,
            durationMs,
            isSuccess,
            exception,
            input,
            output);
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
        Exception? exception = null,
        object? input = null,
        object? output = null)
    {
        ServiceFrameworkLogger.LogPubSub(
            logger,
            isSuccess ? "published" : "failed",
            serviceName,
            methodName,
            subject,
            mode,
            auditInfo?.ReqSeqId,
            auditInfo?.Metadata,
            durationMs,
            isSuccess,
            exception,
            input,
            output);
    }
}

/// <summary>
/// Audit information extracted from request/response DTOs
/// </summary>
public record AuditInfo(
    string? ReqSeqId,
    string? Timestamp = null,
    Dictionary<string, string>? Metadata = null);