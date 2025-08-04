using System.Reflection;
using System.Text.Json;

using EdgeSync.ServiceFramework.AspNetCore.Mvc.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Conventions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Decisions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Subscriptions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.SwaggerGen;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Handlers;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Core.Filters;
using EdgeSync.ServiceFramework.Core.Serializers;
using EdgeSync.ServiceFramework.Core.Abstractions;
using EdgeSync.ServiceFramework.Core.Services;
using EdgeSync.ServiceFramework.Core.RouteBuilders;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Swashbuckle.AspNetCore.SwaggerGen;
using EdgeSync.ServiceFramework.Core.SwaggerGen;
using EdgeSync.ServiceFramework.Core.Handlers;
using EdgeSync.ServiceFramework.Core.Controllers;
using EdgeSync.ServiceFramework.Core.Subscriptions;
using EdgeSync.ServiceFramework.Core;


namespace EdgeSync.ServiceFramework;

public static class ServiceCollectionExtensions
{
    private static readonly JsonSerializerOptions _defaultOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static IServiceCollection AddAutoConvention<TAutoConventionRouteBuilder>(this IServiceCollection services, Action<SwaggerGenOptions>? setupAction = null, bool autoRegisterSwaggerDoc = false)
        where TAutoConventionRouteBuilder : class, IAutoConventionRouteBuilder
    {
        services.AddAutoConventionOptions(out var options);
        services.AddApplicationServiceConvention<TAutoConventionRouteBuilder>(options);
        services.AddServiceFrameworkSwagger(options.UseExceptionHandler, setupAction, autoRegisterSwaggerDoc: autoRegisterSwaggerDoc);
        services.AddNatsServiceAutoDiscovery(options.Settings!);
        services.AddSubscriptionHandlers();
        services.AddSerializerAdapters();
        services.AddServiceFrameworkComponents();
        services.AddHostedService<ServiceFrameworkBackgroundService>();

        services.ConfigureHttpJsonOptions(opts =>
        {
            opts.SerializerOptions.PropertyNamingPolicy = _defaultOptions.PropertyNamingPolicy;
            opts.SerializerOptions.DictionaryKeyPolicy = _defaultOptions.DictionaryKeyPolicy;
            opts.SerializerOptions.WriteIndented = _defaultOptions.WriteIndented;
        });
        services.AddHttpContextAccessor();

        return services;
    }

    public static IServiceCollection AddAutoConvention(this IServiceCollection services, Action<SwaggerGenOptions>? setupAction = null)
    {
        return services.AddAutoConvention<DefaultAutoConventionRouteBuilder>(setupAction);
    }

    private static IServiceCollection AddAutoConventionOptions(this IServiceCollection services, out AutoConventionOptions options)
    {
        services.AddOptions<AutoConventionOptions>()
            .BindConfiguration(AutoConventionOptions.Section)
            .PostConfigure(opts => opts.Settings = GetDefaultAssemblies())
            .ValidateDataAnnotations()
            .ValidateOnStart();

        using var sp = services.BuildServiceProvider();
        options = sp.GetRequiredService<IOptions<AutoConventionOptions>>().Value;
        options.Settings ??= GetDefaultAssemblies();

        return services;
    }

