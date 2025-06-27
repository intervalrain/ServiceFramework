# AuthorSystem 四種消息模式演示

本文檔展示了 EdgeSync Service Framework 中的四種消息通信模式，以 `AuthorAppService` 為例。

## 📋 模式總覽

| 模式 | 特徵 | 使用場景 | 範例方法 |
|------|------|----------|----------|
| **Request/Response** | 有回傳值 | 同步查詢、CRUD 操作 | `GetAsync`, `CreateAsync` |
| **Pub/Sub Push with JetStream** | 無回傳值 + JetStream | 事件發布、需要持久化 | `PublishAuthorCreatedEvent` |
| **Classic Pub/Sub** | 無回傳值 + 明確禁用JetStream | 高頻率、低延遲通信 | `HandleBatchAuthorUpdate` |
| **Pull Mode with JetStream** | JetStreamPullAttribute | 控制消費速度、重要業務 | `HandlePriorityNotification` |

## 🔄 決策樹對應

### Mode 1: Request/Response
```
決策樹路徑: (1)有ReturnType -> (2)Request/Response Mode
```

**特徵：**
- 方法有回傳值（如 `ErrorOr<T>`）
- 同步請求-回應模式
- 客戶端等待回應

**範例：**
```csharp
[Subject("get-author", "authorsys.authors.*.get")]
public async Task<ErrorOr<AuthorDto>> GetAsync(Guid id)
```

**使用場景：**
- CRUD 操作
- 查詢操作
- 需要立即回應的業務邏輯

### Mode 2: Pub/Sub Push with JetStream
```
決策樹路徑: (1)無ReturnType -> (3)否Collection -> (5)否JetStreamPullAttribute -> (7)有JetStreamSubject -> (10)Pub/Sub Pull Mode with JetStream
```

**特徵：**
- 無回傳值
- 使用 `[JetStream(true)]` 明確啟用
- 支援事件持久化和重播

**範例：**
```csharp
[Subject("author-created-event", "authorsys.events.author.created")]
[JetStream(true)]
public async Task PublishAuthorCreatedEvent(AuthorEventDto eventData)
```

**使用場景：**
- 重要業務事件發布
- 需要事件溯源
- 跨服務事件通知
- 需要持久化的消息

### Mode 3: Classic Pub/Sub
```
決策樹路徑: (1)無ReturnType -> (3)是Collection -> (4)否JetStreamPullAttribute -> (6)否JetStreamSubject -> 明確禁用JetStream -> (13)Pub/Sub Push Mode classic
```

**特徵：**
- 無回傳值
- 使用 `[JetStream(false)]` 明確禁用
- Collection 參數類型
- 高性能、低延遲

**範例：**
```csharp
[Subject("batch-author-update", "authorsys.classic.batch.update")]
[JetStream(false)]
public async Task HandleBatchAuthorUpdate(List<BatchAuthorOperationDto> operations)
```

**使用場景：**
- 高頻率狀態同步
- 簡單批次操作
- 不需要持久化的消息
- 對延遲敏感的操作

### Mode 4: Pull Mode with JetStream
```
決策樹路徑: (1)無ReturnType -> (3)否Collection -> (5)有JetStreamPullAttribute -> (15)Pub/Sub Pull Mode with JetStream
```

**特徵：**
- 使用 `[JetStreamPull]` 屬性
- 支援消費者控制
- 可配置批次大小和確認策略

**範例：**
```csharp
[Subject("priority-author-notification", "authorsys.pull.priority.notification")]
[JetStreamPull(ConsumerName = "PriorityNotificationConsumer", MaxMessages = 5)]
public async Task HandlePriorityNotification(AuthorNotificationDto notification)
```

**使用場景：**
- 重要業務邏輯處理
- 需要控制處理速度
- 需要手動確認的任務
- 批次維護操作

## ⚙️ JetStreamPullAttribute 配置選項

```csharp
[JetStreamPull(
    ConsumerName = "MyConsumer",          // 消費者名稱
    ConsumerGroup = "MyGroup",            // 消費者群組
    MaxMessages = 10,                     // 一次拉取的最大消息數
    AckPolicy = "Explicit",               // 確認策略
    CreateConsumerIfNotExists = true      // 自動創建消費者
)]
```

## 🎯 使用建議

### 選擇 Request/Response 當：
- 需要立即回應
- CRUD 操作
- 客戶端需要知道操作結果

### 選擇 Pub/Sub Push with JetStream 當：
- 發布重要業務事件
- 需要事件持久化
- 跨服務通信
- 需要事件重播能力

### 選擇 Classic Pub/Sub 當：
- 高頻率操作
- 對延遲敏感
- 簡單的狀態同步
- 不需要持久化

### 選擇 Pull Mode 當：
- 需要控制處理速度
- 重要業務邏輯
- 需要確保消息處理成功
- 批次處理場景

## 🔧 配置範例

在 `appsettings.json` 中配置：

```json
{
  "AutoConvention": {
    "DefaultJetStreamEnable": true,
    "RoutePrefix": "nats"
  }
}
```

## 📊 性能考量

| 模式 | 延遲 | 吞吐量 | 持久化 | 可靠性 |
|------|------|--------|--------|--------|
| Request/Response | 中等 | 中等 | ❌ | 高 |
| Push + JetStream | 中等 | 高 | ✅ | 高 |
| Classic Pub/Sub | 低 | 極高 | ❌ | 中等 |
| Pull + JetStream | 可控 | 可控 | ✅ | 極高 |

## 🚀 最佳實踐

1. **Request/Response**: 用於 API 端點和查詢操作
2. **Push + JetStream**: 用於重要事件發布
3. **Classic Pub/Sub**: 用於高頻狀態更新
4. **Pull Mode**: 用於關鍵業務處理

每種模式都有其適用場景，選擇正確的模式可以優化系統性能和可靠性。