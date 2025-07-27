using NATS.Client.Core;
using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.Core.Serializers;

/// <summary>
/// Adapter for JSON serializers, providing a unified interface for JSON-specific operations.
/// JSON serializers are generally more flexible with object types and can handle nulls gracefully.
/// Enhanced with type-safe operations while maintaining backward compatibility.
/// </summary>
public class JsonSerializerAdapter : ITypedSerializerAdapter
{
    private readonly ILogger<JsonSerializerAdapter> _logger;

    public JsonSerializerAdapter(ILogger<JsonSerializerAdapter> logger)
    {
        _logger = logger;
    }

    public SerializerType SerializerType => SerializerType.Json;

    public bool CanAdapt(INatsSerializerRegistry serializerRegistry)
    {
        var registryType = serializerRegistry.GetType();
        
        // Default NATS serializer registry uses JSON by default
        if (registryType == typeof(NatsDefaultSerializerRegistry))
        {
            return true;
        }
        
        // Check for JSON-related naming patterns
        return registryType.Name.Contains("Json", StringComparison.OrdinalIgnoreCase) ||
               registryType.Namespace?.Contains("Json", StringComparison.OrdinalIgnoreCase) == true ||
               registryType.Name.Contains("System.Text.Json", StringComparison.OrdinalIgnoreCase) ||
               registryType.Name.Contains("Newtonsoft", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<object?> SendRequestAsync(
        INatsConnection connection, 
        string subject, 
        object? request, 
        bool isOriginallyParameterless, 
        Type? expectedResponseType,
        INatsSerializerRegistry serializerRegistry)
    {
        _logger.LogDebug("JsonSerializerAdapter: Sending request to subject {Subject}, IsParameterless: {IsParameterless}, ExpectedResponseType: {ResponseType}", 
            subject, isOriginallyParameterless, expectedResponseType?.Name ?? "Unknown");

        if (isOriginallyParameterless)
        {
            if (request != null)
            {
                // Originally parameterless method with EnableAuditWrapper=true
                _logger.LogDebug("JsonSerializerAdapter: Sending request with audit wrapper for parameterless method");
                var response = await connection.RequestAsync<object, object>(subject, request,
                    requestSerializer: serializerRegistry.GetSerializer<object>(),
                    replySerializer: serializerRegistry.GetDeserializer<object>());
                return response.Data;
            }
            else
            {
                // Originally parameterless method with EnableAuditWrapper=false
                _logger.LogDebug("JsonSerializerAdapter: Sending null request for parameterless method");
                var response = await connection.RequestAsync<object?, object>(subject, null,
                    requestSerializer: null, // Let NATS handle null serialization
                    replySerializer: serializerRegistry.GetDeserializer<object>());
                return response.Data;
            }
        }
        else if (request != null)
        {
            // For requests with data
            _logger.LogDebug("JsonSerializerAdapter: Sending request with data, RequestType: {RequestType}", 
                request.GetType().Name);
            var response = await connection.RequestAsync<object, object>(subject, request,
                requestSerializer: serializerRegistry.GetSerializer<object>(),
                replySerializer: serializerRegistry.GetDeserializer<object>());
            return response.Data;
        }
        else
        {
            // Fallback case - treat as parameterless
            _logger.LogDebug("JsonSerializerAdapter: Fallback to parameterless handling");
            var response = await connection.RequestAsync<object?, object>(subject, null,
                requestSerializer: null,
                replySerializer: serializerRegistry.GetDeserializer<object>());
            return response.Data;
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
            // For parameterless publish - use null payload
            await connection.PublishAsync<object?>(subject, null, serializer: null);
        }
        else
        {
            // For publish with data
            await connection.PublishAsync(subject, message,
                serializer: serializerRegistry.GetSerializer<object>());
        }
    }


    public Type? GetEmptyMessageType()
    {
        // JSON serializers can handle null/object types gracefully
        return typeof(object);
    }

    public bool IsTypeCompatible(Type type)
    {
        // JSON serializers are generally compatible with most types
        // They can handle complex objects, RequestDto<T>, ErrorOr<T>, etc.
        return true;
    }

    public SerializerEndpointConfig GetEndpointConfig(INatsSerializerRegistry serializerRegistry)
    {
        return new SerializerEndpointConfig
        {
            ParameterlessHandlerType = typeof(object),
            RequiresAuditWrapperHandling = false, // JSON can handle RequestDto<T> natively
            RequestSerializer = null, // Use registry default
            ResponseDeserializer = null // Use registry default
        };
    }

    #region ITypedSerializerAdapter Implementation

    public async Task<TResponse> SendRequestAsync<TRequest, TResponse>(
        INatsConnection connection,
        string subject,
        TRequest request,
        INatsSerializerRegistry serializerRegistry)
    {
        _logger.LogDebug("JsonSerializerAdapter: Sending typed request to subject {Subject}, RequestType: {RequestType}, ResponseType: {ResponseType}", 
            subject, typeof(TRequest).Name, typeof(TResponse).Name);

        try
        {
            var response = await connection.RequestAsync<TRequest, TResponse>(subject, request,
                requestSerializer: serializerRegistry.GetSerializer<TRequest>(),
                replySerializer: serializerRegistry.GetDeserializer<TResponse>());
            
            _logger.LogDebug("JsonSerializerAdapter: Successfully received typed response for subject {Subject}", subject);
            return response.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JsonSerializerAdapter: Error in typed request for subject {Subject}", subject);
            throw;
        }
    }

    public async Task<TResponse> SendParameterlessRequestAsync<TResponse>(
        INatsConnection connection,
        string subject,
        INatsSerializerRegistry serializerRegistry)
    {
        _logger.LogDebug("JsonSerializerAdapter: Sending parameterless typed request to subject {Subject}, ResponseType: {ResponseType}", 
            subject, typeof(TResponse).Name);

        try
        {
            var response = await connection.RequestAsync<object?, TResponse>(subject, null,
                requestSerializer: null, // Let NATS handle null serialization
                replySerializer: serializerRegistry.GetDeserializer<TResponse>());
            
            _logger.LogDebug("JsonSerializerAdapter: Successfully received parameterless typed response for subject {Subject}", subject);
            return response.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JsonSerializerAdapter: Error in parameterless typed request for subject {Subject}", subject);
            throw;
        }
    }

    public async Task PublishAsync<TMessage>(
        INatsConnection connection,
        string subject,
        TMessage message,
        INatsSerializerRegistry serializerRegistry)
    {
        _logger.LogDebug("JsonSerializerAdapter: Publishing typed message to subject {Subject}, MessageType: {MessageType}", 
            subject, typeof(TMessage).Name);

        try
        {
            if (message == null)
            {
                await connection.PublishAsync<TMessage?>(subject, default(TMessage), serializer: null);
            }
            else
            {
                await connection.PublishAsync(subject, message,
                    serializer: serializerRegistry.GetSerializer<TMessage>());
            }
            
            _logger.LogDebug("JsonSerializerAdapter: Successfully published typed message to subject {Subject}", subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JsonSerializerAdapter: Error publishing typed message to subject {Subject}", subject);
            throw;
        }
    }

    public bool CanHandleTypes(Type? requestType, Type? responseType)
    {
        // JSON serializers can handle virtually any .NET type
        // Only check for non-serializable types
        if (requestType != null && !IsSerializableType(requestType))
        {
            return false;
        }

        if (responseType != null && !IsSerializableType(responseType))
        {
            return false;
        }

        return true;
    }

    public TypedSerializationConfig GetTypedConfig(Type? requestType, Type? responseType, INatsSerializerRegistry serializerRegistry)
    {
        return new TypedSerializationConfig
        {
            RequestSerializer = requestType != null ? GetSerializerForType(serializerRegistry, requestType) : null,
            ResponseDeserializer = responseType != null ? GetDeserializerForType(serializerRegistry, responseType) : null,
            ActualRequestType = requestType ?? typeof(object),
            ActualResponseType = responseType ?? typeof(object),
            RequestTransformer = null, // JSON doesn't need transformation
            ResponseTransformer = null // JSON doesn't need transformation
        };
    }

    #endregion

    #region Private Helper Methods

    private bool IsSerializableType(Type type)
    {
        // Exclude types that cannot be serialized
        if (typeof(Delegate).IsAssignableFrom(type))
        {
            return false;
        }

        if (type.IsGenericType)
        {
            var genericDefinition = type.GetGenericTypeDefinition();
            
            // Async types should not be serialized
            if (genericDefinition == typeof(Task<>) || 
                genericDefinition == typeof(ValueTask<>))
            {
                return false;
            }
        }

        return true;
    }

    private object GetSerializerForType(INatsSerializerRegistry serializerRegistry, Type type)
    {
        var method = typeof(INatsSerializerRegistry).GetMethod("GetSerializer", Type.EmptyTypes);
        if (method != null)
        {
            var genericMethod = method.MakeGenericMethod(type);
            return genericMethod.Invoke(serializerRegistry, null)!;
        }
        
        return serializerRegistry.GetSerializer<object>();
    }

    private object GetDeserializerForType(INatsSerializerRegistry serializerRegistry, Type type)
    {
        var method = typeof(INatsSerializerRegistry).GetMethod("GetDeserializer", Type.EmptyTypes);
        if (method != null)
        {
            var genericMethod = method.MakeGenericMethod(type);
            return genericMethod.Invoke(serializerRegistry, null)!;
        }
        
        return serializerRegistry.GetDeserializer<object>();
    }

    #endregion
}