    private static IServiceCollection AddApplicationServiceConvention<TAutoConventionRouteBuilder>(this IServiceCollection services, AutoConventionOptions options)
        where TAutoConventionRouteBuilder : class, IAutoConventionRouteBuilder
    {
        services.AddSingleton<IAutoConventionRouteBuilder, TAutoConventionRouteBuilder>();
        services.AddSingleton<IConventionDecisionMaker, ConventionDecisionMaker>();
        
        // Register the new builder components
        services.AddSingleton<IControllerModelBuilder, ControllerModelBuilder>();
        services.AddSingleton<IActionModelBuilder, ActionModelBuilder>();
        
        services.AddControllers().AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.PropertyNamingPolicy = _defaultOptions.PropertyNamingPolicy;
            opts.JsonSerializerOptions.DictionaryKeyPolicy = _defaultOptions.DictionaryKeyPolicy;
            opts.JsonSerializerOptions.WriteIndented = _defaultOptions.WriteIndented;
        });
        services.AddTransient<ApplicationServiceConvention>();
        services.AddSingleton<IConfigureOptions<MvcOptions>, ConfigureMvcConvention>();
        
        // Register the NATS proxy action filter
        services.AddScoped<NatsProxyActionFilter>();

        if (options.UseExceptionHandler)
        {
            services.AddScoped<ErrorOrResultFilter>();
        }

        return services;
    }

    private static IServiceCollection AddServiceFrameworkSwagger(this IServiceCollection services, bool useErrorExceptionHandler, Action<SwaggerGenOptions>? setupAction = null, bool autoRegisterSwaggerDoc = false)
    {
        services.AddEndpointsApiExplorer();

        // Disable auto-registration to prevent conflicts with ABP or other frameworks
        // Let the host application handle SwaggerDoc registration
        DefaultSwaggerGenOptions.SetDefault(useErrorExceptionHandler, autoRegisterSwaggerDoc: autoRegisterSwaggerDoc);
        services.AddSwaggerGen(setupAction ?? DefaultSwaggerGenOptions.Default.Configure);

        return services;
    }

    /// <summary>
    /// Adds ServiceFramework Swagger configuration with custom SwaggerDoc registration control.
    /// Use this when you need to control whether to register the default "v1" SwaggerDoc.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="useErrorExceptionHandler">Whether to enable exception handler</param>
    /// <param name="autoRegisterSwaggerDoc">Whether to automatically register the default "v1" SwaggerDoc</param>
    /// <param name="setupAction">Optional custom swagger configuration</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddServiceFrameworkSwagger(this IServiceCollection services, 
        bool useErrorExceptionHandler = true, 
        bool autoRegisterSwaggerDoc = false, 
        Action<SwaggerGenOptions>? setupAction = null)
    {
        services.AddEndpointsApiExplorer();

        DefaultSwaggerGenOptions.SetDefault(useErrorExceptionHandler, autoRegisterSwaggerDoc);
        services.AddSwaggerGen(setupAction ?? DefaultSwaggerGenOptions.Default.Configure);

        return services;
    }

    /// <summary>
    /// Adds ServiceFramework Swagger features to existing Swagger configuration.
    /// Use this when the host application (like ABP) already handles SwaggerDoc registration.
    /// This only adds EdgeSync-specific features like OperationFilters and SchemaFilters.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="useErrorExceptionHandler">Whether to enable exception handler</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddServiceFrameworkSwaggerFeatures(this IServiceCollection services, 
        bool useErrorExceptionHandler = true)
    {
        // Configure SwaggerGen to add only EdgeSync-specific features
        services.ConfigureSwaggerGen(options =>
        {
            // Add nats information including subject, service name, method name in swagger description. 
            options.OperationFilter<AutoGeneratedApiOperationFilter>();

            if (useErrorExceptionHandler)
            {
                // set schema unwrap if enabled useExceptionHandler to match ErrorOrResultFilter
                options.SchemaFilter<ErrorOrSchemaFilter>();
            }

            // Include XML comments
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var xmlFile = $"{assembly.GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }
            }
        });

        return services;
    }

    private static IServiceCollection AddNatsServiceAutoDiscovery(this IServiceCollection services, List<AutoConventionSetting> settings)
    {
        foreach (var setting in settings)
        {
            var assembly = setting.Assembly;
            setting.TypePredicate ??= type => type.IsClass && !type.IsAbstract && typeof(NatsService).IsAssignableFrom(type);
            var natsServiceTypes = assembly.GetTypes()
                .Where(setting.TypePredicate)
                .ToList();

            foreach (var serviceType in natsServiceTypes)
            {
                var interfaces = ServiceTypeHelper.GetServiceInterfaces(serviceType);

                if (interfaces.Any())
                {
                    foreach (var serviceInterface in interfaces)
                    {
                        services.AddScoped(serviceInterface, serviceType);
                    }
                    
                }
                services.AddScoped(serviceType);
            }
        }

        return services;
    }

    private class ConfigureMvcConvention(ApplicationServiceConvention convention) : IConfigureOptions<MvcOptions>
    {
        public void Configure(MvcOptions options)
        {
            options.Conventions.Add(convention);
        }
    }

    private static List<AutoConventionSetting> GetDefaultAssemblies() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !IsSystemAssembly(assembly))
            .Select(assembly => new AutoConventionSetting { Assembly = assembly })
            .ToList();
    

    private static bool IsSystemAssembly(Assembly assembly)
    {
        if (assembly.IsDynamic) return true;

        var name = assembly.FullName ?? "";
        var location = assembly.Location;

        if (name.StartsWith("Microsoft.") ||
            name.StartsWith("System.") ||
            name.StartsWith("netstandard") ||
            name.StartsWith("mscorlib") ||
            name.StartsWith("Serilog"))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(location) && location.Contains("Microsoft.NET"))
        {
            return true;
        }

        if (name.StartsWith("Newtonsoft.") ||
            name.StartsWith("Swashbuckle.") ||
            name.StartsWith("AutoMapper."))
        {
            return true;
        }

        if (name.StartsWith("EdgeSync.ServiceFramework") ||
            name.StartsWith("AutoMapper") ||
            name.StartsWith("NATS"))
        {
            return true;
        }

        return false;
    }


    private static IServiceCollection AddSubscriptionHandlers(this IServiceCollection services)
    {
        // Register message type resolver
        services.AddSingleton<IMessageTypeResolver, MessageTypeResolver>();
        
        // Register all subscription handlers
        services.AddSingleton<ISubscriptionHandler, RequestResponseSubscriptionHandler>();
        services.AddSingleton<ISubscriptionHandler, PubSubPushClassicSubscriptionHandler>();
        services.AddSingleton<ISubscriptionHandler, PubSubPushJetStreamSubscriptionHandler>();
        services.AddSingleton<ISubscriptionHandler, PubSubPullJetStreamSubscriptionHandler>();

        // Register the factory
        services.AddSingleton<ISubscriptionHandlerFactory, SubscriptionHandlerFactory>();

        return services;
    }

    private static IServiceCollection AddServiceFrameworkComponents(this IServiceCollection services)
    {
        // Register the core service framework components (Singletons for stateless services)
        services.AddSingleton<IServiceDiscovery, ServiceDiscovery>();
        services.AddSingleton<IServiceRegistrar, ServiceRegistrar>();
        services.AddSingleton<IMethodInfoBuilder, MethodInfoBuilder>();
        services.AddSingleton<IPubSubManager, PubSubManager>();
        services.AddSingleton<IConnectionResolver, ConnectionResolver>();
        services.AddSingleton<IChannelResolver, ChannelResolver>();

        // Register refactored action filter components (Scoped for per-request state)
        services.AddScoped<IAuditHandler, AuditHandler>();
        services.AddScoped<IRequestDataExtractor, RequestDataExtractor>();
        services.AddScoped<IResponseProcessor, ResponseProcessor>();

        // Register Convention Mode Handlers (Scoped for per-request state)
        services.AddScoped<IConventionModeHandler, NatsRequestResponseHandler>();
        services.AddScoped<IConventionModeHandler, PubSubHandler>();
        
        // Register the factory (Scoped to match handler dependencies)
        services.AddScoped<IConventionModeHandlerFactory, ConventionModeHandlerFactory>();

        return services;
    }
}