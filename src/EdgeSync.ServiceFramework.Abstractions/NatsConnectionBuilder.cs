using NATS.Client.Core;

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
    /// Sets the credential file path for authentication
    /// </summary>
    /// <param name="credFile">Path to the credential file</param>
    /// <returns>The builder instance for method chaining</returns>
    public NatsConnectionBuilder WithCredFile(string credFile)
    {
        _settings.CredFile = credFile;
        return this;
    }
}