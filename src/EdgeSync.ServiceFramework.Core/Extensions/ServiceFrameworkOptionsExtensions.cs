namespace EdgeSync.ServiceFramework.Core.Extensions;

/// <summary>
/// Configuration options for Service Framework with multiple NATS connections support
/// </summary>
public static class ServiceFrameworkOptionsExtensions
{
    public static NatsConnectionBuilder AddConnection(this ServiceFrameworkOptions options, string name, string url)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Connection name cannot be null or empty", nameof(name));
        
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Connection URL cannot be null or empty", nameof(url));

        // Duplicate name check
        if (options.Connections.ContainsKey(name))
            throw new ArgumentException($"Connection with name '{name}' already exists. Please use a unique connection name.");

        var settings = new NatsConnectionSettings
        {
            Name = name,
            Url = url,
        };

        options.Connections[name] = settings;
        return new NatsConnectionBuilder(settings);
    }
}
