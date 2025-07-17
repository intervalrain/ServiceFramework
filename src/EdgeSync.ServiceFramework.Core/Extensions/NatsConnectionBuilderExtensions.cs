using EdgeSync.ServiceFramework.Core.Serialization;

using NATS.Client.Core;
using NATS.Client.Serializers.Json;

namespace EdgeSync.ServiceFramework.Core.Extensions;

public static class NatsConnectionBuilderExtensions
{
    /// <summary>
    /// Sets the serializer registry for this connection using a string identifier
    /// </summary>
    /// <param name="builder">The NatsConnectionBuilder</param>
    /// <param name="serializerName">The serializer name: "json", "protobuf", or "default"</param>
    /// <returns>The builder instance for method chaining</returns>
    public static NatsConnectionBuilder WithSerializerRegistry(this NatsConnectionBuilder builder, string serializerName)
    {
        if (string.IsNullOrWhiteSpace(serializerName))
            throw new ArgumentException("Serializer name cannot be null or empty", nameof(serializerName));

        builder.WithSerializerRegistry(GetSerializerRegistry(serializerName));
        return builder;
    }

    /// <summary>
    /// Gets the appropriate serializer registry based on the string name
    /// </summary>
    private static INatsSerializerRegistry GetSerializerRegistry(string serializerName)
    {
        return serializerName.ToLowerInvariant() switch
        {
            "json" => NatsJsonSerializerRegistry.Default,
            "protobuf" => NatsProtobufSerializerRegistry.Default,
            "default" => NatsDefaultSerializerRegistry.Default,
            _ => throw new ArgumentException($"Unknown serializer '{serializerName}'. Supported values: json, protobuf, default", nameof(serializerName))
        };
    }
}