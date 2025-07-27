using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EdgeSync.ServiceFramework.Core.Serializers;

/// <summary>
/// Extension methods for registering serializer adapters in the dependency injection container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the serializer adapter factory and built-in adapters to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSerializerAdapters(this IServiceCollection services)
    {
        // Register the factory as singleton since it caches adapters
        services.TryAddSingleton<ISerializerAdapterFactory, SerializerAdapterFactory>();
        
        // Register type mapper for Protobuf type conversions
        services.TryAddSingleton<ProtobufTypeMapper>();
        
        // Register individual adapters as transient since factory creates them
        services.TryAddTransient<JsonSerializerAdapter>();
        services.TryAddTransient<ProtobufSerializerAdapter>();
        
        return services;
    }
    
    /// <summary>
    /// Adds a custom serializer adapter to the service collection
    /// </summary>
    /// <typeparam name="TAdapter">The adapter type</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSerializerAdapter<TAdapter>(this IServiceCollection services) 
        where TAdapter : class, ISerializerAdapter
    {
        services.TryAddTransient<TAdapter>();
        return services;
    }
    
    /// <summary>
    /// Adds a custom serializer adapter instance to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="adapter">The adapter instance</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSerializerAdapter(this IServiceCollection services, ISerializerAdapter adapter)
    {
        services.AddSingleton(adapter);
        return services;
    }
    
    /// <summary>
    /// Configures the serializer adapter factory with custom adapters after service container is built
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <param name="configureAdapters">Action to configure additional adapters</param>
    public static void ConfigureSerializerAdapters(this IServiceProvider serviceProvider, Action<ISerializerAdapterFactory> configureAdapters)
    {
        var factory = serviceProvider.GetService<ISerializerAdapterFactory>();
        if (factory != null)
        {
            configureAdapters(factory);
        }
    }
}

/// <summary>
/// Builder for configuring serializer adapters with fluent API
/// </summary>
public class SerializerAdapterBuilder
{
    private readonly IServiceCollection _services;
    
    internal SerializerAdapterBuilder(IServiceCollection services)
    {
        _services = services;
    }
    
    /// <summary>
    /// Adds a custom serializer adapter
    /// </summary>
    /// <typeparam name="TAdapter">The adapter type</typeparam>
    /// <returns>The builder for chaining</returns>
    public SerializerAdapterBuilder AddAdapter<TAdapter>() where TAdapter : class, ISerializerAdapter
    {
        _services.AddSerializerAdapter<TAdapter>();
        return this;
    }
    
    /// <summary>
    /// Adds a custom serializer adapter instance
    /// </summary>
    /// <param name="adapter">The adapter instance</param>
    /// <returns>The builder for chaining</returns>
    public SerializerAdapterBuilder AddAdapter(ISerializerAdapter adapter)
    {
        _services.AddSerializerAdapter(adapter);
        return this;
    }
}

/// <summary>
/// Additional extension methods for fluent configuration
/// </summary>
public static class SerializerAdapterServiceCollectionExtensions
{
    /// <summary>
    /// Adds serializer adapters with fluent configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSerializerAdapters(this IServiceCollection services, Action<SerializerAdapterBuilder> configure)
    {
        services.AddSerializerAdapters();
        
        var builder = new SerializerAdapterBuilder(services);
        configure(builder);
        
        return services;
    }
}