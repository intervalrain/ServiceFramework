using System.Reflection;

using EdgeSync.ServiceFramework.AspNetCore.Mvc.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Conventions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.RouteBuilders;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.SwaggerGen;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAutoConvention<TAutoConventionRouteBuilder>(this IServiceCollection services, Action<SwaggerGenOptions>? setupAction = null)
        where TAutoConventionRouteBuilder : class, IAutoConventionRouteBuilder
    {
        services.AddAutoConventionOptions(out var options);
        services.AddApplicationServiceConvention<TAutoConventionRouteBuilder>(options);
        services.AddServiceFrameworkSwagger(options.UseExceptionHandler, setupAction);
        services.AddNatsServiceAutoDiscovery(options.Settings);
        services.AddHostedService<ServiceFrameworkBackgroundService>();

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

        options = new AutoConventionOptions();
        options.Settings = GetDefaultAssemblies();

        return services;
    }

    private static IServiceCollection AddApplicationServiceConvention<TAutoConventionRouteBuilder>(this IServiceCollection services, AutoConventionOptions options)
        where TAutoConventionRouteBuilder : class, IAutoConventionRouteBuilder
    {
        services.AddSingleton<IAutoConventionRouteBuilder, TAutoConventionRouteBuilder>();
        services.AddControllers();
        services.AddTransient<ApplicationServiceConvention>();
        services.AddSingleton<IConfigureOptions<MvcOptions>, ConfigureMvcConvention>();

        if (options.UseExceptionHandler)
        {
            services.AddScoped<ErrorOrResultFilter>();
        }

        return services;
    }

    private static IServiceCollection AddServiceFrameworkSwagger(this IServiceCollection services, bool useErrorExceptionHandler, Action<SwaggerGenOptions>? setupAction = null)
    {
        services.AddEndpointsApiExplorer();

        DefaultSwaggerGenOptions.SetDefault(useErrorExceptionHandler);
        services.AddSwaggerGen(setupAction ?? DefaultSwaggerGenOptions.Default.Configure);

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
                var interfaces = serviceType.GetInterfaces()
                    .Where(i => i.Name.StartsWith("I") &&
                               i.Name.EndsWith("Service"))
                    .ToList();

                if (interfaces.Any())
                {
                    foreach (var serviceInterface in interfaces)
                    {
                        services.AddScoped(serviceInterface, serviceType);
                    }
                }
                else
                {
                    services.AddScoped(serviceType);
                }
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
            name.StartsWith("mscorlib"))
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
}