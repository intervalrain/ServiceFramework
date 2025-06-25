[English](README.md) | 繁體中文

# BookStore ServiceFramework 範例

此範例展示如何使用 EdgeSync ServiceFramework 搭配 Clean Architecture 模式，採用 NATS 訊息傳遞取代傳統的 HTTP API。

## 架構概觀

此解決方案遵循 Clean Architecture 原則，包含以下層級：

- **Domain 領域層**: 核心業務實體、規格與介面
- **Application 應用層**: 業務邏輯、DTOs 與應用服務
- **Infrastructure 基礎設施層**: 資料存取實作（InMemoryRepository）
- **NATS API**: 基於 NATS 的服務處理器
- **NATS Client**: 用於消費 NATS 服務的客戶端函式庫
- **Web API**: 展示 NATS 客戶端使用的 HTTP API

## 功能特色

- ✅ Clean Architecture 實作
- ✅ ErrorOr 錯誤處理模式
- ✅ ServiceFramework NATS 訊息傳遞
- ✅ AutoMapper 物件對應
- ✅ JetBrains.Annotations 驗證
- ✅ Domain 規格商業規則
- ✅ In-memory 資料儲存
- ✅ Swagger/OpenAPI 文件

## 專案結構

```
BookStore/
├── BookStore.Domain/           # 領域實體、規格、錯誤定義
├── BookStore.Application/      # 應用服務、DTOs、對應
├── BookStore.Infrastructure/   # InMemoryRepository 實作
├── BookStore.Nats.Api/        # NATS ServiceHandlers
├── BookStore.Nats.Client/     # NATS 客戶端函式庫
└── BookStore.Web.Api/         # HTTP API 展示
```

## 必要條件

- .NET 9.0 SDK
- NATS Server

## 快速開始

### 步驟 1：安裝並啟動 NATS Server

#### 選項 A：使用 Docker（推薦）
```bash
docker run -d --name nats-server -p 4222:4222 -p 8222:8222 nats:latest
```

#### 選項 B：本地安裝 NATS Server
1. 下載：https://docs.nats.io/running-a-nats-service/introduction/installation
2. 啟動：`nats-server`

### 步驟 2：建置解決方案
```bash
cd samples/BookStore
dotnet build
```

### 步驟 3：啟動 NATS API 服務
```bash
cd BookStore.Nats.Api
dotnet run
```

### 步驟 4：啟動 Web API（新終端機）
```bash
cd BookStore.Web.Api
dotnet run
```

### 步驟 5：測試 API

#### 存取 Swagger UI
開啟瀏覽器並導航至：`https://localhost:xxxx/swagger`（將 xxxx 替換為實際埠號）

## 測試方法

### 方法 1：Swagger UI（最簡單）
1. 開啟 `https://localhost:xxxx/swagger`
2. 嘗試以下操作：
   - **GET /api/books** - 取得所有書籍（應回傳 3 本範例書籍）
   - **GET /api/books/{id}** - 取得特定書籍（從清單中複製 ID）
   - **POST /api/books** - 建立新書籍
   - **PUT /api/books/{id}** - 更新書籍
   - **DELETE /api/books/{id}** - 刪除書籍

### 方法 2：curl 指令

#### 取得所有書籍
```bash
curl -X GET "https://localhost:xxxx/api/books" -k
```

#### 取得特定書籍
```bash
# 先取得所有書籍找到 ID，然後：
curl -X GET "https://localhost:xxxx/api/books/{book-id}" -k
```

#### 建立新書籍
```bash
curl -X POST "https://localhost:xxxx/api/books" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "設計模式",
    "author": "Gang of Four",
    "isbn": "9780201633610",
    "price": 54.99,
    "stock": 15
  }' -k
```

#### 更新書籍
```bash
curl -X PUT "https://localhost:xxxx/api/books/{book-id}" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "更新標題",
    "author": "更新作者",
    "price": 59.99,
    "stock": 20
  }' -k
```

#### 刪除書籍
```bash
curl -X DELETE "https://localhost:xxxx/api/books/{book-id}" -k
```

## 範例資料

InMemoryRepository 包含 3 本範例書籍：

1. **Clean Architecture** by Robert C. Martin
   - ISBN: 9780134494166
   - 價格: $45.99
   - 庫存: 10

2. **Domain-Driven Design** by Eric Evans
   - ISBN: 9780321125217
   - 價格: $52.99
   - 庫存: 5

3. **Microservices Patterns** by Chris Richardson
   - ISBN: 9781617294549
   - 價格: $48.99
   - 庫存: 8

## 組態設定

### 環境變數
- `MSG_BROKER_URL`: NATS broker URL（預設：`nats://localhost:4222`）
- `MSG_BUS_URL`: NATS bus URL（預設：`nats://localhost:4222`）

### 自訂組態
您可以在 `Program.cs` 檔案中修改 NATS URL：

