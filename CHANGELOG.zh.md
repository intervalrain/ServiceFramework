# Changelog

English | [中文](CHANGELOG.md)

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2024-12-25

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
### Changed
- JsonSerializer now uses camelCase naming policy by default

## [1.0.7] - 2025-05-16
### Added
- Added `RequestAsync` method to `IJetStreamClient` interface

## [1.0.6] - 2025-04-30
### Fixed
- Fixed typo errors from version 1.0.5

## [1.0.5] - 2025-04-18
### Added
- Implemented IOptions pattern support
- Added support for injecting connection URL and credFile via appsettings.json

## [1.0.4] - 2025-04-18
### Added
- Provided Fluent Configuration method `AddNatsApi()`
- Added alternative ways to inject URL and credFile parameters beyond .env files

## [1.0.3] - 2025-04-17
### Changed
- Modified KV Store to use Bus Channel mechanism

## [1.0.2] - 2025-04-17
### Changed
- Removed default value for connection URL
- Connection URL is now a mandatory parameter (must-be)

## [1.0.1] - 2025-04-08
### Added
- Added Testlib testing library
- Provided `MockJetStreamClient` mock class

## [1.0.0] - 2025-04-08
### Added
- Separated from original project as independent library
- Architecture split into Abstractions and Core implementation layers

### Breaking Changes
- Initial release, refactored and separated from original project