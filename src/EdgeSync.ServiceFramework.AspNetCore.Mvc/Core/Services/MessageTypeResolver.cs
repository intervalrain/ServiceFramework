using System.Reflection;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Abstractions;
using EdgeSync.ServiceFramework.Data;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.Services;

/// <summary>
/// Resolves message types from method signatures for proper serialization
/// </summary>
public class MessageTypeResolver : IMessageTypeResolver
{
    public Type ResolveMessageType(MethodInfo method, bool enableAuditWrapper)
    {
        var parameters = method.GetParameters();
        
        // Handle parameterless methods
        if (parameters.Length == 0)
        {
            return enableAuditWrapper ? typeof(RequestDto<object>) : typeof(object);
        }
        
        // For methods with parameters, use the first parameter as the message type
        var firstParam = parameters[0];
        var paramType = firstParam.ParameterType;
        
        // Handle special cases
        if (paramType == typeof(Guid))
        {
            // For Guid parameters, we expect a RequestDto with Id field
            return enableAuditWrapper ? typeof(RequestDto<object>) : typeof(object);
        }
        
        // For complex types, wrap in RequestDto if audit wrapper is enabled
        if (enableAuditWrapper && paramType.IsClass && paramType != typeof(string))
        {
            return typeof(RequestDto<>).MakeGenericType(paramType);
        }
        
        return paramType;
    }
    
    public Type ResolveResponseType(MethodInfo method, bool enableAuditWrapper)
    {
        var returnType = method.ReturnType;
        
        // Handle Task<T> return types
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var taskResultType = returnType.GetGenericArguments()[0];
            
            // Handle ErrorOr<T> pattern
            if (taskResultType.IsGenericType && taskResultType.Name.StartsWith("ErrorOr"))
            {
                var errorOrResultType = taskResultType.GetGenericArguments()[0];
                return enableAuditWrapper ? typeof(ResponseDto<>).MakeGenericType(errorOrResultType) : errorOrResultType;
            }
            
            return enableAuditWrapper ? typeof(ResponseDto<>).MakeGenericType(taskResultType) : taskResultType;
        }
        
        // Handle Task (void) return types
        if (returnType == typeof(Task))
        {
            return enableAuditWrapper ? typeof(ResponseDto<object>) : typeof(object);
        }
        
        // Handle synchronous return types
        if (returnType != typeof(void))
        {
            return enableAuditWrapper ? typeof(ResponseDto<>).MakeGenericType(returnType) : returnType;
        }
        
        return enableAuditWrapper ? typeof(ResponseDto<object>) : typeof(object);
    }
    
    public bool IsCollectionParameter(MethodInfo method)
    {
        var parameters = method.GetParameters();
        return parameters.Any(p => 
            p.ParameterType != typeof(string) && 
            typeof(System.Collections.IEnumerable).IsAssignableFrom(p.ParameterType));
    }
}