using System.Reflection;


using EdgeSync.ServiceFramework.JetStream;
using EdgeSync.ServiceFramework.KeyValueStore;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNatsApi(this IServiceCollection services)
    {
        // Register factories
        services.AddSingleton<IJetStreamClientFactory, JetStreamClientFactory>();
        services.AddSingleton<INatsConnectionFactory, NatsConnectionFactory>();
        
        // Register INatsConnection properly using the service provider
        services.AddSingleton<INatsConnection>(sp =>
        {
            var factory = sp.GetRequiredService<INatsConnectionFactory>();
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

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