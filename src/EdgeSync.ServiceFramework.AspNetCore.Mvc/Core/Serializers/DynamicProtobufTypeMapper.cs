using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Google.Protobuf;
using System.Collections;
using EdgeSync.ServiceFramework.Abstractions.Protos;
using EdgeSync.ServiceFramework.Abstractions.Serialization;

namespace EdgeSync.ServiceFramework.Core.Serializers;

/// <summary>
/// Advanced type mapper that can dynamically generate Protobuf-compatible types
/// for complex .NET types including ErrorOr&lt;T&gt;, collections, and nested generics
/// </summary>
public class DynamicProtobufTypeMapper
{
    private readonly ILogger<DynamicProtobufTypeMapper> _logger;
    private readonly ConcurrentDictionary<string, Type?> _typeCache = new();
    private readonly ConcurrentDictionary<Type, Func<object, IMessage>?> _converterCache = new();

    public DynamicProtobufTypeMapper(ILogger<DynamicProtobufTypeMapper> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Maps a .NET type to its Protobuf equivalent, creating dynamic converters if needed
    /// </summary>
    /// <param name="dotnetType">The .NET type to map</param>
    /// <returns>The mapping result containing the Protobuf type and converter</returns>
    public ProtobufTypeMapping GetProtobufMapping(Type dotnetType)
    {
        if (dotnetType == null)
        {
            return ProtobufTypeMapping.Unsupported;
        }

        var cacheKey = GetTypeCacheKey(dotnetType);
        
        _logger.LogDebug("DynamicProtobufTypeMapper: Mapping type {TypeName}", dotnetType.FullName);

        // Check if it's already a Protobuf type
        if (IsProtobufType(dotnetType))
        {
            return new ProtobufTypeMapping(dotnetType, dotnetType, obj => (IMessage)obj);
        }

        // Handle ErrorOr<T> types
        if (IsErrorOrType(dotnetType))
        {
            return MapErrorOrType(dotnetType);
        }

        // Handle collection types
        if (IsCollectionType(dotnetType))
        {
            return MapCollectionType(dotnetType);
        }

        // Handle nullable types
        if (IsNullableType(dotnetType))
        {
            return MapNullableType(dotnetType);
        }

        // Handle complex objects - use UniversalMessage as wrapper
        return new ProtobufTypeMapping(
            dotnetType, 
            typeof(UniversalMessage), 
            obj => UniversalProtobufConverter.ToUniversalMessage(obj));
    }

    /// <summary>
    /// Creates a converter function that can transform .NET objects to Protobuf messages
    /// </summary>
    /// <param name="sourceType">The source .NET type</param>
    /// <param name="targetType">The target Protobuf type</param>
    /// <returns>A converter function</returns>
    public Func<object, IMessage>? CreateConverter(Type sourceType, Type targetType)
    {
        return _converterCache.GetOrAdd(sourceType, type => CreateConverterInternal(type, targetType));
    }

    /// <summary>
    /// Checks if the mapper can handle the specified type
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the type can be mapped</returns>
    public bool CanMapType(Type type)
    {
        if (type == null) return false;

        // Already protobuf types are directly supported
        if (IsProtobufType(type)) return true;

        // ErrorOr types can be mapped
        if (IsErrorOrType(type)) return true;

        // Collections can be mapped
        if (IsCollectionType(type)) return true;

        // Nullable types can be mapped
        if (IsNullableType(type)) return true;

        // Most serializable types can be wrapped in UniversalMessage
        return IsSerializableType(type);
    }

    #region Private Helper Methods

    private ProtobufTypeMapping MapErrorOrType(Type errorOrType)
    {
        _logger.LogDebug("DynamicProtobufTypeMapper: Mapping ErrorOr<{InnerType}>", GetInnerTypeName(errorOrType));

        var innerType = errorOrType.GetGenericArguments()[0];
        
        // For ErrorOr<T>, we'll use ErrorOrResult from protobuf
        return new ProtobufTypeMapping(
            errorOrType,
            typeof(ErrorOrResult),
            obj => ConvertErrorOrToProtobuf(obj, errorOrType));
    }

    private ProtobufTypeMapping MapCollectionType(Type collectionType)
    {
        _logger.LogDebug("DynamicProtobufTypeMapper: Mapping collection type {CollectionType}", collectionType.Name);

        var elementType = GetCollectionElementType(collectionType);
        
        // For collections, we can use CollectionResult or specific typed collections
        if (IsPrimitiveType(elementType))
        {
            return MapPrimitiveCollectionType(collectionType, elementType);
        }

        // For complex element types, use CollectionResult
        return new ProtobufTypeMapping(
            collectionType,
            typeof(CollectionResult),
            obj => ConvertCollectionToProtobuf(obj, collectionType));
    }

    private ProtobufTypeMapping MapNullableType(Type nullableType)
    {
        var underlyingType = Nullable.GetUnderlyingType(nullableType);
        _logger.LogDebug("DynamicProtobufTypeMapper: Mapping nullable type {NullableType} with underlying {UnderlyingType}", 
            nullableType.Name, underlyingType?.Name);

        return new ProtobufTypeMapping(
            nullableType,
            typeof(NullableResult),
            obj => ConvertNullableToProtobuf(obj, nullableType));
    }

    private ProtobufTypeMapping MapPrimitiveCollectionType(Type collectionType, Type elementType)
    {
        if (elementType == typeof(string))
        {
            return new ProtobufTypeMapping(collectionType, typeof(StringCollection), ConvertStringCollection);
        }
        if (elementType == typeof(int) || elementType == typeof(int?))
        {
            return new ProtobufTypeMapping(collectionType, typeof(Int32Collection), ConvertInt32Collection);
        }
        if (elementType == typeof(long) || elementType == typeof(long?))
        {
            return new ProtobufTypeMapping(collectionType, typeof(Int64Collection), ConvertInt64Collection);
        }
        if (elementType == typeof(double) || elementType == typeof(double?))
        {
            return new ProtobufTypeMapping(collectionType, typeof(DoubleCollection), ConvertDoubleCollection);
        }
        if (elementType == typeof(bool) || elementType == typeof(bool?))
        {
            return new ProtobufTypeMapping(collectionType, typeof(BoolCollection), ConvertBoolCollection);
        }

        // Fallback to generic collection
        return new ProtobufTypeMapping(
            collectionType,
            typeof(CollectionResult),
            obj => ConvertCollectionToProtobuf(obj, collectionType));
    }

    private Func<object, IMessage>? CreateConverterInternal(Type sourceType, Type targetType)
    {
        if (IsProtobufType(targetType))
        {
            // Create a converter that transforms sourceType to targetType
            return obj =>
            {
                try
                {
                    // Use UniversalMessage as intermediate format
                    var universalMessage = UniversalProtobufConverter.ToUniversalMessage(obj);
                    var result = UniversalProtobufConverter.FromUniversalMessage(universalMessage, targetType);
                    return (IMessage)result!;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to convert {SourceType} to {TargetType}", sourceType.Name, targetType.Name);
                    throw;
                }
            };
        }

        return null;
    }

    #region Type Checking Methods

    private bool IsErrorOrType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition().Name.Contains("ErrorOr");
    }

