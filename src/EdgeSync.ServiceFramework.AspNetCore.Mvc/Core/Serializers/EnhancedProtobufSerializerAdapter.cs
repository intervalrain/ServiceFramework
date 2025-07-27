using NATS.Client.Core;
using Microsoft.Extensions.Logging;
using EdgeSync.ServiceFramework.Data;
using EdgeSync.ServiceFramework.Abstractions.Protos;
using EdgeSync.ServiceFramework.Abstractions.Serialization;
using System.Reflection;
using System.Text.Json;
using System.Collections;
using ProtobufEmpty = Google.Protobuf.WellKnownTypes.Empty;
using Google.Protobuf;

namespace EdgeSync.ServiceFramework.Core.Serializers;

/// <summary>
/// Enhanced Protobuf serializer adapter that provides type-safe operations
/// for complex generic types including ErrorOr&lt;T&gt; and collections.
/// Uses UniversalMessage for seamless type conversion.
/// </summary>
public class EnhancedProtobufSerializerAdapter : ITypedSerializerAdapter
{
    private readonly ILogger<EnhancedProtobufSerializerAdapter> _logger;
    private readonly ProtobufTypeMapper _typeMapper;

    public EnhancedProtobufSerializerAdapter(ILogger<EnhancedProtobufSerializerAdapter> logger, ProtobufTypeMapper typeMapper)
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

    #region Legacy ISerializerAdapter Implementation

    public async Task<object?> SendRequestAsync(
        INatsConnection connection, 
        string subject, 
        object? request, 
        bool isOriginallyParameterless, 
        Type? expectedResponseType,
        INatsSerializerRegistry serializerRegistry)
    {
        _logger.LogDebug("EnhancedProtobufSerializerAdapter: Legacy SendRequestAsync called for subject {Subject}", subject);

        if (isOriginallyParameterless)
        {
            if (request != null)
            {
                throw new NotSupportedException(
                    $"Protobuf serialization is not supported for RequestDto<T> types. Subject: {subject}. " +
                    "Please use JSON serializer when EnableAuditWrapper=true.");
            }

            return await SendParameterlessRequestInternalAsync(connection, subject, expectedResponseType, serializerRegistry);
        }

        if (request != null)
        {
            var requestType = request.GetType();
            if (requestType.IsGenericType && requestType.GetGenericTypeDefinition() == typeof(RequestDto<>))
            {
                throw new NotSupportedException(
                    $"Protobuf serialization is not supported for RequestDto<T> types. Subject: {subject}. " +
                    "Please use JSON serializer when EnableAuditWrapper=true.");
            }

            return await SendTypedRequestInternalAsync(connection, subject, request, expectedResponseType, serializerRegistry);
        }

        return await SendParameterlessRequestInternalAsync(connection, subject, expectedResponseType, serializerRegistry);
    }

    public async Task PublishAsync(
        INatsConnection connection, 
        string subject, 
        object? message, 
        INatsSerializerRegistry serializerRegistry)
    {
        if (message == null)
        {
            var emptyMessage = EmptyMessage.Create();
            await connection.PublishAsync(subject, emptyMessage,
                serializer: serializerRegistry.GetSerializer<ProtobufEmpty>());
        }
        else
        {
            var messageType = message.GetType();
            if (messageType.IsGenericType && messageType.GetGenericTypeDefinition() == typeof(RequestDto<>))
            {
                _logger.LogWarning("EnhancedProtobufSerializerAdapter: Publishing RequestDto<T> with Protobuf may cause issues");
            }

            var universalMessage = UniversalProtobufConverter.ToUniversalMessage(message);
            await connection.PublishAsync(subject, universalMessage,
                serializer: serializerRegistry.GetSerializer<UniversalMessage>());
        }
    }


    public Type? GetEmptyMessageType() => typeof(ProtobufEmpty);

    public bool IsTypeCompatible(Type type)
    {
        // RequestDto<T> is problematic with Protobuf
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(RequestDto<>))
        {
            return false;
        }

