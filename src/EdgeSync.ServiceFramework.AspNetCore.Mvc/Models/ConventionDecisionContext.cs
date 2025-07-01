using System.Reflection;
using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.Abstractions.Attributes;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

/// <summary>
/// Context information for convention decision making
/// </summary>
public class ConventionDecisionContext
{
    public required MethodInfo Method { get; set; }
    public required Type ReturnType { get; set; }
    public required AutoConventionOptions Options { get; set; }
    
    /// <summary>
    /// Whether method has return value (not void/Task/ValueTask)
    /// </summary>
    public bool HasReturnValue { get; set; }
    
    /// <summary>
    /// Whether parameter is a collection type
    /// </summary>
    public bool IsParameterCollection { get; set; }
    
    /// <summary>
    /// Whether method has JetStreamSubject attribute with Enable=true
    /// </summary>
    public bool HasJetStreamSubject { get; set; }
    
    /// <summary>
    /// JetStreamAttribute instance if present
    /// </summary>
    public JetStreamAttribute? JetStreamAttribute { get; set; }
    
    /// <summary>
    /// Whether method has JetStreamPullAttribute
    /// </summary>
    public bool HasJetStreamPullAttribute { get; set; }
    
    /// <summary>
    /// JetStreamPullAttribute instance if present
    /// </summary>
    public JetStreamPullAttribute? JetStreamPullAttribute { get; set; }
}