    private bool IsCollectionType(Type type)
    {
        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }

    private bool IsNullableType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    private bool IsProtobufType(Type type)
    {
        return type.GetInterfaces().Any(i => 
            i.FullName == "Google.Protobuf.IMessage" || 
            (i.Name == "IMessage" && i.Namespace?.Contains("Protobuf") == true));
    }

    private bool IsSerializableType(Type type)
    {
        if (typeof(Delegate).IsAssignableFrom(type)) return false;
        
        if (type.IsGenericType)
        {
            var genericDefinition = type.GetGenericTypeDefinition();
            if (genericDefinition == typeof(Task<>) || genericDefinition == typeof(ValueTask<>))
                return false;
        }

        return true;
    }

    private bool IsPrimitiveType(Type type)
    {
        return type.IsPrimitive || 
               type == typeof(string) || 
               type == typeof(decimal) || 
               type == typeof(DateTime) || 
               type == typeof(Guid) ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) && 
                IsPrimitiveType(Nullable.GetUnderlyingType(type)!));
    }

    #endregion

    #region Conversion Methods

    private IMessage ConvertErrorOrToProtobuf(object errorOr, Type errorOrType)
    {
        // Delegate to UniversalProtobufConverter which already handles ErrorOr
        var universal = UniversalProtobufConverter.ToUniversalMessage(errorOr);
        return universal.ErrorOrResult ?? new ErrorOrResult();
    }

    private IMessage ConvertCollectionToProtobuf(object collection, Type collectionType)
    {
        var universal = UniversalProtobufConverter.ToUniversalMessage(collection);
        return universal.Collection ?? new CollectionResult();
    }

    private IMessage ConvertNullableToProtobuf(object nullable, Type nullableType)
    {
        var universal = UniversalProtobufConverter.ToUniversalMessage(nullable);
        return universal.Nullable ?? new NullableResult { IsNull = true };
    }

    private IMessage ConvertStringCollection(object obj)
    {
        var collection = new StringCollection();
        if (obj is IEnumerable<string> strings)
        {
            collection.Items.AddRange(strings);
        }
        return collection;
    }

    private IMessage ConvertInt32Collection(object obj)
    {
        var collection = new Int32Collection();
        if (obj is IEnumerable<int> ints)
        {
            collection.Items.AddRange(ints);
        }
        return collection;
    }

    private IMessage ConvertInt64Collection(object obj)
    {
        var collection = new Int64Collection();
        if (obj is IEnumerable<long> longs)
        {
            collection.Items.AddRange(longs);
        }
        return collection;
    }

    private IMessage ConvertDoubleCollection(object obj)
    {
        var collection = new DoubleCollection();
        if (obj is IEnumerable<double> doubles)
        {
            collection.Items.AddRange(doubles);
        }
        return collection;
    }

    private IMessage ConvertBoolCollection(object obj)
    {
        var collection = new BoolCollection();
        if (obj is IEnumerable<bool> bools)
        {
            collection.Items.AddRange(bools);
        }
        return collection;
    }

    #endregion

    #region Utility Methods

    private Type? GetCollectionElementType(Type collectionType)
    {
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType();
        }

        if (collectionType.IsGenericType)
        {
            return collectionType.GetGenericArguments()[0];
        }

        return typeof(object);
    }

    private string GetTypeCacheKey(Type type)
    {
        return type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
    }

    private string GetInnerTypeName(Type type)
    {
        if (type.IsGenericType && type.GetGenericArguments().Length > 0)
        {
            return type.GetGenericArguments()[0].Name;
        }
        return "Unknown";
    }

    #endregion
}

/// <summary>
/// Represents the result of mapping a .NET type to its Protobuf equivalent
/// </summary>
public record ProtobufTypeMapping
{
    public static readonly ProtobufTypeMapping Unsupported = new(null, null, null);

    /// <summary>
    /// The original .NET type
    /// </summary>
    public Type? SourceType { get; }

    /// <summary>
    /// The corresponding Protobuf type
    /// </summary>
    public Type? TargetType { get; }

    /// <summary>
    /// Function to convert from .NET object to Protobuf message
    /// </summary>
    public Func<object, IMessage>? Converter { get; }

    /// <summary>
    /// Whether this mapping is supported
    /// </summary>
    public bool IsSupported => SourceType != null && TargetType != null && Converter != null;

    public ProtobufTypeMapping(Type? sourceType, Type? targetType, Func<object, IMessage>? converter)
    {
        SourceType = sourceType;
        TargetType = targetType;
        Converter = converter;
    }
}
#endregion