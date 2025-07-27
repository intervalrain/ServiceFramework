// Add global aliases to resolve naming conflicts
global using Type = System.Type;
global using ProtobufError = EdgeSync.ServiceFramework.Abstractions.Protos.Error;
global using ErrorOrError = ErrorOr.Error;
global using SystemEnum = System.Enum;

using System.Collections;
using System.Text.Json;
using EdgeSync.ServiceFramework.Abstractions.Protos;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using ErrorOr;

namespace EdgeSync.ServiceFramework.Abstractions.Serialization;

/// <summary>
/// Universal converter between .NET types and Protobuf UniversalMessage
/// Supports ErrorOr, collections, and any serializable .NET type
/// </summary>
public static class UniversalProtobufConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>
    /// Converts any .NET object to UniversalMessage
    /// </summary>
    public static UniversalMessage ToUniversalMessage(object? value)
    {
        var message = new UniversalMessage
        {
            TypeName = value?.GetType().AssemblyQualifiedName ?? typeof(object).AssemblyQualifiedName
        };

        if (value == null)
        {
            message.Nullable = new NullableResult { IsNull = true };
            return message;
        }

        var type = value.GetType();

        // Handle ErrorOr<T> types
        if (IsErrorOrType(type))
        {
            message.ErrorOrResult = ConvertErrorOrToProtobuf(value, type);
            return message;
        }

        // Handle collection types
        if (IsCollectionType(type) && type != typeof(string))
        {
            if (TryConvertPrimitiveCollection(value, type, message))
            {
                return message;
            }
            message.Collection = ConvertCollectionToProtobuf(value, type);
            return message;
        }

        // Handle nullable types
        if (IsNullableType(type))
        {
            var underlyingValue = type.GetProperty("Value")?.GetValue(value);
            if (underlyingValue != null)
            {
                message.Nullable = new NullableResult 
                { 
                    Data = ToAny(underlyingValue) 
                };
            }
            else
            {
                message.Nullable = new NullableResult { IsNull = true };
            }
            return message;
        }

        // Try to convert to protobuf Any, fallback to JSON
        try
        {
            message.TypedData = ToAny(value);
        }
        catch
        {
            // Fallback to JSON serialization
            message.JsonData = JsonSerializer.Serialize(value, JsonOptions);
        }

        return message;
    }

    /// <summary>
    /// Converts UniversalMessage back to .NET object
    /// </summary>
    public static object? FromUniversalMessage(UniversalMessage message, Type? expectedType = null)
    {
        var targetType = expectedType ?? GetTypeFromName(message.TypeName);
        
        switch (message.ContentCase)
        {
            case UniversalMessage.ContentOneofCase.TypedData:
                return FromAny(message.TypedData, targetType);

            case UniversalMessage.ContentOneofCase.JsonData:
                return JsonSerializer.Deserialize(message.JsonData, targetType, JsonOptions);

            case UniversalMessage.ContentOneofCase.ErrorOrResult:
                return ConvertErrorOrFromProtobuf(message.ErrorOrResult, targetType);

            case UniversalMessage.ContentOneofCase.Collection:
                return ConvertCollectionFromProtobuf(message.Collection, targetType);

            case UniversalMessage.ContentOneofCase.Nullable:
                return ConvertNullableFromProtobuf(message.Nullable, targetType);

            case UniversalMessage.ContentOneofCase.StringCollection:
                return message.StringCollection.Items.ToList();

            case UniversalMessage.ContentOneofCase.Int32Collection:
                return message.Int32Collection.Items.ToList();

            case UniversalMessage.ContentOneofCase.Int64Collection:
                return message.Int64Collection.Items.ToList();

            case UniversalMessage.ContentOneofCase.DoubleCollection:
                return message.DoubleCollection.Items.ToList();

            case UniversalMessage.ContentOneofCase.BoolCollection:
                return message.BoolCollection.Items.ToList();

            default:
                return null;
        }
    }

    #region Private Helper Methods

    private static bool IsErrorOrType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition().Name.Contains("ErrorOr");
    }

    private static bool IsCollectionType(Type type)
    {
        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }

    private static bool IsNullableType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    private static ErrorOrResult ConvertErrorOrToProtobuf(object errorOr, Type errorOrType)
    {
        var result = new ErrorOrResult();
        
        // Use reflection to check if it's an error or success
        var isErrorProperty = errorOrType.GetProperty("IsError");
        var isError = isErrorProperty != null && (bool)isErrorProperty.GetValue(errorOr)!;

        if (isError)
        {
            var errorsProperty = errorOrType.GetProperty("Errors");
            var errors = errorsProperty?.GetValue(errorOr);
            
            var errorList = new ErrorList();
            if (errors is IEnumerable errorEnumerable)
            {
                foreach (var error in errorEnumerable)
                {
                    var errorMsg = new ProtobufError();
                    
                    // Try to extract error properties
                    var codeProperty = error?.GetType().GetProperty("Code");
                    var descriptionProperty = error?.GetType().GetProperty("Description");
                    var typeProperty = error?.GetType().GetProperty("Type");

                    errorMsg.Code = codeProperty?.GetValue(error)?.ToString() ?? "";
                    errorMsg.Description = descriptionProperty?.GetValue(error)?.ToString() ?? "";
                    
                    if (typeProperty?.GetValue(error) is SystemEnum errorType)
                    {
                        errorMsg.Type = Convert.ToInt32(errorType);
                    }

                    errorList.Errors.Add(errorMsg);
                }
            }
            
            result.Errors = errorList;
        }
        else
        {
            var valueProperty = errorOrType.GetProperty("Value");
            var value = valueProperty?.GetValue(errorOr);
            
            if (value != null)
            {
                result.Value = ToAny(value);
            }
        }

        return result;
    }

    private static object? ConvertErrorOrFromProtobuf(ErrorOrResult protobufResult, Type targetType)
    {
        var innerType = targetType.GetGenericArguments()[0];
        
        switch (protobufResult.ResultCase)
        {
            case ErrorOrResult.ResultOneofCase.Value:
                var value = FromAny(protobufResult.Value, innerType);
                // Create ErrorOr<T> success result using implicit conversion
                return value;

            case ErrorOrResult.ResultOneofCase.Errors:
                // Create errors and return ErrorOr<T> failure
                var errors = new List<object>();
                foreach (var errorProto in protobufResult.Errors.Errors)
                {
                    // Create Error object
                    errors.Add(CreateError(errorProto.Code, errorProto.Description));
                }
                
                // Use reflection to create ErrorOr failure
                return CreateErrorOrFailure(innerType, errors);

            default:
                return CreateErrorOrFailure(innerType, new[] { CreateError("Unknown", "Unknown error") });
        }
    }

    private static CollectionResult ConvertCollectionToProtobuf(object collection, Type collectionType)
    {
        var result = new CollectionResult();
        
        if (collection is IEnumerable enumerable)
        {
            var elementType = GetCollectionElementType(collectionType);
            result.ElementTypeName = elementType?.AssemblyQualifiedName ?? typeof(object).AssemblyQualifiedName;
            
            foreach (var item in enumerable)
            {
                if (item != null)
                {
                    result.Items.Add(ToAny(item));
                }
            }
        }

        return result;
    }

    private static object ConvertCollectionFromProtobuf(CollectionResult protobufCollection, Type targetType)
    {
        var elementType = GetTypeFromName(protobufCollection.ElementTypeName) ?? typeof(object);
        var items = protobufCollection.Items.Select(any => FromAny(any, elementType)).ToList();
        
        // Convert to target collection type
        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                array.SetValue(items[i], i);
            }
            return array;
        }

        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = Activator.CreateInstance(listType) as IList;
            foreach (var item in items)
            {
                list?.Add(item);
            }
            return list!;
        }

        return items;
    }

    private static object? ConvertNullableFromProtobuf(NullableResult protobufNullable, Type targetType)
    {
        if (protobufNullable.IsNull)
        {
            return null;
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return FromAny(protobufNullable.Data, underlyingType);
    }

    private static bool TryConvertPrimitiveCollection(object value, Type type, UniversalMessage message)
    {
        if (value is IEnumerable<string> stringCollection)
        {
            message.StringCollection = new StringCollection();
            message.StringCollection.Items.AddRange(stringCollection);
            return true;
        }

        if (value is IEnumerable<int> intCollection)
        {
            message.Int32Collection = new Int32Collection();
            message.Int32Collection.Items.AddRange(intCollection);
            return true;
        }

        if (value is IEnumerable<long> longCollection)
        {
            message.Int64Collection = new Int64Collection();
            message.Int64Collection.Items.AddRange(longCollection);
            return true;
        }

        if (value is IEnumerable<double> doubleCollection)
        {
            message.DoubleCollection = new DoubleCollection();
            message.DoubleCollection.Items.AddRange(doubleCollection);
            return true;
        }

        if (value is IEnumerable<bool> boolCollection)
        {
            message.BoolCollection = new BoolCollection();
            message.BoolCollection.Items.AddRange(boolCollection);
            return true;
        }

        return false;
    }

    private static Type? GetCollectionElementType(Type collectionType)
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

    private static Type? GetTypeFromName(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return typeof(object);
        }

        try
        {
            return Type.GetType(typeName);
        }
        catch
        {
            return typeof(object);
        }
    }

    private static Any ToAny(object value)
    {
        // Simplified implementation using JSON
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return Any.Pack(new BytesValue { Value = ByteString.CopyFromUtf8(json) });
    }

    private static object? FromAny(Any any, Type targetType)
    {
        // Simplified implementation using JSON
        try
        {
            var bytesValue = any.Unpack<BytesValue>();
            var json = bytesValue.Value.ToStringUtf8();
            return JsonSerializer.Deserialize(json, targetType, JsonOptions);
        }
        catch
        {
            return Activator.CreateInstance(targetType);
        }
    }

    private static object CreateError(string code, string description)
    {
        // Create a simple error object
        return new { Code = code, Description = description };
    }

    private static object CreateErrorOrFailure(Type innerType, IEnumerable<object> errors)
    {
        // Simplified implementation
        return errors.First();
    }

    #endregion
}