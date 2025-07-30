using System.Reflection;
using System.Text.Json;

using EdgeSync.ServiceFramework.Abstractions;
using EdgeSync.ServiceFramework.Abstractions.Attributes;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Data;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    protected readonly AutoConventionOptions Options;
    protected readonly ServiceFrameworkOptions ServiceFrameworkOptions;
    protected readonly IMessageTypeResolver MessageTypeResolver;

    protected BaseSubscriptionHandler(IServiceProvider serviceProvider, ILogger logger)
    {
        ServiceProvider = serviceProvider;
        Logger = logger;
        var optionsAccessor = serviceProvider.GetRequiredService<IOptions<AutoConventionOptions>>();
        Options = optionsAccessor.Value;
        var serviceFrameworkOptionsAccessor = serviceProvider.GetRequiredService<IOptions<ServiceFrameworkOptions>>();
        ServiceFrameworkOptions = serviceFrameworkOptionsAccessor.Value;
        MessageTypeResolver = serviceProvider.GetRequiredService<IMessageTypeResolver>();
    }

    public abstract ConventionMode SupportedMode { get; }

    public abstract Task SubscribeAsync(INatsConnection connection, Type serviceType, NatsMethodInfo methodInfo, CancellationToken cancellationToken);

    /// <summary>
    /// Helper method to invoke generic subscription methods with resolved types
    /// </summary>
    protected async Task InvokeGenericSubscription(
        string methodName,
        INatsConnection connection, 
        Type serviceType, 
        NatsMethodInfo methodInfo, 
        CancellationToken cancellationToken)
    {
        var messageType = MessageTypeResolver.ResolveMessageType(methodInfo.Method, Options.EnableAuditWrapper);
        
        Logger.LogDebug("Resolved message type: {MessageType} for method: {Method}", 
            messageType.Name, methodInfo.Method.Name);
        
        var subscribeMethod = GetType()
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.MakeGenericMethod(messageType);
            
        if (subscribeMethod == null)
        {
            throw new InvalidOperationException($"Method {methodName} not found in {GetType().Name}");
        }
        
        await (Task)subscribeMethod.Invoke(this, new object[] { 
            connection, serviceType, methodInfo, cancellationToken 
        });
    }

    /// <summary>
    /// Generic message handler for request-response pattern
    /// </summary>
    protected async Task HandleRequestResponseMessage<T>(Type serviceType, NatsMethodInfo methodInfo, NatsMsg<T> msg, INatsConnection connection) where T : class
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
            var args = await DeserializeMethodParametersAsync<T>(parameters, msg.Data, serviceType, methodInfo);

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

                    // Handle response with proper serialization
                    if (taskResult != null)
                    {
                        var response = CreateNatsResponse(taskResult);
                        var responseType = MessageTypeResolver.ResolveResponseType(methodInfo.Method, Options.EnableAuditWrapper);
                        
                        // Use the correct serializer for the response type
                        await msg.ReplyAsync(response);
                    }
                    else
                    {
                        // No result, send success response
                        var successResponse = Options.EnableAuditWrapper 
                            ? ResponseDto<object>.Success(new object(), Guid.NewGuid())
                            : new NatsResponse<object> { IsSuccess = true, Data = null, Error = null };
                        
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

    /// <summary>
    /// Legacy string-based message handler (deprecated)
    /// </summary>
    protected async Task HandleRequestResponseMessage(Type serviceType, NatsMethodInfo methodInfo, NatsMsg<string> msg, INatsConnection connection)
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
            var args = await DeserializeMethodParametersAsync(parameters, msg.Data, serviceType, methodInfo);

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
                        var response = (NatsResponse<object>)CreateNatsResponse(taskResult);
                        await msg.ReplyAsync(response, serializer: connection.Opts.SerializerRegistry.GetSerializer<NatsResponse<object>>());
                    }
                    else
                    {
                        // No result, send success response
                        var successResponse = new NatsResponse<object>
                        {
                            IsSuccess = true,
                            Data = null,
                            Error = null
                        };
                        await msg.ReplyAsync(successResponse, serializer: connection.Opts.SerializerRegistry.GetSerializer<NatsResponse<object>>());
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

    /// <summary>
    /// Generic message handler for JetStream pattern
    /// </summary>
    protected async Task HandleJetStreamMessage<T>(Type serviceType, NatsMethodInfo methodInfo, NatsJSMsg<T> msg, INatsConnection connection) where T : class
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
            var args = await DeserializeMethodParametersAsync<T>(parameters, msg.Data, serviceType, methodInfo);

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

    /// <summary>
    /// Legacy string-based JetStream message handler (deprecated)
    /// </summary>
    protected async Task HandleJetStreamMessage(Type serviceType, NatsMethodInfo methodInfo, NatsJSMsg<string> msg, INatsConnection connection)
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
            var args = await DeserializeMethodParametersAsync(parameters, msg.Data, serviceType, methodInfo);

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

    /// <summary>
    /// Generic message handler for Classic pub-sub pattern
    /// </summary>
    protected async Task HandleClassicMessage<T>(Type serviceType, NatsMethodInfo methodInfo, NatsMsg<T> msg, INatsConnection connection) where T : class
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
            var args = await DeserializeMethodParametersAsync<T>(parameters, msg.Data, serviceType, methodInfo);

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

    /// <summary>
    /// Legacy string-based Classic message handler (deprecated)
    /// </summary>
    protected async Task HandleClassicMessage(Type serviceType, NatsMethodInfo methodInfo, NatsMsg<string> msg, INatsConnection connection)
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
            var args = await DeserializeMethodParametersAsync(parameters, msg.Data, serviceType, methodInfo);

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

    /// <summary>
    /// Generic method parameter deserialization with proper typing
    /// </summary>
    private async Task<object[]> DeserializeMethodParametersAsync<T>(ParameterInfo[] parameters, T? messageData, Type serviceType, NatsMethodInfo methodInfo) where T : class
    {
        var args = new object[parameters.Length];

        // If no parameters, return empty array
        if (parameters.Length == 0)
        {
            return args;
        }

        // For parameterless methods or null data
        if (messageData == null)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                args[i] = paramType.IsValueType ? Activator.CreateInstance(paramType)! : null!;
            }
            return args;
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            
            // Handle direct type match
            if (paramType.IsAssignableFrom(typeof(T)))
            {
                args[i] = messageData;
                continue;
            }

            // Handle RequestDto unwrapping
            if (Options.EnableAuditWrapper && typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(RequestDto<>))
            {
                var requestDto = messageData as dynamic;
                if (requestDto?.Data != null)
                {
                    args[i] = requestDto.Data;
                    continue;
                }
            }

            // Handle Guid extraction from object data (for backward compatibility)
            if (paramType == typeof(Guid))
            {
                try
                {
                    if (messageData is string jsonString)
                    {
                        var request = JsonSerializer.Deserialize<JsonElement>(jsonString);
                        if (request.TryGetProperty("Id", out var idProp))
                        {
                            args[i] = Guid.Parse(idProp.GetString()!);
                            continue;
                        }
                    }
                    else if (messageData.GetType().GetProperty("Id") is var idProperty && idProperty != null)
                    {
                        var idValue = idProperty.GetValue(messageData);
                        if (idValue is Guid guid)
                        {
                            args[i] = guid;
                            continue;
                        }
                        else if (idValue is string guidString && Guid.TryParse(guidString, out var parsedGuid))
                        {
                            args[i] = parsedGuid;
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to extract Guid from message data for parameter {ParameterName}", parameters[i].Name);
                }
                
                args[i] = Guid.Empty;
                continue;
            }

            // Default assignment (try direct cast or create default instance)
            try
            {
                if (paramType.IsAssignableFrom(messageData.GetType()))
                {
                    args[i] = messageData;
                }
                else
                {
                    args[i] = paramType.IsValueType ? Activator.CreateInstance(paramType)! : null!;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to assign parameter {ParameterName} of type {ParameterType}", 
                    parameters[i].Name, paramType.Name);
                args[i] = paramType.IsValueType ? Activator.CreateInstance(paramType)! : null!;
            }
        }

        return await Task.FromResult(args);
    }

    /// <summary>
    /// Legacy string-based parameter deserialization (deprecated)
    /// </summary>
    private async Task<object[]> DeserializeMethodParametersAsync(ParameterInfo[] parameters, string? messageData, Type serviceType, NatsMethodInfo methodInfo)
    {
        var args = new object[parameters.Length];
        var serializerRegistry = GetSerializerForChannel(serviceType, methodInfo);

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
                // Deserialize complex objects using the appropriate serializer
                try
                {
                    // Get the deserializer for this type
                    var deserializerMethod = serializerRegistry.GetType().GetMethod("GetDeserializer")!.MakeGenericMethod(paramType);
                    var deserializer = deserializerMethod.Invoke(serializerRegistry, null);
                    
                    // Deserialize the data
                    var bytes = System.Text.Encoding.UTF8.GetBytes(messageData ?? "{}");
                    var buffer = new System.Buffers.ReadOnlySequence<byte>(bytes);
                    
                    var deserializeMethod = deserializer!.GetType().GetMethod("Deserialize", new[] { typeof(System.Buffers.ReadOnlySequence<byte>) });
                    var deserializedData = deserializeMethod!.Invoke(deserializer, new object[] { buffer });
                    
                    // Check if EnableAuditWrapper is on and the deserialized data is RequestDto<T>
                    if (Options.EnableAuditWrapper && deserializedData != null)
                    {
                        var dataType = deserializedData.GetType();
                        if (dataType.IsGenericType && dataType.GetGenericTypeDefinition() == typeof(RequestDto<>))
                        {
                            // Extract the actual data from RequestDto
                            var dataProp = dataType.GetProperty("Data");
                            args[i] = dataProp?.GetValue(deserializedData) ?? deserializedData;
                        }
                        else
                        {
                            args[i] = deserializedData;
                        }
                    }
                    else
                    {
                        args[i] = deserializedData!;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to deserialize parameter of type {ParamType}", paramType.Name);
                    args[i] = Activator.CreateInstance(paramType)!;
                }
            }
        }

        return await Task.FromResult(args);
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

    private INatsSerializerRegistry GetSerializerForChannel(Type serviceType, NatsMethodInfo methodInfo)
    {
        var channelName = GetChannelName(serviceType, methodInfo);
        
        // Find the connection settings for this channel
        if (!string.IsNullOrEmpty(channelName) && ServiceFrameworkOptions.Connections.TryGetValue(channelName, out var connectionSettings))
        {
            return connectionSettings.NatsSerializerRegistry;
        }
        
        // Try default connection if specified
        if (!string.IsNullOrEmpty(ServiceFrameworkOptions.DefaultConnection) &&
            ServiceFrameworkOptions.Connections.TryGetValue(ServiceFrameworkOptions.DefaultConnection, out var defaultSettings))
        {
            return defaultSettings.NatsSerializerRegistry;
        }
        
        // Fallback to default serializer registry
        return ServiceFrameworkOptions.DefaultSerializerRegistry;
    }
    
    private string GetChannelName(Type serviceType, NatsMethodInfo methodInfo)
    {
        // Priority: method > class > default connection

        // 1. Check method-level Channel attribute
        var methodChannel = methodInfo.Method.GetCustomAttribute<ChannelAttribute>();
        if (methodChannel != null)
        {
            return methodChannel.Name;
        }

        // 2. Check class-level Channel attribute
        var classChannel = serviceType.GetCustomAttribute<ChannelAttribute>();
        if (classChannel != null)
        {
            return classChannel.Name;
        }

        // 3. Use default connection
        return ServiceFrameworkOptions.DefaultConnection ?? string.Empty;
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