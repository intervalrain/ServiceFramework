# Serializer Adapter Pattern

This directory contains the implementation of the Serializer Adapter pattern, which provides a clean, extensible solution for handling different serialization formats in the NATS service framework.

## Problem Solved

Previously, the codebase used hard-coded if-else logic to handle different serializer types:

```csharp
var isProtobufSerializer = IsProtobufSerializer(serializerRegistry);
if (isProtobufSerializer)
{
    // Protobuf-specific handling with EmptyMessage
}
else
{
    // JSON/default handling with object types
}
```

This approach had several issues:
- **Not extensible**: Adding new serializers required modifying existing code
- **Scattered logic**: Serializer-specific handling was spread across multiple methods
- **Violation of Open/Closed Principle**: Code wasn't open for extension but closed for modification

## Solution: Adapter Pattern

The Adapter pattern provides a unified interface that abstracts serializer-specific operations:

```csharp
public interface ISerializerAdapter
{
    SerializerType SerializerType { get; }
    bool CanAdapt(INatsSerializerRegistry serializerRegistry);
    Task<object?> SendRequestAsync(/* parameters */);
    Task PublishAsync(/* parameters */);
    Type? GetEmptyMessageType();
    bool IsTypeCompatible(Type type);
    SerializerEndpointConfig GetEndpointConfig(INatsSerializerRegistry serializerRegistry);
}
```

## Architecture Components

### 1. Core Interface (`ISerializerAdapter`)
Defines the contract for all serializer adapters.

### 2. Built-in Adapters
- **`JsonSerializerAdapter`**: Handles JSON serialization (flexible with object types)
- **`ProtobufSerializerAdapter`**: Handles Protobuf serialization (requires EmptyMessage for parameterless operations)

### 3. Factory (`SerializerAdapterFactory`)
Manages adapter instances and automatically selects the appropriate adapter for a given serializer registry.

### 4. Service Registration
Extension methods for easy DI container registration.

## Usage

### Basic Setup

The adapters are automatically registered when you use the framework:

```csharp
services.AddAutoConvention(); // This includes AddSerializerAdapters()
```

### Adding Custom Adapters

#### Option 1: Register Individual Adapter
```csharp
services.AddSerializerAdapter<MessagePackSerializerAdapter>();
```

#### Option 2: Fluent Configuration
```csharp
services.AddSerializerAdapters(builder =>
{
    builder.AddAdapter<MessagePackSerializerAdapter>()
           .AddAdapter<AvroSerializerAdapter>()
           .AddAdapter(new CustomSerializerAdapter());
});
```

#### Option 3: Runtime Registration
```csharp
serviceProvider.ConfigureSerializerAdapters(factory =>
{
    factory.RegisterAdapter<MessagePackSerializerAdapter>();
});
```

## Creating Custom Adapters

### Step 1: Implement the Interface

```csharp
public class MyCustomSerializerAdapter : ISerializerAdapter
{
    public SerializerType SerializerType => SerializerType.Custom;
    
    public bool CanAdapt(INatsSerializerRegistry serializerRegistry)
    {
        // Check if this adapter can handle the serializer
        return serializerRegistry.GetType().Name.Contains("MyCustomSerializer");
    }
    
    public async Task<object?> SendRequestAsync(
        INatsConnection connection, 
        string subject, 
        object? request, 
        bool isOriginallyParameterless, 
        INatsSerializerRegistry serializerRegistry)
    {
        // Implement custom serializer-specific request handling
        // Handle parameterless methods, audit wrappers, etc.
    }
    
    // Implement other interface methods...
}
```

### Step 2: Register the Adapter

```csharp
services.AddSerializerAdapter<MyCustomSerializerAdapter>();
```

### Step 3: Configure Your NATS Connection

```csharp
services.Configure<ServiceFrameworkOptions>(options =>
{
    options.Connections["custom"] = new ConnectionSettings
    {
        NatsSerializerRegistry = new MyCustomSerializerRegistry()
    };
});
```

## Serializer-Specific Considerations

### JSON Serializers
- **Flexibility**: Can handle most object types gracefully
- **Null handling**: Supports null values for parameterless operations
- **Audit wrappers**: Fully compatible with `RequestDto<T>` and `ResponseDto<T>`

### Protobuf Serializers
- **Type constraints**: Requires specific message types
- **Empty messages**: Uses `EmptyMessage` for parameterless operations
- **Audit wrapper limitations**: Not compatible with generic `RequestDto<T>` types
- **Error handling**: Throws descriptive errors for incompatible scenarios

### Custom Serializers
- **Flexibility varies**: Depends on the serializer's capabilities
- **Configuration needed**: May require custom setup for complex types
- **Testing important**: Ensure compatibility with your specific use cases

## Benefits

1. **Extensibility**: Easy to add new serializer support without modifying existing code
2. **Separation of Concerns**: Each adapter handles only its specific serializer requirements
3. **Maintainability**: Serializer-specific logic is contained and easy to update
4. **Testability**: Each adapter can be tested independently
5. **SOLID Principles**: Follows Open/Closed and Single Responsibility principles

## Example: Adding MessagePack Support

```csharp
// 1. Create the adapter (see Examples/MessagePackSerializerAdapter.cs)
public class MessagePackSerializerAdapter : ISerializerAdapter { /* implementation */ }

// 2. Register in your startup
services.AddSerializerAdapter<MessagePackSerializerAdapter>();

// 3. Configure NATS to use MessagePack
services.Configure<ServiceFrameworkOptions>(options =>
{
    options.Connections["messagepack"] = new ConnectionSettings
    {
        NatsSerializerRegistry = new MessagePackSerializerRegistry()
    };
});

// 4. Use in your service
[Channel("messagepack")]
public class MyService : NatsService
{
    [Subject("my.subject")]
    public async Task<string> GetDataAsync() => "Hello MessagePack!";
}
```

The framework will automatically use the MessagePack adapter for this service!

## Migration from Old Approach

Old code like this:
```csharp
var isProtobufSerializer = IsProtobufSerializer(serializerRegistry);
if (isProtobufSerializer)
{
    // Protobuf handling
}
else
{
    // JSON handling
}
```

Is now replaced with:
```csharp
var adapter = _serializerAdapterFactory.GetAdapter(serializerRegistry);
var response = await adapter.SendRequestAsync(/* parameters */);
```

The adapter automatically handles all serializer-specific requirements!