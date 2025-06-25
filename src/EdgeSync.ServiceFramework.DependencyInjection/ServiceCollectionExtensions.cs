using System.Reflection;


using EdgeSync.ServiceFramework.JetStream;
using EdgeSync.ServiceFramework.KeyValueStore;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.DependencyInjection;

/// <summary>
/// Extension methods for configuring Service Framework services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Service Framework services to the service collection with configuration support
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <param name="configuration">The IConfiguration to bind options from</param>
    /// <param name="sectionName">The configuration section name (default: "ServiceFramework")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddServiceFramework(this IServiceCollection services, IConfiguration configuration, string sectionName = "ServiceFramework")
    {
        var options = new ServiceFrameworkOptions();
        configuration.GetSection(sectionName).Bind(options);
        services.AddSingleton(Options.Create(options));
        
        return AddServiceFrameworkCore(services, options);
    }

    /// <summary>
    /// Adds Service Framework services to the service collection with options configuration
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <param name="configureOptions">Action to configure the ServiceFrameworkOptions</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddServiceFramework(this IServiceCollection services, Action<ServiceFrameworkOptions> configureOptions)
    {
        var options = new ServiceFrameworkOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(Options.Create(options));
        
        return AddServiceFrameworkCore(services, options);
    }

    /// <summary>
    /// Core method for adding Service Framework services
    /// </summary>
    private static IServiceCollection AddServiceFrameworkCore(IServiceCollection services, ServiceFrameworkOptions options)
    {
        // Validate the options before proceeding
        options.Validate();
        
        // Convert ServiceFrameworkOptions to legacy NatsApiOptions for backward compatibility
        var legacyOptions = ConvertToLegacyOptions(options);
        ServiceConfig.Initialize(legacyOptions);

        // Configure legacy options for existing code
        services.Configure<NatsApiOptions>(opts =>
        {
            opts.MsgBrokerUrl = legacyOptions.MsgBrokerUrl;
            opts.MsgBusUrl = legacyOptions.MsgBusUrl;
            opts.MsgBrokerCredFile = legacyOptions.MsgBrokerCredFile;
            opts.MsgBusCredFile = legacyOptions.MsgBusCredFile;
        });

        // Register factories
        services.AddSingleton<IJetStreamClientFactory, JetStreamClientFactory>();
        services.AddSingleton<INatsConnectionFactory, NatsConnectionFactory>();

        if (ServiceConfig.MsgBusUrl != null)
        {
            // Register INatsConnection properly using the service provider
            services.AddSingleton<INatsConnection>(sp =>
            {
                var factory = sp.GetRequiredService<INatsConnectionFactory>();
                return factory.CreateConnectionAsync().GetAwaiter().GetResult();
            });
        }

        services.AddSingleton<IKVStore, KVStoreClient>();

        // Register client services
        services.AddSingleton<IBrokerJetStreamClient>(sp => 
        {
            var factory = sp.GetRequiredService<IJetStreamClientFactory>();
            var client = factory.CreateMsgBrokerClient();
            return client;
        });
        
        services.AddSingleton<IBusJetStreamClient>(sp => 
        {
            var factory = sp.GetRequiredService<IJetStreamClientFactory>();
            var client = factory.CreateMsgBusClient();
            return client;
        });

        return services;
    }

    /// <summary>
    /// Converts ServiceFrameworkOptions to legacy NatsApiOptions for backward compatibility
    /// </summary>
    private static NatsApiOptions ConvertToLegacyOptions(ServiceFrameworkOptions options)
    {
        var legacyOptions = new NatsApiOptions();

        // Set default connection or first available connection as MsgBus
        var defaultConnectionName = options.DefaultConnection ?? options.Connections.Keys.FirstOrDefault();
        if (defaultConnectionName != null && options.Connections.TryGetValue(defaultConnectionName, out var defaultConnection))
        {
            legacyOptions.MsgBusUrl = defaultConnection.Url;
            legacyOptions.MsgBusCredFile = defaultConnection.CredFile;
        }

        // Try to find "broker" connection, otherwise use default
        if (options.Connections.TryGetValue("broker", out var brokerConnection))
        {
            legacyOptions.MsgBrokerUrl = brokerConnection.Url;
            legacyOptions.MsgBrokerCredFile = brokerConnection.CredFile;
        }
        else if (defaultConnectionName != null && options.Connections.TryGetValue(defaultConnectionName, out var fallbackConnection))
        {
            legacyOptions.MsgBrokerUrl = fallbackConnection.Url;
            legacyOptions.MsgBrokerCredFile = fallbackConnection.CredFile;
        }

        // Try to find "bus" connection, otherwise use default  
        if (options.Connections.TryGetValue("bus", out var busConnection))
        {
            legacyOptions.MsgBusUrl = busConnection.Url;
            legacyOptions.MsgBusCredFile = busConnection.CredFile;
        }

        return legacyOptions;
    }
    /// <summary>
    /// Adds NATS API services to the service collection with options configuration
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <param name="configureOptions">Optional action to configure the NatsApiOptions</param>
    /// <returns>The service collection for chaining</returns>
    [Obsolete("Use AddServiceFramework instead. This method will be removed in a future version.")]
    public static IServiceCollection AddNatsApi(this IServiceCollection services, Action<NatsApiOptions>? configureOptions = null)
    {
        var options = new NatsApiOptions();

        configureOptions?.Invoke(options);

        ServiceConfig.Initialize(options);

        // Inject Nats Connection Url & CredFile
        services.Configure<NatsApiOptions>(options =>
        {
            options.MsgBrokerUrl = options.MsgBrokerUrl;
            options.MsgBusUrl = options.MsgBusUrl;
            options.MsgBrokerCredFile = options.MsgBrokerCredFile;
            options.MsgBusCredFile = options.MsgBusCredFile;
        });

        // Register factories
        services.AddSingleton<IJetStreamClientFactory, JetStreamClientFactory>();
        services.AddSingleton<INatsConnectionFactory, NatsConnectionFactory>();

        if (ServiceConfig.MsgBusUrl != null)
        {
            // this connection is used for connecting to msg bus, if the url is not set, it will be ignored.
            // Register INatsConnection properly using the service provider
            services.AddSingleton<INatsConnection>(sp =>
            {
                var factory = sp.GetRequiredService<INatsConnectionFactory>();
                return factory.CreateConnectionAsync().GetAwaiter().GetResult();
            });
        }

        services.AddSingleton<IKVStore, KVStoreClient>();

        // Register client services
        services.AddSingleton<IBrokerJetStreamClient>(sp => 
        {
            var factory = sp.GetRequiredService<IJetStreamClientFactory>();
            var client = factory.CreateMsgBrokerClient();
            return client;
        });
        
        services.AddSingleton<IBusJetStreamClient>(sp => 
        {
            var factory = sp.GetRequiredService<IJetStreamClientFactory>();
            var client = factory.CreateMsgBusClient();
            return client;
        });

        return services;
    }

    public static IServiceCollection AddServiceHandlersFromAssembly<TEntry>(this IServiceCollection services)
        where TEntry : class
    {
        return services.AddServiceHandlersFromAssembly(typeof(TEntry).Assembly);
    }

    public static IServiceCollection AddServiceHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var servicesToRegister = assembly.GetTypes()
            .Where(t => !t.IsAbstract && 
                  !t.IsInterface && 
                  (typeof(ServiceHandler).IsAssignableFrom(t) || 
                   typeof(BaseEventHandler).IsAssignableFrom(t)) &&
                  !t.GetCustomAttributes(typeof(ObsoleteAttribute), false).Any())
            .ToList();
        
        foreach (var serviceType in servicesToRegister)
        {
            services.Add(new ServiceDescriptor(
                typeof(IHostedService), 
                serviceType, 
                ServiceLifetime.Singleton));
        }
        
        return services;
    }
}