**BookStore.Nats.Api/Program.cs:**
+ **推薦**: 使用 `AddServiceFramework`
```csharp
builder.Services.AddServiceFramework(static options =>
{
    options.DefaultConnection = "bus";
    options.AddConnection("bus", Environment.GetEnvironmentVariable("MSG_BUS_URL") ?? "nats://localhost:4223")
        .WithCredFile(Environment.GetEnvironmentVariable("MSG_BUS_CREDFILE") ?? string.Empty)
        .WithSerializerRegistry(NatsDefaultSerializerRegistry.Default);

    options.AddConnection("broker", Environment.GetEnvironmentVariable("MSG_BROKER_URL") ?? "nats://localhost:4222")
        .WithCredFile(Environment.GetEnvironmentVariable("MSG_BROKER_CREDFILE") ?? string.Empty)
        .WithSerializerRegistry(NatsProtobufSerializerRegistry.Default);
});
```

+ **仍向下相容**，但未來可能會棄用: 使用 `AddNatsApi`
```csharp
builder.Services.AddNatsApi(options =>
{
    options.MsgBrokerUrl = "nats://your-broker:4222";
    options.MsgBusUrl = "nats://your-bus:4222";
});
```

## NATS 主題

可用的 NATS 主題：

- `bookstore.books.{id}.get` - 取得單一書籍
- `bookstore.books.get` - 取得所有書籍
- `bookstore.books.post` - 建立新書籍
- `bookstore.books.{id}.put` - 更新現有書籍
- `bookstore.books.{id}.delete` - 刪除書籍

## 錯誤處理

此範例使用 ErrorOr 模式展示完整的錯誤處理：

### 領域錯誤
- **Book.NotFound** (404) - 找不到書籍
- **Book.InvalidTitle** (400) - 無效標題
- **Book.InvalidAuthor** (400) - 無效作者
- **Book.InvalidISBN** (400) - 無效 ISBN 格式
- **Book.InvalidPrice** (400) - 無效價格
- **Book.InvalidStock** (400) - 無效庫存

### 通訊錯誤
- **Communication.ConnectionFailed** (503) - NATS 連線失敗
- **Communication.RequestTimeout** (503) - 請求逾時
- **Communication.InvalidResponse** (503) - 無效回應
- **Communication.ServiceUnavailable** (503) - 服務無法使用
- **Communication.SerializationFailed** (503) - 序列化失敗

## 疑難排解

### NATS Server 未執行
**錯誤：** 連線被拒絕或逾時錯誤
**解決方案：** 確保 NATS server 在埠 4222 上執行

### 埠已被佔用
**錯誤：** "Unable to bind to https://localhost:xxxx"
**解決方案：** 
- 終止程序：`lsof -ti:xxxx | xargs kill -9`
- 或使用不同埠：`dotnet run --urls="https://localhost:8080"`

### API 回傳 503 服務無法使用
**錯誤：** 所有 API 呼叫都回傳 503
**解決方案：** 
1. 檢查 NATS API 服務（BookStore.Nats.Api）是否執行
2. 驗證 NATS server 是否可存取
3. 檢查連線錯誤日誌

### 驗證錯誤
API 使用 JetBrains.Annotations 驗證輸入：
- 標題：必填，最多 200 字元
- 作者：必填，最多 100 字元
- ISBN：必填，必須為 10 或 13 位數字
- 價格：必須 > 0 且 < 999999.99
- 庫存：必須 >= 0

## API 端點

| 方法 | 端點 | 描述 | 請求內容 |
|------|------|------|----------|
| GET | `/api/books` | 取得所有書籍 | 無 |
| GET | `/api/books/{id}` | 依 ID 取得書籍 | 無 |
| POST | `/api/books` | 建立新書籍 | CreateBookDto |
| PUT | `/api/books/{id}` | 更新書籍 | UpdateBookDto |
| DELETE | `/api/books/{id}` | 刪除書籍 | 無 |

### 請求/回應範例

**CreateBookDto:**
```json
{
  "title": "書籍標題",
  "author": "作者姓名",
  "isbn": "1234567890",
  "price": 29.99,
  "stock": 100
}
```

**BookDto 回應:**
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "title": "書籍標題",
  "author": "作者姓名",
  "isbn": "1234567890",
  "price": 29.99,
  "stock": 100,
  "createdAt": "2024-01-01T00:00:00.000Z"
}
```

## 關鍵設計模式

1. **Clean Architecture**: 關注點分離與依賴反轉
2. **ErrorOr**: 功能性錯誤處理，不使用例外
3. **Repository Pattern**: 資料存取抽象化
4. **Specification Pattern**: 業務規則封裝
5. **CQRS**: 應用服務中的命令/查詢分離
6. **Messaging Pattern**: 基於 NATS 的通訊

## 學習目標

此範例教學：

- 如何結構化 Clean Architecture 解決方案
- 如何使用 EdgeSync ServiceFramework 搭配 NATS
- 如何實作 ErrorOr 模式
- 如何建立基於 NATS 的微服務
- 如何為 NATS 服務建立客戶端函式庫
- 如何在分散式系統中優雅地處理錯誤