        // With UniversalMessage, we can handle most .NET types
        return IsSerializableType(type);
    }

    public SerializerEndpointConfig GetEndpointConfig(INatsSerializerRegistry serializerRegistry)
    {
        return new SerializerEndpointConfig
        {
            ParameterlessHandlerType = typeof(ProtobufEmpty),
            RequiresAuditWrapperHandling = true,
            RequestSerializer = serializerRegistry.GetSerializer<UniversalMessage>(),
            ResponseDeserializer = serializerRegistry.GetDeserializer<UniversalMessage>()
        };
    }

    #endregion

    #region ITypedSerializerAdapter Implementation

    public async Task<TResponse> SendRequestAsync<TRequest, TResponse>(
        INatsConnection connection,
        string subject,
        TRequest request,
        INatsSerializerRegistry serializerRegistry)
    {
        _logger.LogDebug("EnhancedProtobufSerializerAdapter: Sending typed request to subject {Subject}, RequestType: {RequestType}, ResponseType: {ResponseType}", 
            subject, typeof(TRequest).Name, typeof(TResponse).Name);

        try
        {
            // Convert request to UniversalMessage
            var requestMessage = UniversalProtobufConverter.ToUniversalMessage(request);

            // Send request and receive UniversalMessage response
            var response = await connection.RequestAsync<UniversalMessage, UniversalMessage>(subject, requestMessage,
                requestSerializer: serializerRegistry.GetSerializer<UniversalMessage>(),
                replySerializer: serializerRegistry.GetDeserializer<UniversalMessage>());

            // Convert response back to expected type
            var result = UniversalProtobufConverter.FromUniversalMessage(response.Data, typeof(TResponse));
            
            _logger.LogDebug("EnhancedProtobufSerializerAdapter: Successfully converted response to {ResponseType}", typeof(TResponse).Name);
            return (TResponse)result!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnhancedProtobufSerializerAdapter: Error in typed request for subject {Subject}", subject);
            throw;
        }
    }

    public async Task<TResponse> SendParameterlessRequestAsync<TResponse>(
        INatsConnection connection,
        string subject,
        INatsSerializerRegistry serializerRegistry)
    {
        _logger.LogDebug("EnhancedProtobufSerializerAdapter: Sending parameterless typed request to subject {Subject}, ResponseType: {ResponseType}", 
            subject, typeof(TResponse).Name);

        try
        {
            var emptyMessage = EmptyMessage.Create();

            // Send empty request and receive UniversalMessage response
            var response = await connection.RequestAsync<ProtobufEmpty, UniversalMessage>(subject, emptyMessage,
                requestSerializer: serializerRegistry.GetSerializer<ProtobufEmpty>(),
                replySerializer: serializerRegistry.GetDeserializer<UniversalMessage>());

            // Convert response back to expected type
            var result = UniversalProtobufConverter.FromUniversalMessage(response.Data, typeof(TResponse));
            
            _logger.LogDebug("EnhancedProtobufSerializerAdapter: Successfully converted parameterless response to {ResponseType}", typeof(TResponse).Name);
            return (TResponse)result!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnhancedProtobufSerializerAdapter: Error in parameterless typed request for subject {Subject}", subject);
            throw;
        }
    }

    public async Task PublishAsync<TMessage>(
        INatsConnection connection,
        string subject,
        TMessage message,
        INatsSerializerRegistry serializerRegistry)
    {
        _logger.LogDebug("EnhancedProtobufSerializerAdapter: Publishing typed message to subject {Subject}, MessageType: {MessageType}", 
            subject, typeof(TMessage).Name);

        try
        {
            if (message == null)
            {
                var emptyMessage = EmptyMessage.Create();
                await connection.PublishAsync(subject, emptyMessage,
                    serializer: serializerRegistry.GetSerializer<ProtobufEmpty>());
            }
            else
            {
                var universalMessage = UniversalProtobufConverter.ToUniversalMessage(message);
                await connection.PublishAsync(subject, universalMessage,
                    serializer: serializerRegistry.GetSerializer<UniversalMessage>());
            }
            
            _logger.LogDebug("EnhancedProtobufSerializerAdapter: Successfully published typed message to subject {Subject}", subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnhancedProtobufSerializerAdapter: Error publishing typed message to subject {Subject}", subject);
            throw;
        }
    }

    public bool CanHandleTypes(Type? requestType, Type? responseType)
    {
        // Check if we can handle the request type
        if (requestType != null && !CanHandleType(requestType))
        {
            return false;
        }

        // Check if we can handle the response type
        if (responseType != null && !CanHandleType(responseType))
        {
            return false;
        }

        return true;
    }

    public TypedSerializationConfig GetTypedConfig(Type? requestType, Type? responseType, INatsSerializerRegistry serializerRegistry)
    {
        return new TypedSerializationConfig
        {
            RequestSerializer = serializerRegistry.GetSerializer<UniversalMessage>(),
            ResponseDeserializer = serializerRegistry.GetDeserializer<UniversalMessage>(),
            ActualRequestType = typeof(UniversalMessage),
            ActualResponseType = typeof(UniversalMessage),
            RequestTransformer = obj => UniversalProtobufConverter.ToUniversalMessage(obj),
            ResponseTransformer = (obj, type) => UniversalProtobufConverter.FromUniversalMessage((UniversalMessage)obj!, type)
        };
    }

    #endregion

    #region Private Helper Methods

    private bool CanHandleType(Type type)
    {
        // RequestDto<T> is not supported
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(RequestDto<>))
        {
            return false;
        }

        // ResponseDto<T> may also have issues
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ResponseDto<>))
        {
            return false;
        }

        // Check if it's a serializable type
        return IsSerializableType(type);
    }

    private bool IsSerializableType(Type type)
    {
        // Exclude delegates and other non-serializable types
        if (typeof(Delegate).IsAssignableFrom(type))
        {
            return false;
        }

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

        return true;
    }

    private async Task<object?> SendParameterlessRequestInternalAsync(
        INatsConnection connection,
        string subject,
        Type? expectedResponseType,
        INatsSerializerRegistry serializerRegistry)
    {
        var emptyMessage = EmptyMessage.Create();

        var response = await connection.RequestAsync<ProtobufEmpty, UniversalMessage>(subject, emptyMessage,
            requestSerializer: serializerRegistry.GetSerializer<ProtobufEmpty>(),
            replySerializer: serializerRegistry.GetDeserializer<UniversalMessage>());

        if (response.Data != null && expectedResponseType != null)
        {
            return UniversalProtobufConverter.FromUniversalMessage(response.Data, expectedResponseType);
        }

        return response.Data;
    }

    private async Task<object?> SendTypedRequestInternalAsync(
        INatsConnection connection,
        string subject,
        object request,
        Type? expectedResponseType,
        INatsSerializerRegistry serializerRegistry)
    {
        var requestMessage = UniversalProtobufConverter.ToUniversalMessage(request);

        var response = await connection.RequestAsync<UniversalMessage, UniversalMessage>(subject, requestMessage,
            requestSerializer: serializerRegistry.GetSerializer<UniversalMessage>(),
            replySerializer: serializerRegistry.GetDeserializer<UniversalMessage>());

        if (response.Data != null && expectedResponseType != null)
        {
            return UniversalProtobufConverter.FromUniversalMessage(response.Data, expectedResponseType);
        }

        return response.Data;
    }

    #endregion
}