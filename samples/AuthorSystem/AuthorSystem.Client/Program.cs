using AuthorSystem.Client.Clients;
using EdgeSync.ServiceFramework.Abstractions;
using EdgeSync.ServiceFramework.Core;
using EdgeSync.ServiceFramework.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Serializers.Json;

namespace AuthorSystem.Client;

public class Program
{
    public static async Task Main(string[] args)
    {
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
        // add solutions
        services.AddKeyedTransient<ISampleService, SimpleSampleService>("simple");
        services.AddKeyedTransient<ISampleService, BusSampleService>("bus");

        var sp = services.BuildServiceProvider();
        var logger = sp.GetRequiredService<ILogger<Program>>();

        try
        {
            var bus = sp.GetRequiredKeyedService<ISampleService>(args.Length == 0 ? "bus" : args[0].ToLower());

            while (true)
            {
                logger.LogInformation("Enter command (e.g., Square(5), Prime(7)) or 'exit' to quit: ");
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                if (input.ToLower() == "exit")
                    break;

                if (!Validate(input, out string subject, out int number))
                {
                    logger.LogWarning("Invalid input format. Use: MethodName(number)");
                    continue;
                }

                try
                {
                    switch (subject.ToLower())
                    {
                        case "square":
                            var squareResult = await bus.Square(number);
                            logger.LogInformation($"Square({number}) = {squareResult}");
                            break;

                        case "sqrt":
                            var sqrtResult = await bus.Sqrt(number);
                            logger.LogInformation($"Sqrt({number}) = {sqrtResult}");
                            break;

                        case "double":
                            var doubleResult = await bus.Double(number);
                            logger.LogInformation($"Double({number}) = {doubleResult}");
                            break;

                        case "prime":
                            var primeResult = await bus.Prime(number);
                            logger.LogInformation($"Prime({number}) = {primeResult}");
                            break;

                        default:
                            logger.LogWarning($"Unknown method: {subject}. Available methods: Square, Sqrt, Double, Prime");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Error executing {subject}({number})");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred");
        }
    }

    private static bool Validate(string input, out string subject, out int number)
    {
        int left = -1;
        int right = -1;

        subject = string.Empty;
        number = 0;

        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '(')
            {
                if (left >= 0) return false;
                left = i;
            }
            else if (input[i] == ')')
            {
                if (right >= 0) return false;
                right = i;
            }
        }

        if (left == -1 || right == -1) return false;
        else if (left >= right) return false;

        var parts = input.Split('(');
        subject = parts[0];
        var numberString = parts[1].Split(')')[0];

        return int.TryParse(numberString, out number);
    }
}