using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.Abstractions;

/// <summary>
/// Configuration options for Service Framework with multiple NATS connections support
/// </summary>
public class ServiceFrameworkOptions
{
    public const string SectionName = "ServiceFramework";
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