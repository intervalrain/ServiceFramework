using System.Reflection;
using System.Text.Json;

using EdgeSync.ServiceFramework.Abstractions.Attributes;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

using ErrorOr;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;

public class ServiceFrameworkBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServiceFrameworkBackgroundService> _logger;
    private readonly List<IAsyncDisposable> _subscriptions = [];

    public ServiceFrameworkBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ServiceFrameworkBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Discover all NatsService implementations
        var serviceTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && !type.IsAbstract &&
                           type.IsSubclassOf(typeof(NatsService)))
            .ToList();

        var subscriptionTasks = new List<Task>();

        foreach (var serviceType in serviceTypes)
        {
            var channelAttribute = serviceType.GetCustomAttribute<ChannelAttribute>();
            var channelName = channelAttribute?.Name;

            using var scope = _serviceProvider.CreateScope();
            INatsConnection connection;
            try
            {
                if (channelName != null)
                {
                    connection = scope.ServiceProvider.GetRequiredKeyedService<INatsConnection>(channelName);
                    _logger.LogInformation("Service {ServiceType} using NATS connection '{ChannelName}'", serviceType.Name, channelName);
                }
                else
                {
                    connection = scope.ServiceProvider.GetRequiredService<INatsConnection>();
                    _logger.LogInformation("Service {ServiceType} using default NATS connection", serviceType.Name);
                }

                await connection.ConnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get NATS connection for service {ServiceType} with channel '{ChannelName}'", serviceType.Name, channelName ?? "default");
                continue;
            }

            var service = scope.ServiceProvider.GetService(serviceType) as NatsService;

            if (service == null && serviceType.IsSubclassOf(typeof(NatsService)))
            {
                var constructors = serviceType.GetConstructors();
                foreach (var constructor in constructors)
                {
                    var parameters = constructor.GetParameters();
                    var args = new object[parameters.Length];
                    var canCreate = true;

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        try
                        {
                            args[i] = scope.ServiceProvider.GetRequiredService(parameters[i].ParameterType);
                        }
                        catch
                        {
                            canCreate = false;
                            break;
                        }
                    }

                    if (canCreate)
                    {
                        service = (NatsService)Activator.CreateInstance(serviceType, args)!;
                        break;
                    }
                }
            }

            if (service != null)
            {
                var natsMethods = service.GetNatsMethods();

                foreach (var natsMethod in natsMethods)
                {
                    var task = Task.Run(async () =>
                    {
                        await SubscribeToMethod(connection, serviceType, natsMethod, stoppingToken);
                    }, stoppingToken);

                    subscriptionTasks.Add(task);
                }
            }
        }

        _logger.LogInformation("All NATS subscriptions started via reflection");
        await Task.WhenAll(subscriptionTasks);
    }

    private async Task SubscribeToMethod(INatsConnection connection, Type serviceType, NatsMethodInfo methodInfo, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Subscribing to subject: {Subject} for method: {Method} on connection {ConnectionId}",
                methodInfo.SubjectName, methodInfo.Method.Name, connection.ServerInfo?.ClientId);

            var subscription = await connection.SubscribeCoreAsync<string>(methodInfo.SubjectName, cancellationToken: cancellationToken);
            _subscriptions.Add(subscription);

            _logger.LogInformation("Successfully subscribed to subject: {Subject} on connection {ConnectionId}", methodInfo.SubjectName, connection.ServerInfo?.ClientId);

            // Process messages
            await foreach (var msg in subscription.Msgs.ReadAllAsync(cancellationToken))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleMessage(serviceType, methodInfo, msg);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message for subject: {Subject}", methodInfo.SubjectName);
                    }
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to subject: {Subject} on connection {ConnectionId}", methodInfo.SubjectName, connection.ServerInfo?.ClientId);
        }
    }

    private async Task HandleMessage(Type serviceType, NatsMethodInfo methodInfo, NatsMsg<string> msg)
    {
        using var scope = _serviceProvider.CreateScope();

        try
        {
            var service = scope.ServiceProvider.GetService(serviceType);
            if (service == null)
            {
                _logger.LogError("Could not resolve service: {ServiceType}", serviceType.Name);
                return;
            }

            var parameters = methodInfo.Method.GetParameters();
            var args = new object[parameters.Length];

            // Handle method parameters based on their types
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;

                if (paramType == typeof(Guid))
                {
                    // Extract Guid from message data - try different formats
                    try
                    {
                        var request = JsonSerializer.Deserialize<JsonElement>(msg.Data ?? "{}");
                        if (request.TryGetProperty("Id", out var idProp))
                        {
                            args[i] = Guid.Parse(idProp.GetString()!);
                        }
                    }
                    catch
                    {
                        // If parsing fails, use empty guid
                        args[i] = Guid.Empty;
                    }
                }
                else if (paramType.IsClass && paramType != typeof(string))
                {
                    // Deserialize complex objects
                    try
                    {
                        args[i] = JsonSerializer.Deserialize(msg.Data ?? "{}", paramType)!;
                    }
                    catch
                    {
                        args[i] = Activator.CreateInstance(paramType)!;
                    }
                }
            }

            // Invoke the method
            var result = methodInfo.Method.Invoke(service, args);

            if (result is Task task)
            {
                await task;

                // Get the result value if it's Task<T>
                if (task.GetType().IsGenericType)
                {
                    var resultProperty = task.GetType().GetProperty("Result");
                    var taskResult = resultProperty?.GetValue(task);

                    // Handle ErrorOr results
                    if (taskResult != null && IsErrorOrType(taskResult.GetType()))
                    {
                        var response = CreateNatsResponse(taskResult);
                        var jsonResponse = JsonSerializer.Serialize(response);
                        await msg.ReplyAsync(jsonResponse);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling NATS message for subject: {Subject}", methodInfo.SubjectName);
            var errorResponse = JsonSerializer.Serialize(new NatsResponse<object>
            {
                IsSuccess = false,
                Data = null,
                Error = ex.Message
            });
            await msg.ReplyAsync(errorResponse);
        }
    }

    private bool IsErrorOrType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ErrorOr<>);
    }

    private object CreateNatsResponse(object errorOrResult)
    {
        var type = errorOrResult.GetType();
        var isErrorProperty = type.GetProperty("IsError");
        var valueProperty = type.GetProperty("Value");
        var errorsProperty = type.GetProperty("Errors");

        if (isErrorProperty != null && valueProperty != null && errorsProperty != null)
        {
            var isError = (bool)isErrorProperty.GetValue(errorOrResult)!;

            if (isError)
            {
                var errors = errorsProperty.GetValue(errorOrResult) as IEnumerable<Error>;
                var firstError = errors?.FirstOrDefault();

                return new NatsResponse<object>
                {
                    IsSuccess = false,
                    Data = null,
                    Error = firstError?.Description ?? "Unknown error"
                };
            }
            else
            {
                var value = valueProperty.GetValue(errorOrResult);
                return new NatsResponse<object>
                {
                    IsSuccess = true,
                    Data = value,
                    Error = null
                };
            }
        }

        return new NatsResponse<object>
        {
            IsSuccess = false,
            Data = null,
            Error = "Invalid result type"
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping NATS reflection background service");

        foreach (var subscription in _subscriptions)
        {
            try
            {
                await subscription.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing subscription");
            }
        }

        await base.StopAsync(cancellationToken);
    }
}