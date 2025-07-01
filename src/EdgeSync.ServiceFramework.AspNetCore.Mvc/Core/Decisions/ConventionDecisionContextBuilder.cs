using System.Reflection;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;
using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.Abstractions.Attributes;
using ErrorOr;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Decisions;

/// <summary>
/// Builder for creating ConventionDecisionContext
/// </summary>
public static class ConventionDecisionContextBuilder
{
    /// <summary>
    /// Build decision context from method information
    /// </summary>
    /// <param name="method">Method to analyze</param>
    /// <param name="options">Auto convention options</param>
    /// <returns>Decision context</returns>
    public static ConventionDecisionContext Build(MethodInfo method, AutoConventionOptions options)
    {
        var returnType = method.ReturnType;
        var hasReturnValue = DetermineHasReturnValue(returnType);
        var isParameterCollection = DetermineIsParameterCollection(method);
        var jetStreamAttribute = method.GetCustomAttribute<JetStreamAttribute>();
        var hasJetStreamSubject = jetStreamAttribute != null && jetStreamAttribute.Enable;
        var jetStreamPullAttribute = method.GetCustomAttribute<JetStreamPullAttribute>();
        var hasJetStreamPullAttribute = jetStreamPullAttribute != null;

        return new ConventionDecisionContext
        {
            Method = method,
            ReturnType = returnType,
            Options = options,
            HasReturnValue = hasReturnValue,
            IsParameterCollection = isParameterCollection,
            HasJetStreamSubject = hasJetStreamSubject,
            JetStreamAttribute = jetStreamAttribute,
            HasJetStreamPullAttribute = hasJetStreamPullAttribute,
            JetStreamPullAttribute = jetStreamPullAttribute
        };
    }

    private static bool DetermineHasReturnValue(Type returnType)
    {
        // Remove ErrorOr wrapper
        var actualType = returnType;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ErrorOr<>))
        {
            actualType = returnType.GetGenericArguments()[0];
        }

        // Check if it's a Task or ValueTask
        if (actualType.IsGenericType)
        {
            var genericDef = actualType.GetGenericTypeDefinition();
            if (genericDef == typeof(Task<>) || genericDef == typeof(ValueTask<>))
            {
                actualType = actualType.GetGenericArguments()[0];
            }
        }

        // Return true if not void, Task, or ValueTask
        return !(actualType == typeof(Task) || actualType == typeof(void) || actualType == typeof(ValueTask));
    }

    private static bool DetermineIsParameterCollection(MethodInfo method)
    {
        var parameters = method.GetParameters();
        
        // Check if any parameter is a collection type
        return parameters.Any(p => IsCollectionType(p.ParameterType));
    }

    private static bool IsCollectionType(Type type)
    {
        // Check if type implements IEnumerable (but not string)
        if (type == typeof(string))
            return false;

        return typeof(System.Collections.IEnumerable).IsAssignableFrom(type) ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)) ||
               type.IsArray;
    }

    private static bool HasJetStreamPullAttribute(MethodInfo method)
    {
        return method.GetCustomAttribute<JetStreamPullAttribute>() != null;
    }
}