using System.Reflection;

using AuthorSystem.Application.Mappings;
using AuthorSystem.Application.Services;
using AuthorSystem.Domain.Repositories;
using AuthorSystem.Infrastructure.Repositories;

using EdgeSync.ServiceFramework.AspNetCore.Mvc;
using EdgeSync.ServiceFramework.Core.Serialization;
using EdgeSync.ServiceFramework.DependencyInjection;

using NATS.Client.Core;

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

            builder.Services.AddServiceFramework(options =>
            {
                options.DefaultConnection = "bus";
                options.AddConnection("bus", Environment.GetEnvironmentVariable("MSG_BUS_URL") ?? "nats://localhost:4223")
                    .WithCredFile(Environment.GetEnvironmentVariable("MSG_BUS_CREDFILE") ?? string.Empty)
                    .WithSerializerRegistry(NatsDefaultSerializerRegistry.Default);

                options.AddConnection("broker", Environment.GetEnvironmentVariable("MSG_BROKER_URL") ?? "nats://localhost:4222")
                    .WithCredFile(Environment.GetEnvironmentVariable("MSG_BROKER_CREDFILE") ?? string.Empty)
                    .WithSerializerRegistry(NatsProtobufSerializerRegistry.Default);
            });
            builder.Services.AddAutoConvention();

            builder.Services.AddSingleton<IAuthorRepository, InMemoryAuthorRepository>();
            builder.Services.AddScoped<IAuthorAppService, AuthorAppService>();

            builder.Services.AddAutoMapper(typeof(AuthorMappingProfile));

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