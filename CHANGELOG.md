[TOC]

# Changelog

English | [繁體中文](CHANGELOG.zh.md)

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2025-07-11
### Added
- **HealthCheck**: Provided `NatsConnectionHealthCheck` for health status of NATS connection.

## [1.1.2] - 2025-06-25
### Added
- **Jet-Stream Ack Mode**: Provided `PubAckResponse` as return type of `PublishAsync`.

## [1.1.1] - 2025-06-25
### Added
- **Stream, Consumer Configuration**: Added `JetStreamConfigOptions` and `ConsumerConfigOptions` configurations.

## [1.1.0] - 2025-06-25
### Added
- **Multi-Connection Support**: Support for multiple named NATS connections instead of fixed Bus/Broker pattern
- **Fluent API**: New `AddServiceFramework` method with fluent configuration API supporting method chaining
- **Serialization System**: Built-in support for JSON and Protobuf serialization with per-connection configuration
- **Lazy Loading Architecture**: Intelligent connection management with lazy loading - unused connections won't cause errors
- **Enhanced Retry Logging**: Detailed retry progress logging showing `i/retryCount` format with user-provided logger support
- **NatsConnectionBuilder**: Fluent API for connection configuration with `.WithSerializerRegistry()` and `.WithCredFile()` methods
- **ServiceFrameworkOptions**: New configuration class supporting multiple named connections with validation
- **MessageTransportBase improvements**: Added `Default` property and convenience `PublishAsync` methods for simplified usage
- **BookStore Sample**: Complete example application demonstrating new architecture with Web API, NATS Client, and NATS API layers
- **Automatic Serialization**: Enhanced `RequestAsync`, `PublishAsync`, and `NatsPublishAsync` methods with automatic object serialization
- **SerializerRegistry Support**: Support for `NatsDefaultSerializerRegistry` (JSON) and `NatsProtobufSerializerRegistry` (Protobuf)

### Enhanced
- **ServiceHandler**: Added new constructor supporting named connections while maintaining backward compatibility
- **BaseEventHandler**: Added new constructor supporting named connections with comprehensive retry counting and logging
- **NatsConnClient**: Improved retry logic with user-provided logger support and fallback to Console output
- **Connection Management**: Added duplicate name validation and comprehensive error handling
- **Retry Logic**: Added retry counting for ServiceHandler and BaseEventHandler with detailed progress logging
- **Connection Factory**: Enhanced `NatsConnectionFactory` to support `NatsConnectionSettings` with serializer configuration

### Deprecated
- **AddNatsApi**: Marked as obsolete with warning message, use `AddServiceFramework` instead (will be removed in future version)

### Fixed
- **Logger Visibility**: Resolved issues with NATS connection retry logging not appearing in console
- **Connection Stability**: Improved connection reliability with better error handling and retry mechanisms

### Backward Compatibility
- All existing APIs remain fully functional without modification
- Legacy constructors and methods are preserved for seamless migration
- Existing configurations continue to work without any changes
- `AddNatsApi` method still works but shows deprecation warning

### Migration Guide
Users can gradually migrate from the old API to the new one:

```csharp
// Old way (still works)
services.AddNatsApi(options => { ... });

// New way (recommended)
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