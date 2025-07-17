using BookStore.Nats.Client.Services;

using EdgeSync.ServiceFramework.Abstractions;
using EdgeSync.ServiceFramework.DependencyInjection;
using EdgeSync.ServiceFramework.Extensions;

using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Option 1: Configuration from appsettings.json (recommended)
builder.Services.AddServiceFramework(builder.Configuration, ServiceFrameworkOptions.SectionName);

// Option 2: Programmatic configuration (alternative)
/*
builder.Services.AddServiceFramework(options =>
{
    options.DefaultConnection = "bus";
    options.AddConnection("bus", Environment.GetEnvironmentVariable("MSG_BUS_URL") ?? "nats://localhost:4223")
        .WithCredFile(Environment.GetEnvironmentVariable("MSG_BUS_CREDFILE") ?? string.Empty)
        .WithSerializerRegistry("json"); // 支援字串參數

    options.AddConnection("broker", Environment.GetEnvironmentVariable("MSG_BROKER_URL") ?? "nats://localhost:4222")
        .WithCredFile(Environment.GetEnvironmentVariable("MSG_BROKER_CREDFILE") ?? string.Empty)
        .WithSerializerRegistry("protobuf"); // 支援字串參數
});
*/

builder.Services.AddScoped<IBookNatsClient, BookNatsClient>();
builder.Services.AddHealthChecks()
    // .AddCheck<NatsBrokerConnectionHealthCheck>("broker", tags: [""])
    .AddNatsCheck();

var app = builder.Build();

// Enable Swagger in all environments for demo purposes
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "BookStore API V1");
    c.RoutePrefix = "";
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/api/health-status", async (IServiceProvider serviceProvider) =>
{
    var healthCheckService = serviceProvider.GetRequiredService<HealthCheckService>();
    var healthReport = await healthCheckService.CheckHealthAsync();
    
    var healthResponse = new
    {
        status = healthReport.Status.ToString(),
        totalDuration = healthReport.TotalDuration.ToString(),
        entries = healthReport.Entries.ToDictionary(
            kvp => kvp.Key,
            kvp => new
            {
                status = kvp.Value.Status.ToString(),
                description = kvp.Value.Description,
                duration = kvp.Value.Duration.ToString(),
                data = kvp.Value.Data
            })
    };

    return healthReport.Status == HealthStatus.Healthy 
        ? Results.Ok(healthResponse) 
        : Results.Json(healthResponse, statusCode: 503);
});

app.Run();