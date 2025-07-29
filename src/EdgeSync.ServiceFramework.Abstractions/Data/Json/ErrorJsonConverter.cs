using System.Text.Json;
using System.Text.Json.Serialization;
using ErrorOr;

namespace EdgeSync.ServiceFramework.Data.Json;

/// <summary>
/// Custom JSON converter for ErrorOr.Error to ensure camelCase property names
/// This converter only changes property names to camelCase, keeping all values unchanged
/// </summary>
public class ErrorJsonConverter : JsonConverter<Error>
{
    public override Error Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // For reading, we'll use the default behavior and let the system handle it
        // Since we're only concerned with output formatting (Write method)
        throw new NotSupportedException("ErrorJsonConverter is designed for write-only operations to ensure camelCase output.");
    }

    public override void Write(Utf8JsonWriter writer, Error value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Write properties with camelCase names but keep original values
        // Only write non-null values to respect JsonIgnoreCondition.WhenWritingNull
        if (!string.IsNullOrEmpty(value.Code))
        {
            writer.WriteString("code", value.Code);


            if (!string.IsNullOrEmpty(value.Description))
            {
                writer.WriteString("description", value.Description);
            }

            writer.WriteString("type", value.Type.ToString());
            writer.WriteNumber("statusCode", MapToHttpStatusCode(value.Type, value.NumericType));

            // Write metadata if it exists
            if (value.Metadata?.Any() == true)
            {
                writer.WritePropertyName("metadata");
                JsonSerializer.Serialize(writer, value.Metadata, options);
            }
        }
        
        writer.WriteEndObject();
    }

    /// <summary>
    /// Maps ErrorOr.ErrorType to appropriate HTTP status codes
    /// </summary>
    private static int MapToHttpStatusCode(ErrorType errorType, int numericType)
    {
        return errorType switch
        {
            ErrorType.Failure => 500,        // Internal Server Error
            ErrorType.Validation => 400,     // Bad Request
            ErrorType.NotFound => 404,       // Not Found
            ErrorType.Conflict => 409,       // Conflict
            ErrorType.Unauthorized => 401,   // Unauthorized
            ErrorType.Forbidden => 403,      // Forbidden
            _ => numericType > 0 ? numericType : 500  // Use original numericType if valid, otherwise default to 500
        };
    }
}

/// <summary>
/// Custom JSON converter for List&lt;Error&gt; to ensure camelCase property names
/// This converter is designed for write-only operations to ensure camelCase output
/// </summary>
public class ErrorListJsonConverter : JsonConverter<List<Error>>
{
    private readonly ErrorJsonConverter _errorConverter = new();

    public override List<Error> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // For reading, we'll use the default behavior and let the system handle it
        // Since we're only concerned with output formatting (Write method)
        throw new NotSupportedException("ErrorListJsonConverter is designed for write-only operations to ensure camelCase output.");
    }

    public override void Write(Utf8JsonWriter writer, List<Error> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        
        foreach (var error in value)
        {
            _errorConverter.Write(writer, error, options);
        }
        
        writer.WriteEndArray();
    }
}