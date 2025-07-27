using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.Core.Serializers;

/// <summary>
/// Maps .NET types to their Protobuf equivalents, especially for complex generic types like ErrorOr<T>
/// </summary>
public class ProtobufTypeMapper
{
    private readonly ILogger<ProtobufTypeMapper> _logger;
    private readonly ConcurrentDictionary<Type, Type?> _typeMapping = new();

    public ProtobufTypeMapper(ILogger<ProtobufTypeMapper> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Attempts to find a Protobuf equivalent for the given .NET type
    /// </summary>
    /// <param name="dotnetType">The .NET type to map</param>
    /// <returns>The Protobuf type if found, null otherwise</returns>
    public Type? GetProtobufType(Type dotnetType)
    {
        return _typeMapping.GetOrAdd(dotnetType, MapTypeInternal);
    }

    private Type? MapTypeInternal(Type dotnetType)
    {
        if (dotnetType == null)
        {
            return null;
        }

        _logger.LogDebug("ProtobufTypeMapper: Attempting to map type {TypeName}", dotnetType.Name);

        // Handle ErrorOr<T> types
        if (dotnetType.IsGenericType && dotnetType.GetGenericTypeDefinition().Name.Contains("ErrorOr"))
        {
            var innerType = dotnetType.GetGenericArguments()[0];
            return MapErrorOrType(innerType);
        }

        // Handle List<T> types
        if (dotnetType.IsGenericType && dotnetType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = dotnetType.GetGenericArguments()[0];
            return MapListType(elementType);
        }

        // Handle direct DTO mappings
        return MapDirectType(dotnetType);
    }

    private Type? MapErrorOrType(Type innerType)
    {
        _logger.LogDebug("ProtobufTypeMapper: Mapping ErrorOr<{InnerType}>", innerType.Name);

        // Try to find corresponding result types in loaded assemblies
        var resultTypeName = GetResultTypeName(innerType);
        if (resultTypeName != null)
        {
            var resultType = FindTypeInLoadedAssemblies(resultTypeName);
            if (resultType != null)
            {
                _logger.LogDebug("ProtobufTypeMapper: Found result type {ResultType} for ErrorOr<{InnerType}>", 
                    resultType.Name, innerType.Name);
                return resultType;
            }
        }

        _logger.LogWarning("ProtobufTypeMapper: No Protobuf result type found for ErrorOr<{InnerType}>", innerType.Name);
        return null;
    }

    private Type? MapListType(Type elementType)
    {
        _logger.LogDebug("ProtobufTypeMapper: Mapping List<{ElementType}>", elementType.Name);

        // Look for corresponding list wrapper types
        var listTypeName = GetListTypeName(elementType);
        if (listTypeName != null)
        {
            var listType = FindTypeInLoadedAssemblies(listTypeName);
            if (listType != null)
            {
                _logger.LogDebug("ProtobufTypeMapper: Found list type {ListType} for List<{ElementType}>", 
                    listType.Name, elementType.Name);
                return listType;
            }
        }

        return null;
    }

    private Type? MapDirectType(Type type)
    {
        // For direct DTO mappings, check if there's a Protobuf version
        // This would typically check if the type has a .proto equivalent
        
        // Check if it's already a Protobuf type
        if (IsProtobufType(type))
        {
            return type;
        }

        return null;
    }

    private string? GetResultTypeName(Type innerType)
    {
        // Map specific types to their result type names
        var typeName = innerType.Name;
        
        if (typeName.Contains("BookDto"))
        {
            return "BookResult";
        }
        
        if (typeName.Contains("List") && typeName.Contains("BookDto"))
        {
            return "BookListResult";
        }
        
        if (typeName.Contains("Deleted"))
        {
            return "DeleteResult";
        }

        // Add more mappings as needed
        return null;
    }

    private string? GetListTypeName(Type elementType)
    {
        var typeName = elementType.Name;
        
        if (typeName.Contains("BookDto"))
        {
            return "BookListDto";
        }

        // Add more mappings as needed
        return null;
    }

    private Type? FindTypeInLoadedAssemblies(string typeName)
    {
        try
        {
            // Search in currently loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var types = assembly.GetTypes();
                    var foundType = types.FirstOrDefault(t => t.Name == typeName);
                    if (foundType != null && IsProtobufType(foundType))
                    {
                        return foundType;
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Skip assemblies that can't be loaded
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ProtobufTypeMapper: Error searching for type {TypeName}", typeName);
        }

        return null;
    }

    private bool IsProtobufType(Type type)
    {
        return type.GetInterfaces().Any(i => 
            i.FullName == "Google.Protobuf.IMessage" || 
            (i.Name == "IMessage" && i.Namespace?.Contains("Protobuf") == true));
    }

    /// <summary>
    /// Checks if a type has a known Protobuf mapping
    /// </summary>
    public bool HasProtobufMapping(Type type)
    {
        return GetProtobufType(type) != null;
    }
}