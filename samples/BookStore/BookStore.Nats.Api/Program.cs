using BookStore.Application.Mappings;
using BookStore.Application.Services;
using BookStore.Domain.Repositories;
using BookStore.Infrastructure.Repositories;

using EdgeSync.ServiceFramework.Core.Serialization;
using EdgeSync.ServiceFramework.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NATS.Client.Serializers.Json;
using EdgeSync.ServiceFramework.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddServiceFramework(static options =>
{
    options.DefaultConnection = "bus";
    options.AddConnection("bus", Environment.GetEnvironmentVariable("MSG_BUS_URL") ?? "nats://localhost:4223")
        .WithCredFile(Environment.GetEnvironmentVariable("MSG_BUS_CREDFILE") ?? string.Empty)
        .WithSerializerRegistry(NatsJsonSerializerRegistry.Default);

    options.AddConnection("broker", Environment.GetEnvironmentVariable("MSG_BROKER_URL") ?? "nats://localhost:4222")
        .WithCredFile(Environment.GetEnvironmentVariable("MSG_BROKER_CREDFILE") ?? string.Empty)
        .WithSerializerRegistry(NatsProtobufSerializerRegistry.Default);
});

builder.Services.AddServiceHandlersFromAssembly<Program>();

// Register Event Handlers

builder.Services.AddSingleton<IBookRepository, InMemoryBookRepository>();
builder.Services.AddScoped<IBookAppService, BookAppService>();

builder.Services.AddAutoMapper(typeof(BookMappingProfile));

var host = builder.Build();

await host.RunAsync();