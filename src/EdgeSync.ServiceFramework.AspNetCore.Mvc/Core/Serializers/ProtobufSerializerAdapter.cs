using NATS.Client.Core;
using Microsoft.Extensions.Logging;
using EdgeSync.ServiceFramework.Data;
using EdgeSync.ServiceFramework.Abstractions.Protos;
using EdgeSync.ServiceFramework.Abstractions.Serialization;
using System.Reflection;
using System.Text.Json;
using ProtobufEmpty = Google.Protobuf.WellKnownTypes.Empty;


namespace EdgeSync.ServiceFramework.Core.Serializers;

/// <summary>
/// Adapter for Protobuf serializers, providing a unified interface while handling Protobuf-specific constraints.
/// Protobuf serializers require specific message types and have limitations with generic object types.
/// </summary>
public class ProtobufSerializerAdapter : ISerializerAdapter
{
    private readonly ILogger<ProtobufSerializerAdapter> _logger;
    private readonly ProtobufTypeMapper _typeMapper;

    public ProtobufSerializerAdapter(ILogger<ProtobufSerializerAdapter> logger, ProtobufTypeMapper typeMapper)
    {
        _logger = logger;
        _typeMapper = typeMapper;
    }

    public SerializerType SerializerType => SerializerType.Protobuf;

    public bool CanAdapt(INatsSerializerRegistry serializerRegistry)
    {
        var registryType = serializerRegistry.GetType();
        return registryType.Name.Contains("Protobuf", StringComparison.OrdinalIgnoreCase) ||
               registryType.Namespace?.Contains("Protobuf", StringComparison.OrdinalIgnoreCase) == true;
    }

