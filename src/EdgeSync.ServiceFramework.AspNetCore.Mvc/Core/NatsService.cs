using System.Reflection;

using EdgeSync.ServiceFramework.AspNetCore.Mvc.Extensions;
using EdgeSync.ServiceFramework.Attributes;

using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;

public abstract class NatsService
{
    protected readonly ILogger Logger;
    protected readonly string ServiceName;

    protected NatsService(ILogger logger)
    {
        Logger = logger;
        ServiceName = GetType().Name.ToLower().RemovePostfixes(["natsapplicationservice", "applicationservice", "appservice", "service"]);
    }

    public virtual string GetSubjectPrefix() => ServiceName;

    public virtual IEnumerable<NatsMethodInfo> GetNatsMethods()
    {
        var methods = GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.EndsWith("Async") && m.ReturnType.IsGenericType)
            .Select(method =>
            {
                var subjectAttr = method.GetCustomAttribute<SubjectAttribute>();
                
                return new NatsMethodInfo
                {
                    Method = method,
                    SubjectName = subjectAttr?.CustomSubject ?? $"{GetSubjectPrefix()}.{method.Name.ToLower().Replace("async", "")}",
                    ServiceMethod = method,
                    Endpoint = subjectAttr?.GetEndpoint(method.Name),
                    SubjectAttribute = subjectAttr
                };
            });

        return methods;
    }
}

public class NatsMethodInfo
{
    public MethodInfo Method { get; set; } = null!;
    public string SubjectName { get; set; } = string.Empty;
    public MethodInfo ServiceMethod { get; set; } = null!;
    public string? Endpoint { get; set; }
    public SubjectAttribute? SubjectAttribute { get; set; }
}