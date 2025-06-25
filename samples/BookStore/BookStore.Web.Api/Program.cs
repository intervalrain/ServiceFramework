using BookStore.Nats.Client.Services;
using EdgeSync.ServiceFramework.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddServiceFramework(options =>
{
    options.DefaultConnection = "bus";
    options.Connections["bus"] = new EdgeSync.ServiceFramework.NatsConnectionSettings
    {
        Name = "bus",
        Url = Environment.GetEnvironmentVariable("MSG_BUS_URL") ?? "nats://localhost:4223"
    };
    options.Connections["broker"] = new EdgeSync.ServiceFramework.NatsConnectionSettings
    {
        Name = "broker", 
        Url = Environment.GetEnvironmentVariable("MSG_BROKER_URL") ?? "nats://localhost:4222"
    };
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