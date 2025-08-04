using EmailSystem.Application.Mappings;
using EmailSystem.Application.Services;
using EmailSystem.Domain.Repositories;
using EmailSystem.Infrastructure.Repositories;
using EmailSystem.Application.Contracts.Services;
using EdgeSync.ServiceFramework.Core.Serialization;
using EdgeSync.ServiceFramework.Extensions;
using EdgeSync.ServiceFramework.DependencyInjection;

using NATS.Client.Serializers.Json;
using EdgeSync.ServiceFramework;

var builder = WebApplication.CreateBuilder(args);

var serviceName = "EmailSystem API v1";

builder.Services.AddServiceFramework(options =>
{
    options.DefaultConnection = "bus";
    options.AddConnection("bus", Environment.GetEnvironmentVariable("MSG_BUS_URL") ?? "nats://localhost:4223")
        .WithCredFile(Environment.GetEnvironmentVariable("MSG_BUS_CREDFILE") ?? string.Empty)
        .WithSerializerRegistry(NatsJsonSerializerRegistry.Default);

    options.AddConnection("broker", Environment.GetEnvironmentVariable("MSG_BROKER_URL") ?? "nats://localhost:4222")
        .WithCredFile(Environment.GetEnvironmentVariable("MSG_BROKER_CREDFILE") ?? string.Empty)
        .WithSerializerRegistry(NatsProtobufSerializerRegistry.Default);
});
builder.Services.AddAutoConvention(options =>
{
    // Register SwaggerDoc for standalone usage (not conflicting with ABP)
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "EmailSystem API",
        Version = "v1",
        Description = "Email management service API"
    });
});

builder.Services.AddSingleton<IEmailRepository, InMemoryEmailRepository>();
builder.Services.AddScoped<IEmailAppService, EmailAppService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddAutoMapper(typeof(EmailMappingProfile));

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

app.Run();