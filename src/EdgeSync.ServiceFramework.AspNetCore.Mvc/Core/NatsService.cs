using System.Reflection;

using EdgeSync.ServiceFramework.AspNetCore.Mvc.Extensions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Decisions;
using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.Abstractions.Attributes;

using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;

public abstract class NatsService
{
    protected readonly ILogger Logger;
    protected readonly string ServiceName;
    private readonly IConventionDecisionMaker? _decisionMaker;
    private readonly AutoConventionOptions? _options;

    protected NatsService(ILogger logger)
    {
        Logger = logger;
        ServiceName = GetType().Name.ToLower().RemovePostfixes(["natsapplicationservice", "applicationservice", "appservice", "service"]);
    }

    protected NatsService(ILogger logger, IConventionDecisionMaker decisionMaker, AutoConventionOptions options) : this(logger)
    {
        _decisionMaker = decisionMaker;
        _options = options;
    }

    public virtual string GetSubjectPrefix() => ServiceName;

    public virtual IEnumerable<NatsMethodInfo> GetNatsMethods()
    {
        var methods = GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.EndsWith("Async") && m.ReturnType.IsGenericType)
            .Select(method =>
            {
                var subjectAttr = method.GetCustomAttribute<SubjectAttribute>();
                var jetStreamAttr = method.GetCustomAttribute<JetStreamAttribute>();
                var jetStreamPullAttr = method.GetCustomAttribute<JetStreamPullAttribute>();
                
                var natsMethodInfo = new NatsMethodInfo
                {
                    Method = method,
                    SubjectName = subjectAttr?.CustomSubject ?? $"{GetSubjectPrefix()}.{method.Name.ToLower().Replace("async", "")}",
                    ServiceMethod = method,
                    Endpoint = subjectAttr?.GetEndpoint(method.Name),
                    SubjectAttribute = subjectAttr,
                    JetStreamAttribute = jetStreamAttr,
                    JetStreamPullAttribute = jetStreamPullAttr
                };

                // Use IConventionDecisionMaker if available, otherwise fallback to default
                if (_decisionMaker != null && _options != null)
                {
                    var context = ConventionDecisionContextBuilder.Build(method, _options);
                    var decisionResult = _decisionMaker.MakeDecision(context);
                    
                    if (decisionResult.IsError)
                    {
                        throw new InvalidOperationException($"Convention decision error for method {method.Name}: {decisionResult.ErrorMessage}");
                    }
                    
                    natsMethodInfo.ConventionMode = decisionResult.Mode;
                }
                else
                {
                    // Fallback to a simple default when decision maker is not available
                    natsMethodInfo.ConventionMode = DetermineConventionModeFallback(method);
                }

                return natsMethodInfo;
            });

        return methods;
    }

    private ConventionMode DetermineConventionModeFallback(MethodInfo method)
    {
        // Simple fallback logic for when IConventionDecisionMaker is not available
        if (method.ReturnType != typeof(void) && method.ReturnType != typeof(Task))
        {
            return ConventionMode.RequestResponse;
        }
        
        // Default to classic pub/sub for backward compatibility
        return ConventionMode.PubSubPushClassic;
    }
}

public class NatsMethodInfo
{
    public MethodInfo Method { get; set; } = null!;
    public string SubjectName { get; set; } = string.Empty;
    public MethodInfo ServiceMethod { get; set; } = null!;
    public string? Endpoint { get; set; }
    public SubjectAttribute? SubjectAttribute { get; set; }
    public JetStreamAttribute? JetStreamAttribute { get; set; }
    public JetStreamPullAttribute? JetStreamPullAttribute { get; set; }
    public ConventionMode ConventionMode { get; set; }
    
    // Helper properties for easier access
    public bool IsRequestResponse => ConventionMode == ConventionMode.RequestResponse;
    public bool IsJetStreamPush => ConventionMode == ConventionMode.PubSubPushJetStream;
    public bool IsJetStreamPull => ConventionMode == ConventionMode.PubSubPullJetStream;
    public bool IsClassicPubSub => ConventionMode == ConventionMode.PubSubPushClassic;
    public bool IsJetStreamMode => IsJetStreamPush || IsJetStreamPull;
}