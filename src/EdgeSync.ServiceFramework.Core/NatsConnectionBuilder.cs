using EdgeSync.ServiceFramework.Core.Serialization;

using NATS.Client.Core;
using NATS.Client.Serializers.Json;

namespace EdgeSync.ServiceFramework.Abstractions;

/// <summary>
/// Fluent API builder for configuring NATS connection settings
/// </summary>
public class NatsConnectionBuilder
{
    private readonly NatsConnectionSettings _settings;

    internal NatsConnectionBuilder(NatsConnectionSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Sets the serializer registry for this connection
    /// </summary>
    /// <param name="serializerRegistry">The NATS serializer registry to use</param>
    /// <returns>The builder instance for method chaining</returns>
    public NatsConnectionBuilder WithSerializerRegistry(INatsSerializerRegistry serializerRegistry)
    {
        _settings.NatsSerializerRegistry = serializerRegistry ?? throw new ArgumentNullException(nameof(serializerRegistry));
        return this;
    }

    /// <summary>
    /// Sets the serializer registry for this connection using a string identifier
    /// </summary>
    /// <param name="serializerName">The serializer name: "json", "protobuf", or "default"</param>
    /// <returns>The builder instance for method chaining</returns>
    public NatsConnectionBuilder WithSerializerRegistry(string serializerName)
    {
        _settings.NatsSerializerRegistry = GetSerializerRegistry(serializerName);
        return this;
    }

    /// <summary>
    /// Sets the credential file path for authentication
    /// </summary>
    /// <param name="credFile">Path to the credential file</param>
    /// <returns>The builder instance for method chaining</returns>
    public NatsConnectionBuilder WithCredFile(string credFile)
    {
        _settings.CredFile = credFile;
        return this;
    }

    public INatsSerializerRegistry GetSerializerRegistry(string serializerName)
    {
        return serializerName.ToLowerInvariant() switch
        {
            "json" => NatsJsonSerializerRegistry.Default,
            "protobuf" => NatsProtobufSerializerRegistry.Default,
            _ => NatsDefaultSerializerRegistry.Default
        };
    }
}