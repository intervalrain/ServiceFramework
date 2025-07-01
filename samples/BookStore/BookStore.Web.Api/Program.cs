using BookStore.Nats.Client.Services;

using EdgeSync.ServiceFramework.Core.Serialization;
using EdgeSync.ServiceFramework.DependencyInjection;

using NATS.Client.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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


builder.Services.AddScoped<IBookNatsClient, BookNatsClient>();

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

app.Run();