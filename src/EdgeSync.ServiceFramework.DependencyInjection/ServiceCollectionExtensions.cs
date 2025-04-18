using System.Reflection;


using EdgeSync.ServiceFramework.JetStream;
using EdgeSync.ServiceFramework.KeyValueStore;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.DependencyInjection;

/// <summary>
/// Extension methods for configuring NATS API services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds NATS API services to the service collection with options configuration
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to</param>
    /// <param name="configureOptions">Optional action to configure the NatsApiOptions</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddNatsApi(this IServiceCollection services, Action<NatsApiOptions> configureOptions = null)
    {
        // Inject Nats Connection Url & CredFile
        services.Configure<NatsApiOptions>(options =>
        {
            options.MsgBrokerUrl ??= Environment.GetEnvironmentVariable("MSG_BROKER_URL") ?? "";
            options.MsgBusUrl ??= Environment.GetEnvironmentVariable("MSG_BUS_URL") ?? "";
            options.MsgBrokerCredFile ??= Environment.GetEnvironmentVariable("MSG_BROKER_CRED") ?? "";
            options.MsgBrokerCredFile ??= Environment.GetEnvironmentVariable("MSG_BUS_CRED") ?? "";

            configureOptions?.Invoke(options);
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