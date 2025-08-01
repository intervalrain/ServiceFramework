# Changelog

[English](CHANGELOG.md) | 中文

本專案的所有重要變更都將記錄在此檔案中。

格式基於 [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)，
並且本專案遵循 [語義化版本控制](https://semver.org/spec/v2.0.0.html)。

## [1.3.0] - 2025-08-01
### 新增功能
- **MessageFramework**: Added RequestDto and ResponseDto, and automatically add audit wrapper via setting  EnableAuditWrapper
- **Audit Log**: 加入統一的 request & response 的訊息記錄。

## [1.2.2] - 2025-07-17
### 功能增強
- **AddNatsCheck**: 提供更簡潔的 HealthCheck 介面供使用者使用(HealthCheckBuilder 的 Extension)。
### 修復
- 修復多連線的問題。
- 修復 NatsConnectionHealthCheck 問題。

## [1.2.1] - 2025-07-14
### 功能增強
- **StreamConfigOptions**: 將 StreamConfigOptions 改為可覆寫（virtual）屬性，使子類別能自訂 Stream 設定。
- **ConsumerConfigOptions**: 將 ConsumerConfigOptions 改為可覆寫（virtual）屬性，使子類別能自訂 Consumer 設定。
- **AutoConvention**: 調整 Request/Response 方法，支援官方 AddEndpointAsync 與 AddServiceAsync 的用法。

## [1.2.0] - 2025-07-11
### 新增功能
- **AutoConvention**: 提供 NatsService 與 ApplicationConvention，簡化 NATS controller 的自動補全與註冊。
- **HealthCheck&**: 新增 NatsConnectionHealthCheck，用於檢查 NATS 連線的健康狀態。

## [1.2.0] - 2025-07-11
### Added
- **健康度檢查**: 提供 `NatsConnectionHealthCheck` 來檢查 NATS 的連線健康狀態。

## [1.1.2] - 2025-06-25
### 新增功能
- **Jet-Stream Ack 模式**: 提供 `PubAckResponse` 作為 `PublishAsync` 的回傳值。

## [1.1.1] - 2025-06-25
### 新增功能
- **Stream, Consumer 配置**: 新增 `JetStreamConfigOptions` 與 `ConsumerConfigOptions` 類別的配置。

## [1.1.0] - 2025-06-25
### 新增功能
- **多連接支持**: 支持多個命名 NATS 連接，取代固定的 Bus/Broker 模式
- **流暢 API**: 新增支持方法鏈式調用的 `AddServiceFramework` 配置 API
- **序列化系統**: 內建支持 JSON 和 Protobuf 序列化，每個連接可獨立配置
- **延遲載入架構**: 智能連接管理與延遲載入 - 未使用的連接不會產生錯誤
- **增強重試日誌**: 詳細的重試進度日誌，顯示 `i/retryCount` 格式，支持用戶提供的 Logger
- **NatsConnectionBuilder**: 流暢 API 連接配置，支持 `.WithSerializerRegistry()` 和 `.WithCredFile()` 方法
- **ServiceFrameworkOptions**: 新的配置類別，支持多個命名連接與驗證
- **MessageTransportBase 改進**: 新增 `Default` 屬性和便利的 `PublishAsync` 方法，簡化使用
- **BookStore 範例**: 完整的範例應用程式，展示包含 Web API、NATS Client 和 NATS API 的新架構
- **自動序列化**: 增強的 `RequestAsync`、`PublishAsync` 和 `NatsPublishAsync` 方法支持自動物件序列化
- **SerializerRegistry 支持**: 支持 `NatsDefaultSerializerRegistry` (JSON) 和 `NatsProtobufSerializerRegistry` (Protobuf)

### 功能增強
- **ServiceHandler**: 新增支持命名連接的建構函式，同時保持向後兼容性
- **BaseEventHandler**: 新增支持命名連接的建構函式，包含完整的重試計數和日誌記錄
- **NatsConnClient**: 改進重試邏輯，支持用戶提供的 Logger 和控制台輸出回退
- **連接管理**: 新增重複名稱驗證和完整的錯誤處理
- **重試邏輯**: 為 ServiceHandler 和 BaseEventHandler 新增重試計數與詳細進度日誌
- **連接工廠**: 增強 `NatsConnectionFactory` 支持帶序列化器配置的 `NatsConnectionSettings`

### 棄用功能
- **AddNatsApi**: 標記為過時並顯示警告訊息，請使用 `AddServiceFramework` 替代（未來版本將移除）

### 問題修復
- **Logger 可見性**: 解決 NATS 連接重試日誌不顯示在控制台的問題
- **連接穩定性**: 改進連接可靠性，提供更好的錯誤處理和重試機制

### 向後兼容性
- 所有現有 API 保持完全功能，無需修改
- 保留舊版建構函式和方法，確保無縫遷移
- 現有配置繼續運作，無需任何變更
- `AddNatsApi` 方法仍可使用但會顯示棄用警告

### 遷移指南
用戶可以逐步從舊 API 遷移到新 API：

```csharp
// 舊方式（仍可使用）
services.AddNatsApi(options => { ... });

// 新方式（推薦）
services.AddServiceFramework(options => 
{
    options.AddConnection("bus", "nats://localhost:4222")
           .WithSerializerRegistry(NatsDefaultSerializerRegistry.Default);
});
```

## [1.0.8] - 2025-06-06
### 變更
- JsonSerializer 現在預設使用 camelCase 命名策略

## [1.0.7] - 2025-05-16
### 新增功能
- 為 `IJetStreamClient` 介面新增 `RequestAsync` 方法

## [1.0.6] - 2025-04-30
### 修復
- 修復版本 1.0.5 的拼寫錯誤

## [1.0.5] - 2025-04-18
### 新增功能
- 實作 IOptions 模式支持
- 新增透過 appsettings.json 注入連接 URL 和 credFile 的支持

## [1.0.4] - 2025-04-18
### 新增功能
- 提供流暢配置方法 `AddNatsApi()`
- 新增除了 .env 檔案之外注入 URL 和 credFile 參數的替代方法

## [1.0.3] - 2025-04-17
### 變更
- 修改 KV Store 使用 Bus Channel 機制

## [1.0.2] - 2025-04-17
### 變更
- 移除連接 URL 的預設值
- 連接 URL 現在是必填參數

## [1.0.1] - 2025-04-08
### 新增功能
- 新增 Testlib 測試程式庫
- 提供 `MockJetStreamClient` 模擬類別

## [1.0.0] - 2025-04-08
### 新增功能
- 從原專案分離為獨立程式庫
- 架構分為 Abstractions 和 Core 實作層

### 破壞性變更
- 初始版本，從原專案重構並分離