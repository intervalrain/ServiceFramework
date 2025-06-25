# EdgeSync Service Framework

English | [繁體中文](README.zh.md)

[![Version](https://img.shields.io/badge/version-1.1.0-blue.svg)](CHANGELOG.md)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
[![NATS](https://img.shields.io/badge/NATS-JetStream-green.svg)](https://nats.io/)

EdgeSync Service Framework is an enterprise-grade service framework built on NATS JetStream, designed for microservice architectures with powerful serialization support and flexible connection management.

## 🚀 Key Features

- 🔄 **Multi-Connection Support**: Support for multiple named NATS connections instead of fixed Bus/Broker pattern
- 🛠️ **Fluent API**: Intuitive chain-based configuration API for simplified setup
- 📦 **Automatic Serialization**: Support for JSON, Protobuf and other serialization formats with per-connection configuration
- ⚡ **Lazy Loading**: Smart connection management with lazy loading - unused connections won't cause errors
- 🔧 **Backward Compatible**: Full compatibility with existing code for seamless upgrades
- 📊 **Enhanced Logging**: Detailed reconnection and retry logging for monitoring and debugging
- 🎯 **Event-Driven**: Support for both publish/subscribe and request/response patterns

## 📦 Installation

```bash
dotnet add package EdgeSync.ServiceFramework.Abstractions
dotnet add package EdgeSync.ServiceFramework.Core
dotnet add package EdgeSync.ServiceFramework.DependencyInjection
```

## 🔧 Quick Start

### Basic Configuration

```csharp
builder.Services.AddServiceFramework(options =>
{
    options.DefaultConnection = "bus";
    
    // JSON serialization connection (for normal communication)
    options.AddConnection("bus", "nats://localhost:4222")
           .WithSerializerRegistry(NatsDefaultSerializerRegistry.Default);
           
    // Protobuf serialization connection (for high-performance device communication)
    options.AddConnection("broker", "nats://localhost:4223")
           .WithSerializerRegistry(NatsProtobufSerializerRegistry.Default)
           .WithCredFile("/path/to/credentials.creds");
});
```

### Configuration from appsettings.json

```csharp
builder.Services.AddServiceFramework(builder.Configuration);
```

```json
{
  "ServiceFramework": {
    "DefaultConnection": "bus",
    "Connections": {
      "bus": {
        "Url": "nats://localhost:4222",
        "CredFile": "/path/to/creds"
      },
      "broker": {
        "Url": "nats://localhost:4223",
        "CredFile": "/path/to/broker-creds"
      }
    }
  }
}
```

## 🔌 Serialization Support

### Supported Serializers
- **`NatsDefaultSerializerRegistry.Default`**: JSON serialization (default)
- **`NatsProtobufSerializerRegistry.Default`**: Protocol Buffers serialization

### Automatic Serialization API
```csharp
// Publish objects - automatic serialization
await PublishAsync("my.subject", new MyMessage { Id = 1, Name = "Test" });

// Request objects - automatic serialization for both request and response
var response = await _bus.RequestAsync<MyRequest, MyResponse>("my.service", request);

// Use specific connections
await _broker.PublishAsync("device.command", deviceCommand); // Uses Protobuf
await _bus.PublishAsync("web.event", webEvent);             // Uses JSON
```

## 🏗️ Architecture Concepts

### Connection Types and Usage
- **Default**: Configurable default connection, usually pointing to main business logic connection
- **Bus**: For client communication, typically uses JSON serialization
- **Broker**: For device communication, typically uses Protobuf serialization

### Service Types
- **ServiceHandler**: Handles synchronous request/response, suitable for RPC-style services
- **BaseEventHandler**: Handles asynchronous events, suitable for event-driven architecture

## 📖 Usage Examples

### BaseEventHandler
```csharp
public class BookRefillEventHandler : BaseEventHandler
{
    protected override string SubjectName => "bookstore.events.book.refill";
    protected override string StreamName => "BookEvents";
    protected override string ConsumerName => "BookRefillConsumer";

    protected override async Task HandleInputEventCore(byte[] message, string subject)
    {
        // Deserialize message
        var refillEvent = JsonSerializer.Deserialize<BookRefillEvent>(message);
        
        // Process event logic
        await ProcessRefillEvent(refillEvent);
        
        // Publish follow-up event
        await PublishAsync("bookstore.events.inventory.updated", new InventoryUpdatedEvent
        {
            BookId = refillEvent.BookId,
            NewQuantity = refillEvent.RefillQuantity
        });
    }
}
```
#### Advanced Configuration: `JetStreamConfigOptions` and `ConsumerConfigOptions`
+ Overwrite `JStreamCfgOpts` and `ConsumerCfgOpts` in the subclass of BaseEventHandler.
```cs
public class BookRefillEventHandler : BaseEventHandler
{
    protected override JetStreamConfigOptions JStreamCfgOpts { get; set; } = new JetStreamConfigOptions
    {
        MaxMsgs = -1,
        MaxBytes = -1,
        MaxAge = TimeSpan.FromDays(1),
        Description = "Bookstore events stream for book refill events",
        Retention = StreamConfigRetention.Limits,
        Storage = StreamConfigStorage.File,
    };

    protected override ConsumerConfigOptions ConsumerCfgOpts { get; set; } = new ConsumerConfigOptions()
    {
        AckPolicy = ConsumerConfigAckPolicy.Explicit,
        ReplayPolicy = ConsumerConfigReplayPolicy.Instant,
        MaxAckPending = -1,
    };
}
```

### NATS Client (Multi-Connection + Serialization)
```csharp
public class BookNatsClient : IBookNatsClient
{
    private readonly IJetStreamClient _bus;    // JSON serialization
    private readonly IJetStreamClient _broker; // Protobuf serialization

    public BookNatsClient(IJetStreamClientFactory factory)
    {
        _bus = factory.CreateClient("bus");       // For inter-microservices communication
        _broker = factory.CreateClient("broker"); // For device communication
    }

    public async Task<BookDto> GetBookAsync(Guid id)
    {
        // Send JSON request to service
        var response = await _bus.RequestAsync<GetBookRequest, BookDto>(
            "bookstore.api.GetBook", 
            new GetBookRequest { Id = id }
        );
        return response;
    }

    public async Task RefillStockAsync(Guid id, int quantity)
    {
        // Publish Protobuf-formatted device command
        await _broker.PublishAsync("device.inventory.refill", new DeviceRefillCommand
        {
            BookId = id,
            Quantity = quantity
        });
    }
}
```

### ServiceHandler
```csharp
public class BookServiceHandler : ServiceHandler
{
    public override string ServiceName => "BookService";
    public override string ServiceVersion => "1.0.0";
    public override string QueueGroup => "book-service";

    [Subject("GetBook", "bookstore.books.*.get")]
    public async ValueTask GetBook(ServiceMsgContext<GetBookRequest> context, GetBookRequest request)
    {
        var book = await _bookService.GetBookAsync(request.Id);
        await ReplyAsync(context, book); // Auto-serialization response
    }
}
```

## 🔄 Migration Guide

### Upgrading from Old Version
The old `AddNatsApi` method is still supported, but migration to the new `AddServiceFramework` is recommended:

```csharp
// Old way (still works)
services.AddNatsApi(options => 
{
    options.MsgBusUrl = "nats://localhost:4222";
    options.MsgBrokerUrl = "nats://localhost:4223";
});

// New way - same functionality
services.AddServiceFramework(options => 
{
    options.AddConnection("bus", "nats://localhost:4222")
           .WithSerializerRegistry(NatsDefaultSerializerRegistry.Default);
    options.AddConnection("broker", "nats://localhost:4223")
           .WithSerializerRegistry(NatsDefaultSerializerRegistry.Default);
});
```

## 📚 Example Project
Check out the complete example application in the `samples/BookStore` directory, showcasing:
- Multi-tier architecture design (Web API → NATS Client → NATS API)
- Mixed serialization usage (JSON + Protobuf)
- Event-driven and request/response patterns
- Error handling and logging

## 🔧 Build and Test

### Prerequisites
- .NET 9.0 SDK
- NATS Server (for integration tests)

### Build
```bash
dotnet build
```

### Run Tests
```bash
dotnet test
```

### Run BookStore Sample
```bash
cd samples/BookStore
dotnet run --project BookStore.Web.Api
dotnet run --project BookStore.Nats.Api
```

## ⚡ Performance Considerations
- **JSON Serialization**: Suitable for development-friendly, high-readability scenarios
- **Protobuf Serialization**: Suitable for high-frequency communication, low-latency scenarios
- **Connection Reuse**: Connections with identical configurations are automatically reused to reduce resource consumption

## 📄 Version History
See [CHANGELOG.md](CHANGELOG.md) for detailed version update history.

## 📜 License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🔗 Links
- [Documentation](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edgesync_service_framework)
- [Changelog](CHANGELOG.md)

---

Copyright © 2025 Advantech. All rights reserved.