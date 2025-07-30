using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using AuthorSystem.Application.Mappings;
using AuthorSystem.Application.Services;
using AuthorSystem.Domain.Repositories;
using AuthorSystem.Infrastructure.Repositories;

using EdgeSync.ServiceFramework;

using EdgeSync.ServiceFramework.Data.Json;
using EdgeSync.ServiceFramework.DependencyInjection;

using Serilog;
using Serilog.Enrichers.CallerInfo;
using Serilog.Events;

namespace AuthorSystem.Host;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Get assembly name prefix for enrichment
        var assemblyNamePrefix = "AuthorSystem";
        var assembly = Assembly.GetEntryAssembly();
        if (assembly != null)
        {
            var assemblyName = assembly.GetName().Name;
            if (assemblyName != null)
            {
                assemblyNamePrefix = assemblyName.Split('.').FirstOrDefault() ?? "AuthorSystem";
            }
        }

        // Create bootstrap logger
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Async(c => c.Console())
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting {ApplicationName}", assemblyNamePrefix);

            var builder = WebApplication.CreateBuilder(args);

            // Configure Serilog
            builder.Host.UseSerilog((context, services, loggerConfiguration) =>
            {
                loggerConfiguration
#if DEBUG
                    .MinimumLevel.Debug()
#else
                    .MinimumLevel.Information()
#endif
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("System", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .Enrich.WithCallerInfo(
                        includeFileInfo: true,
                        assemblyPrefix: assemblyNamePrefix
                    )
                    .Enrich.WithProperty("AppName", assemblyNamePrefix)
                    .ReadFrom.Configuration(context.Configuration);
            });

            var serviceName = "AuthorSystem API v1";

            builder.Services.AddServiceFramework(builder.Configuration);
            // builder.Services.AddServiceFramework(options =>
            // {
            //     options.DefaultConnection = "bus";
            //     options.AddConnection("bus", Environment.GetEnvironmentVariable("MSG_BUS_URL") ?? "nats://localhost:4223")
            //         .WithCredFile(Environment.GetEnvironmentVariable("MSG_BUS_CREDFILE") ?? string.Empty)
            //         .WithSerializerRegistry(NatsJsonSerializerRegistry.Default);

            //     options.AddConnection("broker", Environment.GetEnvironmentVariable("MSG_BROKER_URL") ?? "nats://localhost:4222")
            //         .WithCredFile(Environment.GetEnvironmentVariable("MSG_BROKER_CREDFILE") ?? string.Empty)
            //         .WithSerializerRegistry(NatsProtobufSerializerRegistry.Default);
            // });
            builder.Services.AddAutoConvention();

            builder.Services.AddSingleton<IAuthorRepository, InMemoryAuthorRepository>();
            builder.Services.AddScoped<IAuthorAppService, AuthorAppService>();
            builder.Services.AddScoped<ISampleAppService, SampleAppService>();

            builder.Services.AddAutoMapper(typeof(AuthorMappingProfile));

            // Configure JSON serialization with ServiceFramework standards
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.ConfigureForServiceFramework();
                options.SerializerOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver();
            });

            builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
            {
                options.JsonSerializerOptions.ConfigureForServiceFramework();
                // Add type info resolver to handle problematic types
                options.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, new SafeJsonTypeInfoResolver());
            });

            var app = builder.Build();

            // Enable Swagger in all environments for demo purposes
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", serviceName);
                c.RoutePrefix = "";
            });

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            // Add Serilog request logging
            app.UseSerilogRequestLogging();

            Log.Information("{ApplicationName} configured and starting", assemblyNamePrefix);
            await app.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly!");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

/// <summary>
/// Safe JSON type info resolver that handles problematic types like System.Text.Encoding
/// </summary>
public class SafeJsonTypeInfoResolver : IJsonTypeInfoResolver
{
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        // Skip problematic types that contain unserializable properties
        if (IsProblematicType(type))
        {
            return null; // Return null to skip serialization of this type
        }

        // For types that contain problematic properties, create custom type info
        if (ContainsProblematicProperties(type))
        {
            var typeInfo = JsonTypeInfo.CreateJsonTypeInfo(type, options);

            // Filter out problematic properties
            if (typeInfo.Kind == JsonTypeInfoKind.Object)
            {
                var safeProperties = typeInfo.Properties
                    .Where(prop => !IsProblematicPropertyType(prop.PropertyType))
                    .ToList();

                typeInfo.Properties.Clear();
                foreach (var prop in safeProperties)
                {
                    typeInfo.Properties.Add(prop);
                }
            }

            return typeInfo;
        }

        return null; // Let default resolver handle other types
    }

    private static bool IsProblematicType(Type type)
    {
        // Skip System.Text.Encoding and related types
        return type == typeof(System.Text.Encoding) ||
               type.IsSubclassOf(typeof(System.Text.Encoding)) ||
               type.FullName?.StartsWith("System.Text.Encoding") == true ||
               // Skip ReadOnlySpan and related types
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>)) ||
               type.FullName?.Contains("ReadOnlySpan") == true;
    }

    private static bool ContainsProblematicProperties(Type type)
    {
        try
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            return properties.Any(prop => IsProblematicPropertyType(prop.PropertyType));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsProblematicPropertyType(Type propertyType)
    {
        return IsProblematicType(propertyType) ||
               propertyType.Name.Contains("Encoding") ||
               propertyType.Name.Contains("ReadOnlySpan");
    }
}