    public async Task<object?> SendRequestAsync(
        INatsConnection connection, 
        string subject, 
        object? request, 
        bool isOriginallyParameterless, 
        Type? expectedResponseType,
        INatsSerializerRegistry serializerRegistry)
    {
        _logger.LogDebug("ProtobufSerializerAdapter: Sending request to subject {Subject}, IsParameterless: {IsParameterless}, ExpectedResponseType: {ResponseType}", 
            subject, isOriginallyParameterless, expectedResponseType?.Name ?? "Unknown");

        if (isOriginallyParameterless)
        {
            if (request != null)
            {
                // Originally parameterless method with EnableAuditWrapper=true
                // Protobuf has issues with RequestDto<T> - throw descriptive error
                _logger.LogWarning("ProtobufSerializerAdapter: Protobuf not supported with audit wrapper for subject {Subject}", subject);
                throw new NotSupportedException(
                    $"Protobuf serialization is not supported for RequestDto<T> types. Subject: {subject}. " +
                    "Please use JSON serializer when EnableAuditWrapper=true.");
            }
            else
            {
                // Originally parameterless method with EnableAuditWrapper=false
                _logger.LogDebug("ProtobufSerializerAdapter: Sending Empty message for parameterless method with expected response type: {ResponseType}", 
                    expectedResponseType?.Name ?? "Unknown");
                
                var emptyMessage = EmptyMessage.Create();
                
                // Use universal Protobuf approach with UniversalMessage
                _logger.LogDebug("ProtobufSerializerAdapter: Using UniversalMessage for parameterless method, expected response type: {ResponseType}", 
                    expectedResponseType?.Name ?? "Unknown");
                
                try
                {
                    // Send empty request and expect UniversalMessage response
                    var response = await connection.RequestAsync<ProtobufEmpty, UniversalMessage>(subject, emptyMessage,
                        requestSerializer: serializerRegistry.GetSerializer<ProtobufEmpty>(),
                        replySerializer: serializerRegistry.GetDeserializer<UniversalMessage>());
                    
                    // Convert UniversalMessage back to expected .NET type
                    if (response.Data != null)
                    {
                        var result = UniversalProtobufConverter.FromUniversalMessage(response.Data, expectedResponseType);
                        _logger.LogDebug("ProtobufSerializerAdapter: Successfully converted UniversalMessage to {ResultType}", 
                            result?.GetType().Name ?? "null");
                        return result;
                    }
                    
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ProtobufSerializerAdapter: Error in UniversalMessage approach for subject {Subject}", subject);
                    throw;
                }
            }
        }
        else if (request != null)
        {
            // For requests with data - check if it's a RequestDto<T> which is problematic for Protobuf
            var requestType = request.GetType();
            if (requestType.IsGenericType && requestType.GetGenericTypeDefinition() == typeof(RequestDto<>))
            {
                _logger.LogWarning("ProtobufSerializerAdapter: RequestDto<T> not supported with Protobuf for subject {Subject}", subject);
                throw new NotSupportedException(
                    $"Protobuf serialization is not supported for RequestDto<T> types. Subject: {subject}. " +
                    "Please use JSON serializer when EnableAuditWrapper=true.");
            }

            // For other data types, attempt to use Protobuf but warn about potential issues
            _logger.LogDebug("ProtobufSerializerAdapter: Sending request with data, RequestType: {RequestType}", requestType.Name);
            _logger.LogWarning("ProtobufSerializerAdapter: Using object type with Protobuf may cause issues. Consider using strongly-typed messages.");
            
            try
            {
                var response = await connection.RequestAsync<object, object>(subject, request,
                    requestSerializer: serializerRegistry.GetSerializer<object>(),
                    replySerializer: serializerRegistry.GetDeserializer<object>());
                return response.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProtobufSerializerAdapter: Failed to serialize request with Protobuf. Consider using JSON serializer.");
                throw;
            }
        }
        else
        {
            // Fallback case - treat as parameterless with Empty message
            _logger.LogDebug("ProtobufSerializerAdapter: Fallback to Empty message handling");
            var emptyMessage = EmptyMessage.Create();
            
            _logger.LogWarning("ProtobufSerializerAdapter: Using string serialization for fallback response due to Protobuf type limitations");
            var response = await connection.RequestAsync<ProtobufEmpty, string>(subject, emptyMessage,
                requestSerializer: serializerRegistry.GetSerializer<ProtobufEmpty>(),
                replySerializer: serializerRegistry.GetDeserializer<string>());
            
            // Try to deserialize the string response back to object
            if (!string.IsNullOrEmpty(response.Data))
            {
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<object>(response.Data);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize Protobuf string response, returning raw string");
                    return response.Data;
                }
            }
            return null;
        }
    }

    public async Task PublishAsync(
        INatsConnection connection, 
        string subject, 
        object? message, 
        INatsSerializerRegistry serializerRegistry)
    {
        if (message == null)
        {
            // For parameterless publish - use Empty message
            var emptyMessage = EmptyMessage.Create();
            await connection.PublishAsync(subject, emptyMessage,
                serializer: serializerRegistry.GetSerializer<ProtobufEmpty>());
        }
        else
        {
            // For publish with data - check for RequestDto<T> compatibility issues
            var messageType = message.GetType();
            if (messageType.IsGenericType && messageType.GetGenericTypeDefinition() == typeof(RequestDto<>))
            {
                _logger.LogWarning("ProtobufSerializerAdapter: Publishing RequestDto<T> with Protobuf may cause issues");
            }

            await connection.PublishAsync(subject, message,
                serializer: serializerRegistry.GetSerializer<object>());
        }
    }


    public Type? GetEmptyMessageType()
    {
        // Protobuf requires Empty message for parameterless operations
        return typeof(ProtobufEmpty);
    }

    public bool IsTypeCompatible(Type type)
    {
        // Protobuf has specific compatibility requirements
        if (type == typeof(ProtobufEmpty))
        {
            return true;
        }

        // RequestDto<T> is problematic with Protobuf
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(RequestDto<>))
        {
            return false;
        }

        // ResponseDto<T> may also have issues
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ResponseDto<>))
        {
            return false;
        }

        // Generally, Protobuf works best with concrete protobuf message types
        // For now, we'll be permissive but log warnings
        return true;
    }

    /// <summary>
    /// Checks if a type is directly compatible with Protobuf serialization.
    /// With the generic JSON-in-Protobuf approach, most types can be handled via JSON serialization.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the type is directly compatible with Protobuf</returns>
    private bool IsProtobufCompatibleType(Type type)
    {
        // Null types are not compatible
        if (type == null)
        {
            return false;
        }

        // ProtobufEmpty is always compatible
        if (type == typeof(ProtobufEmpty))
        {
            return true;
        }

        // Check for Google.Protobuf.IMessage interface (generated Protobuf types)
        if (type.GetInterfaces().Any(i => 
            i.FullName == "Google.Protobuf.IMessage" || 
            i.Name == "IMessage" && i.Namespace?.Contains("Protobuf") == true))
        {
            return true;
        }

        // Primitive types work well
        if (type.IsPrimitive || type == typeof(string) || type == typeof(byte[]) || 
            type == typeof(decimal) || type == typeof(DateTime) || type == typeof(Guid))
        {
            return true;
        }

        // With JSON-in-Protobuf, we can handle most .NET types
        // Only exclude types that are fundamentally non-serializable
        if (type.IsGenericType)
        {
            var genericDefinition = type.GetGenericTypeDefinition();
            
            // Task<T> and similar async types should not be serialized
            if (genericDefinition == typeof(Task<>) || 
                genericDefinition == typeof(ValueTask<>))
            {
                return false;
            }
        }

        // Exclude delegates and other non-serializable types
        if (typeof(Delegate).IsAssignableFrom(type))
        {
            return false;
        }

        // With our generic approach, most other types can be handled via JSON
        // so we return true and let the JSON serialization handle it
        return true;
    }

    /// <summary>
    /// Gets a deserializer for the specified type using reflection
    /// </summary>
    private object GetDeserializer(INatsSerializerRegistry serializerRegistry, Type type)
    {
        var method = typeof(INatsSerializerRegistry).GetMethod("GetDeserializer", Type.EmptyTypes);
        if (method != null)
        {
            var genericMethod = method.MakeGenericMethod(type);
            return genericMethod.Invoke(serializerRegistry, null)!;
        }
        
        // Fallback to object deserializer
        return serializerRegistry.GetDeserializer<object>();
    }

    /// <summary>
    /// Deserializes a generic response (either direct object or JSON-in-Protobuf) to the expected .NET type
    /// </summary>
    private object? DeserializeGenericResponse(object responseData, Type expectedType)
    {
        try
        {
            _logger.LogDebug("ProtobufSerializerAdapter: Deserializing response data {DataType} to {ExpectedType}", 
                responseData.GetType().Name, expectedType.Name);

            // Case 1: Response is already the expected type (direct Protobuf message)
            if (expectedType.IsAssignableFrom(responseData.GetType()))
            {
                return responseData;
            }

            // Case 2: Response is a string (likely JSON)
            if (responseData is string jsonString)
            {
                _logger.LogDebug("ProtobufSerializerAdapter: Deserializing JSON string to {ExpectedType}", expectedType.Name);
                return JsonSerializer.Deserialize(jsonString, expectedType, GetJsonSerializerOptions());
            }

            // Case 3: Response has JSON data property (GenericResponse with JsonPayload)
            var jsonDataProperty = responseData.GetType().GetProperty("JsonData") ?? 
                                   responseData.GetType().GetProperty("json_data");
            
            if (jsonDataProperty != null)
            {
                var jsonData = jsonDataProperty.GetValue(responseData) as string;
                if (!string.IsNullOrEmpty(jsonData))
                {
                    _logger.LogDebug("ProtobufSerializerAdapter: Deserializing JSON payload to {ExpectedType}", expectedType.Name);
                    return JsonSerializer.Deserialize(jsonData, expectedType, GetJsonSerializerOptions());
                }
            }

            // Case 4: Try to serialize response to JSON and then deserialize to expected type
            // This handles cases where Protobuf gives us a structured object that needs conversion
            _logger.LogDebug("ProtobufSerializerAdapter: Converting response via JSON to {ExpectedType}", expectedType.Name);
            var intermediateJson = JsonSerializer.Serialize(responseData, GetJsonSerializerOptions());
            return JsonSerializer.Deserialize(intermediateJson, expectedType, GetJsonSerializerOptions());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProtobufSerializerAdapter: Failed to deserialize response to {ExpectedType}", expectedType.Name);
            
            // Fallback: return the raw response data
            return responseData;
        }
    }

    /// <summary>
    /// Gets JSON serializer options for consistent serialization
    /// </summary>
    private JsonSerializerOptions GetJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public SerializerEndpointConfig GetEndpointConfig(INatsSerializerRegistry serializerRegistry)
    {
        return new SerializerEndpointConfig
        {
            ParameterlessHandlerType = typeof(ProtobufEmpty),
            RequiresAuditWrapperHandling = true, // Protobuf needs special handling for audit wrappers
            RequestSerializer = serializerRegistry.GetSerializer<ProtobufEmpty>(),
            ResponseDeserializer = serializerRegistry.GetDeserializer<ProtobufEmpty>()
        };
    }
}