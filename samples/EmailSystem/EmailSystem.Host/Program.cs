using EmailSystem.Application.Mappings;
using EmailSystem.Application.Services;
using EmailSystem.Domain.Repositories;
using EmailSystem.Infrastructure.Repositories;

using EdgeSync.ServiceFramework.AspNetCore.Mvc;
using EdgeSync.ServiceFramework.Core.Serialization;
using EdgeSync.ServiceFramework.DependencyInjection;

using NATS.Client.Core;
using EmailSystem.Application.Contracts.Services;

var builder = WebApplication.CreateBuilder(args);

var serviceName = "EmailSystem API v1";

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