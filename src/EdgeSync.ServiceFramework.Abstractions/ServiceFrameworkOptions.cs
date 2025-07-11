using NATS.Client.Core;

namespace EdgeSync.ServiceFramework;

/// <summary>
/// Configuration options for Service Framework with multiple NATS connections support
/// </summary>
public class ServiceFrameworkOptions
{
    /// <summary>
    /// Default connection name to use when not specified
    /// </summary>
    public string? DefaultConnection { get; set; }
    
    /// <summary>
    /// Default serializer registry to use for connections that don't specify one
    /// </summary>
    public INatsSerializerRegistry DefaultSerializerRegistry { get; set; } = NatsDefaultSerializerRegistry.Default;
    
    /// <summary>
    /// Dictionary of named NATS connections
    /// </summary>
    public Dictionary<string, NatsConnectionSettings> Connections { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a new connection with the specified name and URL.
    /// Provides a fluent API for connection configuration.
    /// </summary>
    /// <param name="name">The connection name</param>
    /// <param name="url">The NATS URL</param>
    /// <returns>A connection builder for fluent configuration</returns>
    /// <exception cref="ArgumentException">Thrown when connection name already exists</exception>
    public NatsConnectionBuilder AddConnection(string name, string url)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Connection name cannot be null or empty", nameof(name));
        
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Connection URL cannot be null or empty", nameof(url));

        // Duplicate name check
        if (Connections.ContainsKey(name))
            throw new ArgumentException($"Connection with name '{name}' already exists. Please use a unique connection name.");

        var settings = new NatsConnectionSettings
        {
            Name = name,
            Url = url,
            NatsSerializerRegistry = DefaultSerializerRegistry
        };

        Connections[name] = settings;
        return new NatsConnectionBuilder(settings);
    }

    /// <summary>
    /// Validates the configuration for duplicate names and other issues
    /// </summary>
    public void Validate()
    {
        // Check for empty connection names
        var emptyKeys = Connections.Keys.Where(string.IsNullOrWhiteSpace).ToList();
        if (emptyKeys.Any())
        {
            throw new ArgumentException("Connection names cannot be null or empty");
        }

        // Ensure DefaultConnection exists if specified
        if (!string.IsNullOrEmpty(DefaultConnection) && !Connections.ContainsKey(DefaultConnection))
        {
            throw new ArgumentException($"Default connection '{DefaultConnection}' not found in connections dictionary. Please configure DefaultConnection property in ServiceFrameworkOptions through appsettings.json or configuration in Program.cs");
        }
    }
}

/// <summary>
/// Settings for a named NATS connection
/// </summary>
public class NatsConnectionSettings
{
    /// <summary>
    /// URL for the NATS connection
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Path to the credential file for authentication
    /// </summary>
    public string? CredFile { get; set; }

    /// <summary>
    /// Connection name for identification
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Serializer registry for the NATS connection
    /// </summary>
    public INatsSerializerRegistry NatsSerializerRegistry = NatsDefaultSerializerRegistry.Default;
}