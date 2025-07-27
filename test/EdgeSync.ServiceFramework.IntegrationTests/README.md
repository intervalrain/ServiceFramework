# Service Framework Integration Tests

This test project provides comprehensive integration testing for the refactored Service Framework architecture as outlined in the REFACTOR_PLAN.md. These tests validate that all components work together correctly following SOLID principles and maintain backwards compatibility.

## Test Structure

### 📁 Test Categories

- **Core/Handlers/** - Tests for handler factory and convention mode handlers integration
- **Core/Filters/** - Tests for NatsProxyActionFilter orchestration with dependencies  
- **Core/Conventions/** - Tests for ApplicationServiceConvention with model builders
- **Core/** - Tests for ServiceFrameworkBackgroundService coordination
- **EndToEnd/** - Complete flow tests for request-response and pub-sub scenarios
- **DependencyInjection/** - Tests for service registration and resolution
- **ErrorHandling/** - Tests for error scenarios and edge cases
- **Compatibility/** - Tests for backwards compatibility with existing implementations
- **TestHelpers/** - Shared test infrastructure and utilities

### 🎯 Testing Strategy

#### Integration Test Focus Areas

1. **Component Orchestration**
   - Tests how refactored components work together
   - Validates dependency injection resolution
   - Ensures proper coordination between services

2. **SOLID Principles Validation**
   - Single Responsibility: Each component has focused purpose
   - Open/Closed: Factory pattern allows extension
   - Interface Segregation: Focused, cohesive interfaces
   - Dependency Inversion: Components depend on abstractions

3. **End-to-End Scenarios**
   - Complete request-response flows
   - Full pub-sub processing chains
   - Background service lifecycle management

4. **Error Handling & Resilience**
   - Graceful degradation under failure conditions
   - Proper error propagation and logging
   - Edge case handling across component boundaries

5. **Backwards Compatibility**
   - Existing service interfaces still work
   - Configuration patterns maintained
   - Error handling behavior preserved

## 🚀 Running the Tests

### Prerequisites
- .NET 9.0 SDK
- All Service Framework dependencies resolved

### Running All Integration Tests
```bash
cd test/EdgeSync.ServiceFramework.IntegrationTests
dotnet test
```

### Running Specific Test Categories
```bash
# Handler integration tests
dotnet test --filter "namespace~Handlers"

# End-to-end flow tests  
dotnet test --filter "namespace~EndToEnd"

# Error handling tests
dotnet test --filter "namespace~ErrorHandling"

# Backwards compatibility tests
dotnet test --filter "namespace~Compatibility"
```

### Running with Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## 📊 Test Coverage Goals

### Critical Path Coverage
- ✅ **NatsProxyActionFilter** - Complete orchestration flow
- ✅ **ServiceFrameworkBackgroundService** - Full lifecycle management  
- ✅ **ConventionModeHandlerFactory** - All handler types and modes
- ✅ **ApplicationServiceConvention** - Model builder integration

### Component Integration Coverage
- ✅ **IAuditHandler** - Request/response audit processing
- ✅ **IConnectionResolver** - NATS connection management
- ✅ **IRequestDataExtractor** - Request data processing
- ✅ **IResponseProcessor** - Response formatting
- ✅ **IServiceDiscovery** - Service detection and categorization
- ✅ **IServiceRegistrar** - Service registration coordination
- ✅ **IPubSubManager** - Subscription management

### Scenario Coverage
- ✅ **Request-Response Flow** - Complete end-to-end processing
- ✅ **Pub-Sub Flow** - All convention modes (Classic, JetStream Push/Pull)
- ✅ **Error Scenarios** - Connection failures, timeouts, handler exceptions
- ✅ **Edge Cases** - Null data, malformed contexts, unsupported modes

## 🔧 Test Infrastructure

### ServiceFrameworkTestBase
Base class providing:
- Dependency injection container setup
- Test service registration
- Mock NATS connection factory
- Common test utilities

### TestFixtures
Factory methods for creating:
- ActionExecutingContext instances
- Test NATS connections
- Mock audit wrappers
- Test scenario builders

### Test Doubles
- **TestNatsConnectionFactory** - Mock NATS connections
- **TestNatsService** - Sample service for testing
- **Error simulation classes** - For testing failure scenarios

## 📈 Quality Metrics Validation

### Refactoring Success Criteria
The integration tests validate these refactoring goals:

1. **Code Reduction** ✅
   - Original monolithic classes broken into focused components
   - Each component < 500 lines of code
   - Clear separation of concerns

2. **SOLID Compliance** ✅  
   - Interface-based design throughout
   - Single responsibility per component
   - Dependency inversion implemented

3. **Testability** ✅
   - All components independently testable
   - Mock-friendly interfaces
   - Clear dependency boundaries

4. **Maintainability** ✅
   - Loosely coupled components
   - Factory patterns for extensibility
   - Clear error handling boundaries

5. **Backwards Compatibility** ✅
   - Existing APIs unchanged
   - Configuration patterns preserved
   - Error handling behavior maintained

## 🐛 Debugging Test Failures

### Common Issues
1. **Dependency Resolution Failures**
   - Check ServiceCollectionExtensions.cs registration
   - Verify interface/implementation mapping
   - Ensure proper service lifetimes

2. **Connection Mock Issues**
   - Verify TestNatsConnectionFactory setup
   - Check mock connection behavior configuration
   - Ensure proper serializer registry mocking

3. **Timing Issues**
   - Background service tests may need longer timeouts
   - Async operation completion timing
   - Cancellation token handling

### Debug Logging
Tests use console logging by default. To increase verbosity:
```csharp
.ConfigureLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug); // or LogLevel.Trace
})
```

## 🎯 Test Categories Explained

### 1. Handler Integration Tests
Validate that the ConventionModeHandlerFactory correctly:
- Registers all handler types
- Resolves appropriate handlers for each convention mode  
- Handles unsupported modes gracefully
- Integrates with dependency injection container

### 2. Filter Orchestration Tests
Validate that NatsProxyActionFilter correctly:
- Coordinates all injected dependencies
- Handles different convention modes appropriately
- Processes audit information correctly
- Manages error scenarios gracefully

### 3. Background Service Coordination Tests
Validate that ServiceFrameworkBackgroundService correctly:
- Coordinates service discovery and registration
- Manages pub-sub subscription lifecycle
- Handles startup/shutdown sequences
- Manages error conditions gracefully

### 4. Convention Integration Tests
Validate that ApplicationServiceConvention correctly:
- Integrates with controller/action model builders
- Creates appropriate MVC models
- Handles multiple NATS services
- Ignores non-NATS services appropriately

### 5. End-to-End Flow Tests
Validate complete scenarios:
- **Request-Response**: Full processing chain from request to response
- **Pub-Sub**: Complete publish/subscribe workflow
- **Error Handling**: Error propagation through the entire stack
- **Performance**: Timing characteristics maintained

### 6. Dependency Injection Tests
Validate service registration:
- All components properly registered
- Correct service lifetimes (Singleton/Scoped)
- Factory patterns work correctly
- No circular dependencies
- Performance characteristics maintained

### 7. Error Handling Tests
Validate resilience:
- Component failures handled gracefully
- Error messages appropriate and informative
- System continues operating under partial failures
- Audit information preserved during errors

### 8. Backwards Compatibility Tests
Validate compatibility:
- Existing service interfaces unchanged
- Configuration patterns still work
- Error handling behavior preserved
- Performance characteristics maintained
- Existing attribute usage supported

## 📝 Contributing to Tests

### Adding New Integration Tests
1. Choose appropriate test category directory
2. Inherit from `ServiceFrameworkTestBase`
3. Use `TestFixtures` for common setup
4. Follow naming convention: `[Component][Scenario]IntegrationTests`
5. Include both happy path and error scenarios

### Test Naming Convention
- Class: `{ComponentName}IntegrationTests`
- Method: `{ComponentName}_{Scenario}_{ExpectedOutcome}`
- Example: `HandlerFactory_WithRequestResponseMode_ReturnsCorrectHandler`

### Mock Strategy
- Use NSubstitute for test doubles
- Create focused mocks per test scenario
- Avoid over-mocking - test real integration where possible
- Use TestFixtures.Create* methods for consistency

The integration test suite ensures that the refactored Service Framework architecture maintains quality, performance, and compatibility while providing the benefits of improved maintainability and testability outlined in the refactoring plan.