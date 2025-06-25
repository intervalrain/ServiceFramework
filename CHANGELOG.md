[TOC]

# Changelog

English | [繁體中文](CHANGELOG.md)

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).


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