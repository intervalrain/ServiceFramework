using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using Microsoft.Extensions.Logging;

using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Subscriptions;

/// <summary>
/// Subscription handler for Request/Response mode
/// </summary>
public class RequestResponseSubscriptionHandler : BaseSubscriptionHandler
{
    public RequestResponseSubscriptionHandler(
        IServiceProvider serviceProvider,
        ILogger<RequestResponseSubscriptionHandler> logger) 
        : base(serviceProvider, logger)
    {
    }

    public override ConventionMode SupportedMode => ConventionMode.RequestResponse;

    public override async Task SubscribeAsync(INatsConnection connection, Type serviceType, NatsMethodInfo methodInfo, CancellationToken cancellationToken)
    {
        var subscription = await connection.SubscribeCoreAsync<string>(methodInfo.SubjectName, cancellationToken: cancellationToken);
        
        Logger.LogInformation("RequestResponse subscription created for subject: {Subject}", methodInfo.SubjectName);

        // Process messages with response handling
        await foreach (var msg in subscription.Msgs.ReadAllAsync(cancellationToken))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleRequestResponseMessage(serviceType, methodInfo, msg, connection);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error processing request/response message for subject: {Subject}", methodInfo.SubjectName);
                    // Send error response
                    var errorResponse = new NatsResponse<object>
                    {
                        IsSuccess = false,
                        Data = null,
                        Error = ex.Message
                    };
                    await msg.ReplyAsync(errorResponse, serializer: connection.Opts.SerializerRegistry.GetSerializer<NatsResponse<object>>());
                }
            }, cancellationToken);
        }
    }
}