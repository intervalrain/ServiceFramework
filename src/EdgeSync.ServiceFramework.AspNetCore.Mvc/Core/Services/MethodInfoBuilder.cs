using System.Reflection;
using EdgeSync.ServiceFramework.Abstractions;
using EdgeSync.ServiceFramework.Abstractions.Attributes;
using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Decisions;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;

/// <summary>
/// Implementation for building NATS method information with convention decisions
/// Extracted from ServiceFrameworkBackgroundService to follow SRP
/// </summary>
public class MethodInfoBuilder : IMethodInfoBuilder
{
    private readonly IConventionDecisionMaker _decisionMaker;
    private readonly AutoConventionOptions _options;
    private readonly ILogger<MethodInfoBuilder> _logger;

    public MethodInfoBuilder(
        IConventionDecisionMaker decisionMaker,
        IOptions<AutoConventionOptions> options,
        ILogger<MethodInfoBuilder> logger)
    {
        _decisionMaker = decisionMaker;
        _options = options.Value;
        _logger = logger;
    }

    public IEnumerable<NatsMethodInfo> GetNatsMethodsWithDecision(NatsService service)
    {
        var serviceType = service.GetType();
        var methods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.EndsWith("Async") && m.ReturnType.IsGenericType);

        var results = new List<NatsMethodInfo>();

        foreach (var method in methods)
        {
            try
            {
                var subjectAttr = method.GetCustomAttribute<SubjectAttribute>();
                var jetStreamAttr = method.GetCustomAttribute<JetStreamAttribute>();
                var jetStreamPullAttr = method.GetCustomAttribute<JetStreamPullAttribute>();

                // Skip methods without SubjectAttribute
                if (subjectAttr?.CustomSubject == null)
                {
                    _logger.LogDebug("Skipping method {MethodName} on service {ServiceType} - no SubjectAttribute", 
                        method.Name, serviceType.Name);
                    continue;
                }

                var natsMethodInfo = new NatsMethodInfo
                {
                    ServiceName = service.ServiceName,
                    Method = method,
                    SubjectName = subjectAttr.CustomSubject,
                    ServiceMethod = method,
                    Endpoint = subjectAttr?.EndpointName ?? method.Name.ToLower().Replace("async", ""),
                    SubjectAttribute = subjectAttr,
                    JetStreamAttribute = jetStreamAttr,
                    JetStreamPullAttribute = jetStreamPullAttr
                };

                // Use IConventionDecisionMaker to determine the mode
                var context = ConventionDecisionContextBuilder.Build(method, _options);
                var decisionResult = _decisionMaker.MakeDecision(context);

                if (decisionResult.IsError)
                {
                    _logger.LogError("Convention decision error for method {MethodName} on service {ServiceType}: {ErrorMessage}",
                        method.Name, serviceType.Name, decisionResult.ErrorMessage);
                    throw new InvalidOperationException($"Convention decision error for method {method.Name}: {decisionResult.ErrorMessage}");
                }

                natsMethodInfo.ConventionMode = decisionResult.Mode;
                results.Add(natsMethodInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing method {MethodName} on service {ServiceType}", 
                    method.Name, serviceType.Name);
                throw;
            }
        }

        return results;
    }
}