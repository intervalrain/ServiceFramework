using AuthorSystem.Application.Mappings;
using AuthorSystem.Application.Services;
using AuthorSystem.Domain.Repositories;
using AuthorSystem.Infrastructure.Repositories;

using EdgeSync.ServiceFramework.AspNetCore.Mvc;
using EdgeSync.ServiceFramework.Core.Serialization;
using EdgeSync.ServiceFramework.DependencyInjection;

using NATS.Client.Core;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddAutoConvention(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = serviceName,
        Version = "v1",
        Description = "Author management system API"
    });
});

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

app.Run();