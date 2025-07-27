using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.Core.Serializers;

/// <summary>
/// Enhanced serializer adapter interface that provides type-safe operations
/// while maintaining backward compatibility with the existing ISerializerAdapter
/// </summary>
public interface ITypedSerializerAdapter : ISerializerAdapter
{
    /// <summary>
    /// Sends a type-safe NATS request with proper generic type handling
    /// </summary>
    /// <typeparam name="TRequest">The request type</typeparam>
    /// <typeparam name="TResponse">The expected response type</typeparam>
    /// <param name="connection">The NATS connection</param>
    /// <param name="subject">The subject to send to</param>
    /// <param name="request">The request data</param>
    /// <param name="serializerRegistry">The serializer registry to use</param>
    /// <returns>The typed response</returns>
    Task<TResponse> SendRequestAsync<TRequest, TResponse>(
        INatsConnection connection,
        string subject,
        TRequest request,
        INatsSerializerRegistry serializerRegistry);

    /// <summary>
    /// Sends a parameterless request with proper type handling
    /// </summary>
    /// <typeparam name="TResponse">The expected response type</typeparam>
    /// <param name="connection">The NATS connection</param>
    /// <param name="subject">The subject to send to</param>
    /// <param name="serializerRegistry">The serializer registry to use</param>
    /// <returns>The typed response</returns>
    Task<TResponse> SendParameterlessRequestAsync<TResponse>(
        INatsConnection connection,
        string subject,
        INatsSerializerRegistry serializerRegistry);

    /// <summary>
    /// Publishes a type-safe message
    /// </summary>
    /// <typeparam name="TMessage">The message type</typeparam>
    /// <param name="connection">The NATS connection</param>
    /// <param name="subject">The subject to publish to</param>
    /// <param name="message">The message data</param>
    /// <param name="serializerRegistry">The serializer registry to use</param>
    Task PublishAsync<TMessage>(
        INatsConnection connection,
        string subject,
        TMessage message,
        INatsSerializerRegistry serializerRegistry);

    /// <summary>
    /// Determines if the adapter can handle the specified request and response types
    /// </summary>
    /// <param name="requestType">The request type</param>
    /// <param name="responseType">The response type</param>
    /// <returns>True if the adapter can handle these types</returns>
    bool CanHandleTypes(Type? requestType, Type? responseType);

    /// <summary>
    /// Gets the appropriate serializer and deserializer for the specified types
    /// </summary>
    /// <param name="requestType">The request type</param>
    /// <param name="responseType">The response type</param>
    /// <param name="serializerRegistry">The serializer registry</param>
    /// <returns>Configuration for type-safe serialization</returns>
    TypedSerializationConfig GetTypedConfig(Type? requestType, Type? responseType, INatsSerializerRegistry serializerRegistry);
}

/// <summary>
/// Configuration for type-safe serialization operations
/// </summary>
public record TypedSerializationConfig
{
    /// <summary>
    /// The request serializer to use
    /// </summary>
    public object? RequestSerializer { get; init; }

    /// <summary>
    /// The response deserializer to use
    /// </summary>
    public object? ResponseDeserializer { get; init; }

    /// <summary>
    /// The actual request type to use for NATS operation
    /// </summary>
    public Type ActualRequestType { get; init; } = typeof(object);

    /// <summary>
    /// The actual response type to use for NATS operation
    /// </summary>
    public Type ActualResponseType { get; init; } = typeof(object);

    /// <summary>
    /// Function to transform the request object before serialization
    /// </summary>
    public Func<object?, object?>? RequestTransformer { get; init; }

    /// <summary>
    /// Function to transform the response object after deserialization
    /// </summary>
    public Func<object?, Type, object?>? ResponseTransformer { get; init; }
}