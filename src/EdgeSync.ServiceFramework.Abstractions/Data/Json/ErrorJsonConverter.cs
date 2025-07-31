using System.Text.Json;
using System.Text.Json.Serialization;
using ErrorOr;

namespace EdgeSync.ServiceFramework.Data.Json;

/// <summary>
/// Custom JSON converter for ErrorOr.Error to ensure camelCase property names
/// This converter handles both serialization and deserialization of Error objects
/// </summary>
public class ErrorJsonConverter : JsonConverter<Error>
{
    public override Error Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        string? code = null;
        string? description = null;
        ErrorType? errorType = null;
        int statusCode = 0;
        Dictionary<string, object>? metadata = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token");
            }

            string propertyName = reader.GetString()!;
            reader.Read();

            switch (propertyName.ToLowerInvariant())
            {
                case "code":
                    code = reader.GetString();
                    break;
                case "description":
                    description = reader.GetString();
                    break;
                case "type":
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        var typeString = reader.GetString();
                        if (Enum.TryParse<ErrorType>(typeString, true, out var parsedType))
                        {
                            errorType = parsedType;
                        }
                    }
                    break;
                case "statuscode":
                    if (reader.TokenType == JsonTokenType.Number)
                    {
                        statusCode = reader.GetInt32();
                    }
                    break;
                case "metadata":
                    metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        // If we don't have a code, we can't create an Error
        if (string.IsNullOrEmpty(code))
        {
            throw new JsonException("Error must have a code property");
        }

        // Create the appropriate Error based on the type and status code
        var error = errorType switch
        {
            ErrorType.Failure => Error.Failure(code, description),
            ErrorType.Validation => Error.Validation(code, description),
            ErrorType.NotFound => Error.NotFound(code, description),
            ErrorType.Conflict => Error.Conflict(code, description),
            ErrorType.Unauthorized => Error.Unauthorized(code, description),
            ErrorType.Forbidden => Error.Forbidden(code, description),
            _ => Error.Custom(statusCode > 0 ? statusCode : 500, code, description)
        };

        // Add metadata if available
        if (metadata != null && error.Metadata != null)
        {
            foreach (var kvp in metadata)
            {
                error.Metadata[kvp.Key] = kvp.Value;
            }
        }

        return error;
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

public class ErrorListJsonConverter : JsonConverter<List<Error>>
{
    private readonly ErrorJsonConverter _errorConverter = new();

    public override List<Error> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected StartArray token");
        }

        var errors = new List<Error>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            var error = _errorConverter.Read(ref reader, typeof(Error), options);
            errors.Add(error);
        }

        return errors;
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