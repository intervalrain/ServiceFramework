using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.Core.Serializers;

/// <summary>
/// Adapter interface that abstracts serializer-specific operations and type compatibility requirements.
/// This adapter pattern allows different serializer implementations (JSON, Protobuf, MessagePack, etc.)
/// to work through a unified interface, hiding their specific requirements and limitations.
/// </summary>
public interface ISerializerAdapter
{
    /// <summary>
    /// Gets the serializer type this adapter supports
    /// </summary>
    SerializerType SerializerType { get; }
    
    /// <summary>
    /// Determines if this adapter can adapt the specified serializer registry
    /// </summary>
    /// <param name="serializerRegistry">The serializer registry to check</param>
    /// <returns>True if this adapter can handle the serializer registry</returns>
    bool CanAdapt(INatsSerializerRegistry serializerRegistry);
    
    /// <summary>
    /// Sends a NATS request with serializer-specific handling
    /// </summary>
    /// <param name="connection">The NATS connection</param>
    /// <param name="subject">The subject to send to</param>
    /// <param name="request">The request data (can be null for parameterless methods)</param>
    /// <param name="isOriginallyParameterless">Whether the original method was parameterless</param>
    /// <param name="expectedResponseType">The expected response type from the application service</param>
    /// <param name="serializerRegistry">The serializer registry to use</param>
    /// <returns>The response object</returns>
    Task<object?> SendRequestAsync(
        INatsConnection connection,
        string subject,
        object? request,
        bool isOriginallyParameterless,
        Type? expectedResponseType,
        INatsSerializerRegistry serializerRegistry);
    
    /// <summary>
    /// Publishes a NATS message with serializer-specific handling
    /// </summary>
    /// <param name="connection">The NATS connection</param>
    /// <param name="subject">The subject to publish to</param>
    /// <param name="message">The message data (can be null for parameterless methods)</param>
    /// <param name="serializerRegistry">The serializer registry to use</param>
    Task PublishAsync(
        INatsConnection connection,
        string subject,
        object? message,
        INatsSerializerRegistry serializerRegistry);
    
    
    /// <summary>
    /// Gets the appropriate empty message type for parameterless operations
    /// </summary>
    /// <returns>The type to use for empty messages, or null if not needed</returns>
    Type? GetEmptyMessageType();
    
    /// <summary>
    /// Determines if the specified type is compatible with this serializer
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the type is compatible</returns>
    bool IsTypeCompatible(Type type);
    
    /// <summary>
    /// Gets the adapter-specific configuration for NATS service endpoints
    /// </summary>
    /// <param name="serializerRegistry">The serializer registry</param>
    /// <returns>Configuration for endpoint setup</returns>
    SerializerEndpointConfig GetEndpointConfig(INatsSerializerRegistry serializerRegistry);
}

/// <summary>
/// Represents the configuration needed for setting up NATS service endpoints
/// with serializer-specific requirements
/// </summary>
public record SerializerEndpointConfig
{
    /// <summary>
    /// The type to use for parameterless endpoint handlers
    /// </summary>
    public Type ParameterlessHandlerType { get; init; } = typeof(object);
    
    /// <summary>
    /// Whether this serializer requires special handling for audit wrappers
    /// </summary>
    public bool RequiresAuditWrapperHandling { get; init; } = false;
    
    /// <summary>
    /// Custom serializer to use for requests, if needed
    /// </summary>
    public object? RequestSerializer { get; init; }
    
    /// <summary>
    /// Custom deserializer to use for responses, if needed
    /// </summary>
    public object? ResponseDeserializer { get; init; }
}

/// <summary>
/// Enumeration of supported serializer types
/// </summary>
public enum SerializerType
{
    Json,
    Protobuf,
    MessagePack,
    Avro,
    Custom
}