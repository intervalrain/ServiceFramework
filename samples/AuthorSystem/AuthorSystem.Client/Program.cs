using System.Text.Json;
using EdgeSync.ServiceFramework.Abstractions;
using EdgeSync.ServiceFramework.Abstractions.JetStream;
using EdgeSync.ServiceFramework.Core;
using EdgeSync.ServiceFramework.Data;
using EdgeSync.ServiceFramework.Data.Json;
using EdgeSync.ServiceFramework.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Serializers.Json;

var services = new ServiceCollection();

// Setup logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

services.AddServiceFramework(options =>
{
    options.DefaultConnection = "bus";
    options.AddConnection("bus", "nats://localhost:4223")
        .WithSerializerRegistry(NatsJsonSerializerRegistry.Default);
});

services.AddSingleton<INatsConnectionFactory, NatsConnectionFactory>();

var sp = services.BuildServiceProvider();
var logger = sp.GetRequiredService<ILogger<Program>>();

try
{
    var factory = sp.GetRequiredService<IJetStreamClientFactory>();

    // Create a JetStream client
    var bus = factory.CreateClient("bus");

    logger.LogInformation("Connecting to NATS JetStream...");
    await bus.TryConnectAsync();

    if (bus.IsConnected())
    {
        logger.LogInformation("Successfully connected to NATS JetStream!");

        // Example: Send a request w/o request dto.
        var subject = "sqrt";
        var input = new { number = 36};

        logger.LogInformation("Sending request to subject: {Subject}", subject);

        try
        {
            // Send request and wait for response
            var response = await bus.RequestAsync<object>(subject, input);

            if (response != null)
            {
                logger.LogInformation("Received response: {Response}", response);

                // Optionally deserialize the response
                var jsonOptions = new JsonSerializerOptions();
                jsonOptions.ConfigureForServiceFramework();

                logger.LogInformation($"Deserialized response: {response}");
            }
            else
            {
                logger.LogWarning("No response received");
            }
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "Request timed out");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending request");
        }
    }
    else
    {
        logger.LogError("Failed to connect to NATS JetStream");
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "An error occurred");
}