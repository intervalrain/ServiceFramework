using System.Text.Json;

namespace EdgeSync.ServiceFramework.Data.Json;

/// <summary>
/// Extension methods for configuring JSON serialization with ServiceFramework standards
/// </summary>
public static class ServiceFrameworkJsonExtensions
{
    /// <summary>
    /// Configures JSON options with ServiceFramework standards including Error converter
    /// </summary>
    public static JsonSerializerOptions ConfigureForServiceFramework(this JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.WriteIndented = true;
        options.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        
        // Add custom converters for ErrorOr.Error to ensure camelCase consistency
        options.Converters.Add(new ErrorJsonConverter());
        options.Converters.Add(new ErrorListJsonConverter());
        
        return options;
    }
}