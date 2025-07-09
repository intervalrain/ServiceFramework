using System.Text.Json;

using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NATS.Client.Core;
using NATS.Client.JetStream;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Subscriptions;

/// <summary>
/// Base class for subscription handlers with common functionality
/// </summary>
public abstract class BaseSubscriptionHandler : ISubscriptionHandler
{
    protected readonly IServiceProvider ServiceProvider;
    protected readonly ILogger Logger;

    protected BaseSubscriptionHandler(IServiceProvider serviceProvider, ILogger logger)
    {
        ServiceProvider = serviceProvider;
        Logger = logger;
    }

    public abstract ConventionMode SupportedMode { get; }

    public abstract Task SubscribeAsync(INatsConnection connection, Type serviceType, NatsMethodInfo methodInfo, CancellationToken cancellationToken);

    protected async Task HandleRequestResponseMessage(Type serviceType, NatsMethodInfo methodInfo, NatsMsg<string> msg)
    {
        using var scope = ServiceProvider.CreateScope();

        try
        {
            var service = GetServiceInstance(scope.ServiceProvider, serviceType);
            if (service == null)
            {
                Logger.LogError("Could not resolve service: {ServiceType}", serviceType.Name);
                return;
            }

            var parameters = methodInfo.Method.GetParameters();
            var args = DeserializeMethodParameters(parameters, msg.Data);

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

                    // Handle any result type for request/response
                    if (taskResult != null)
                    {
                        var response = CreateNatsResponse(taskResult);
                        var jsonResponse = JsonSerializer.Serialize(response);
                        await msg.ReplyAsync(jsonResponse);
                    }
                    else
                    {
                        // No result, send success response
                        var successResponse = JsonSerializer.Serialize(new NatsResponse<object>
                        {
                            IsSuccess = true,
                            Data = null,
                            Error = null
                        });
                        await msg.ReplyAsync(successResponse);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling request/response message for subject: {Subject}", methodInfo.SubjectName);
            throw; // Re-throw to be handled by caller
        }
    }

    protected async Task HandleJetStreamMessage(Type serviceType, NatsMethodInfo methodInfo, NatsJSMsg<string> msg)
    {
        using var scope = ServiceProvider.CreateScope();

        try
        {
            var service = GetServiceInstance(scope.ServiceProvider, serviceType);
            if (service == null)
            {
                Logger.LogError("Could not resolve service: {ServiceType}", serviceType.Name);
                return;
            }

            var parameters = methodInfo.Method.GetParameters();
            var args = DeserializeMethodParameters(parameters, msg.Data);

            // Invoke the method
            var result = methodInfo.Method.Invoke(service, args);

            if (result is Task task)
            {
                await task;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling JetStream message for subject: {Subject}", methodInfo.SubjectName);
            throw; // Re-throw to trigger NAK
        }
    }

    protected async Task HandleClassicMessage(Type serviceType, NatsMethodInfo methodInfo, NatsMsg<string> msg)
    {
        using var scope = ServiceProvider.CreateScope();

        try
        {
            var service = GetServiceInstance(scope.ServiceProvider, serviceType);
            if (service == null)
            {
                Logger.LogError("Could not resolve service: {ServiceType}", serviceType.Name);
                return;
            }

            var parameters = methodInfo.Method.GetParameters();
            var args = DeserializeMethodParameters(parameters, msg.Data);

            // Invoke the method
            var result = methodInfo.Method.Invoke(service, args);

            if (result is Task task)
            {
                await task;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling classic message for subject: {Subject}", methodInfo.SubjectName);
            // Classic mode doesn't have acknowledgment, just log the error
        }
    }

    private object[] DeserializeMethodParameters(System.Reflection.ParameterInfo[] parameters, string? messageData)
    {
        var args = new object[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;

            if (paramType == typeof(Guid))
            {
                // Extract Guid from message data
                try
                {
                    var request = JsonSerializer.Deserialize<JsonElement>(messageData ?? "{}");
                    if (request.TryGetProperty("Id", out var idProp))
                    {
                        args[i] = Guid.Parse(idProp.GetString()!);
                    }
                }
                catch
                {
                    args[i] = Guid.Empty;
                }
            }
            else if (paramType.IsClass && paramType != typeof(string))
            {
                // Deserialize complex objects
                try
                {
                    args[i] = JsonSerializer.Deserialize(messageData ?? "{}", paramType)!;
                }
                catch
                {
                    args[i] = Activator.CreateInstance(paramType)!;
                }
            }
        }

        return args;
    }

    private object? GetServiceInstance(IServiceProvider serviceProvider, Type serviceType)
    {
        // First, try to get the service by its registered interfaces
        var interfaces = ServiceTypeHelper.GetServiceInterfaces(serviceType);

        Logger.LogDebug("Found {Count} interfaces for service type {ServiceType}: {Interfaces}", 
            interfaces.Count, serviceType.Name, string.Join(", ", interfaces.Select(i => i.Name)));

        foreach (var serviceInterface in interfaces)
        {
            var service = serviceProvider.GetService(serviceInterface);
            if (service != null)
            {
                Logger.LogDebug("Successfully resolved service through interface {Interface}", serviceInterface.Name);
                return service;
            }
            else
            {
                Logger.LogDebug("Could not resolve service through interface {Interface}", serviceInterface.Name);
            }
        }

        // Fallback to the concrete type if no interface is found
        Logger.LogDebug("Attempting to resolve service directly by concrete type {ServiceType}", serviceType.Name);
        var concreteService = serviceProvider.GetService(serviceType);
        if (concreteService != null)
        {
            Logger.LogDebug("Successfully resolved service through concrete type {ServiceType}", serviceType.Name);
        }
        else
        {
            Logger.LogDebug("Could not resolve service through concrete type {ServiceType}", serviceType.Name);
        }
        
        return concreteService;
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
                var errors = errorsProperty.GetValue(errorOrResult) as IEnumerable<ErrorOr.Error>;
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
}