# EdgeSync Service Framework

[English](README.md) | 繁體中文

[![Version](https://img.shields.io/badge/version-1.1.0-blue.svg)](CHANGELOG.zh.md)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
[![NATS](https://img.shields.io/badge/NATS-JetStream-green.svg)](https://nats.io/)

EdgeSync Service Framework 是基於 NATS JetStream 的企業級服務框架，專為微服務架構設計，提供強大的序列化支持和靈活的連接管理。

## 🚀 主要特色

- 🔄 **多連接支持**: 支持多個命名 NATS 連接，取代固定的 Bus/Broker 模式
- 🛠️ **流暢 API**: 直觀的鏈式配置 API，簡化設定流程
- 📦 **自動序列化**: 支持 JSON、Protobuf 等多種序列化格式，每個連接可獨立配置
- ⚡ **延遲載入**: 智能連接管理，未使用的連接不會產生錯誤
- 🔧 **向後兼容**: 完全兼容現有代碼，無縫升級
- 📊 **增強日誌**: 詳細的重連和重試日誌，便於監控和除錯
- 🎯 **事件驅動**: 支持發布/訂閱和請求/回應模式

## 📦 安裝

```bash
dotnet add package EdgeSync.ServiceFramework.Abstractions
dotnet add package EdgeSync.ServiceFramework.Core
dotnet add package EdgeSync.ServiceFramework.DependencyInjection
```

## 🔧 快速開始

### 基本配置

```csharp
builder.Services.AddServiceFramework(options =>
{
    options.DefaultConnection = "bus";
    
    // JSON 序列化連接（適用於通信）
    options.AddConnection("bus", "nats://localhost:4222")
           .WithSerializerRegistry(NatsDefaultSerializerRegistry.Default);
           
    // Protobuf 序列化連接（適用於高性能設備通信）
    options.AddConnection("broker", "nats://localhost:4223")
           .WithSerializerRegistry(NatsProtobufSerializerRegistry.Default)
           .WithCredFile("/path/to/credentials.creds");
});
```

### 從 appsettings.json 配置

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

## 🔌 序列化支持

### 支持的序列化器
- **`NatsDefaultSerializerRegistry`**: JSON 序列化（預設）
- **`NatsProtobufSerializerRegistry`**: Protocol Buffers 序列化
- **`NatsClientDefaultSerializerRegistry`**
- **`NatsJsonSerializerRegistry`**
- **`NatsJsonContextSerializerRegistry`**

### 自動序列化 API
```csharp
// 發布物件 - 自動序列化
await PublishAsync("my.subject", new MyMessage { Id = 1, Name = "Test" });

// 請求物件 - 自動序列化請求和回應
var response = await _bus.RequestAsync<MyRequest, MyResponse>("my.service", request);

// 使用特定連接
await _broker.PublishAsync("device.command", deviceCommand); // 使用 Protobuf
await _bus.PublishAsync("web.event", webEvent);             // 使用 JSON
```

## 🏗️ 架構概念

### 連接類型與用途
- **Default**: 可配置的預設連接，通常指向主要業務邏輯連接
- **Bus**: 用於客戶端通信，通常使用 JSON 序列化
- **Broker**: 用於設備通信，通常使用 Protobuf 序列化

### 服務類型
- **ServiceHandler**: 處理同步請求/回應，適用於 RPC 風格的服務
- **BaseEventHandler**: 處理異步事件，適用於事件驅動架構

## 📖 使用範例

### 事件處理器 BaseEventHandler
```csharp
public class BookRefillEventHandler : BaseEventHandler
{
    protected override string SubjectName => "bookstore.events.book.refill";
    protected override string StreamName => "BookEvents";
    protected override string ConsumerName => "BookRefillConsumer";

    protected override async Task HandleInputEventCore(byte[] message, string subject)
    {
        // 反序列化訊息
        var refillEvent = JsonSerializer.Deserialize<BookRefillEvent>(message);
        
        // 處理事件邏輯
        await ProcessRefillEvent(refillEvent);
        
        // 發布後續事件
        await PublishAsync("bookstore.events.inventory.updated", new InventoryUpdatedEvent
        {
            BookId = refillEvent.BookId,
            NewQuantity = refillEvent.RefillQuantity
        });
    }
}
```

### NATS 客戶端（多連接 + 序列化）
```csharp
public class BookNatsClient : IBookNatsClient
{
    private readonly IJetStreamClient _bus;    // JSON 序列化
    private readonly IJetStreamClient _broker; // Protobuf 序列化

    public BookNatsClient(IJetStreamClientFactory factory)
    {
        _bus = factory.CreateClient("bus");       // 用於微服務間通信
        _broker = factory.CreateClient("broker"); // 用於設備通信
    }

    public async Task<BookDto> GetBookAsync(Guid id)
    {
        // 向服務發送 JSON 請求
        var response = await _bus.RequestAsync<GetBookRequest, BookDto>(
            "bookstore.api.GetBook", 
            new GetBookRequest { Id = id }
        );
        return response;
    }

    public async Task RefillStockAsync(Guid id, int quantity)
    {
        // 發布 Protobuf 格式的設備命令
        await _broker.PublishAsync("device.inventory.refill", new DeviceRefillCommand
        {
            BookId = id,
            Quantity = quantity
        });
    }
}
```

### 服務處理器 ServiceHandler
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
        await ReplyAsync(context, book); // 自動序列化回應
    }
}
```

## 🔄 遷移指南

### 從舊版本升級
舊的 `AddNatsApi` 方法仍然支持，但建議遷移到新的 `AddServiceFramework`：

```csharp
// 舊方式（仍可使用）
services.AddNatsApi(options => 
{
    options.MsgBusUrl = "nats://localhost:4222";
    options.MsgBrokerUrl = "nats://localhost:4223";
});

// 新方式 - 相同功能
services.AddServiceFramework(options => 
{
    options.AddConnection("bus", "nats://localhost:4222")
           .WithSerializerRegistry(NatsDefaultSerializerRegistry.Default);
    options.AddConnection("broker", "nats://localhost:4223")
           .WithSerializerRegistry(NatsDefaultSerializerRegistry.Default);
});
```

## 📚 範例專案
查看 `samples/BookStore` 目錄中的完整範例應用程式，展示：
- 多層架構設計（Web API → NATS Client → NATS API）
- 混合序列化使用（JSON + Protobuf）
- 事件驅動與請求/回應模式
- 錯誤處理和日誌記錄

## 🔧 建置和測試

### 先決條件
- .NET 9.0 SDK
- NATS Server（用於整合測試）

### 建置
```bash
dotnet build
```

### 執行測試
```bash
dotnet test
```

### 執行 BookStore 範例
```bash
cd samples/BookStore
dotnet run --project BookStore.Web.Api
dotnet run --project BookStore.Nats.Api
```

## ⚡ 效能考量
- **JSON 序列化**: 適用於開發友好、可讀性高的場景
- **Protobuf 序列化**: 適用於高頻通信、低延遲的場景
- **連接復用**: 相同配置的連接會自動復用，減少資源消耗

## 📄 版本歷史
查看 [CHANGELOG.zh.md](CHANGELOG.zh.md) 了解詳細的版本更新記錄。

## 📜 授權
此專案採用 MIT 授權 - 詳見 [LICENSE](LICENSE) 檔案。

## 🔗 連結
- [文檔](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edgesync_service_framework)
- [更新日誌](CHANGELOG.zh.md)

---

版權所有 © 2025 研華科技。保留所有權利。