# Prototype Code

This directory contains the working prototype code from the DI service registration research.

## ⚠️ Important Note

This prototype includes exploration of an "isolated DataFlow" API (`AddIsolatedDataFlow`, `IsolatedDataFlowBuilder`) which was later determined to be unnecessary. **The isolated DataFlow API should NOT be implemented in production.** 

For service isolation scenarios, applications can create their own `IServiceCollection`, use the canonical registration APIs, and build a separate service provider as shown in the addendum documentation.

## Purpose

These files demonstrate the validated approach for canonical DI service registration in DataFlow. They were created during research to validate the API design.

## Files

### ServiceCollectionExtensions.cs (Original)
- Location in prototype: `/poc/DataFlow.POC/DependencyInjection/ServiceCollectionExtensions.cs`
- Purpose: Extension method for `IServiceCollection.AddDataFlows()`
- Classes:
  - `DataFlowBuilder` - Fluent builder for registering blocks and strategies
  - `ServiceCollectionExtensions` - Extension methods
- **Status**: ✅ Recommended for implementation

### ServiceCollectionExtensions-v2.cs (Extended)
- Extended version with class-based definitions
- Includes `IDataFlowDefinition` interface
- Includes `AddDataFlowDefinition<T>()` extension
- **Note**: Also includes isolated DataFlow API which should be EXCLUDED from production implementation
- **Status**: ✅ Class-based features recommended, ❌ Isolated API not recommended

### DataFlowGraphBuilderEx.cs
- Location in prototype: `/poc/DataFlow.POC/Builder/DataFlowGraphBuilderEx.cs`
- Purpose: Extended graph builder with DI support
- Key Features:
  - `.UseBlock(name)` to resolve from DI
  - `.AddBlock(instance)` for inline blocks
  - All methods from original `DataFlowGraphBuilder`
- **Status**: ✅ Recommended for implementation

### DiServiceRegistrationTests.cs
- Location in prototype: `/poc/DataFlow.POC.Tests/DiServiceRegistrationTests.cs`
- Purpose: Comprehensive test suite for core DI registration
- Test Coverage (7 tests):
  - Basic DI registration and usage
  - Backward compatibility (hybrid approach)
  - Error scenarios
  - Strategy registration
  - Lifetime verification
- **Status**: ✅ All tests recommended for implementation

### AdvancedDiRegistrationTests.cs
- Purpose: Tests for extended features
- Test Coverage (4 tests):
  - Class-based definition registration ✅ Recommended
  - Isolated DataFlow tests ❌ Skip these (tests for API we're not implementing)
- **Status**: ⚠️ Include class-based test only, skip isolated DataFlow tests

## Validation Results

✅ **Core Features (7 Tests Passing)**
- Traditional approach (baseline)
- New canonical DI approach  
- Hybrid approach (mix DI and inline)
- Error handling (missing block, no service provider)
- Strategy registration
- Singleton lifetime verification

✅ **Class-Based Definitions (1 Test Passing)**
- Class-based definition registration and usage

✅ **Total: 8/8 Recommended Tests Passing**

⚠️ **Excluded from Recommendation**:
- 3 tests for isolated DataFlow API (not recommended for implementation)

✅ **Performance Validated**
- Keyed service resolution overhead: <0.1%
- No meaningful performance impact

## How to Use (for Implementation Team)

1. **Review the code** - Focus on core DI and class-based features
2. **Exclude isolated DataFlow API** - Do not implement `AddIsolatedDataFlow` or `IsolatedDataFlowBuilder`
3. **Copy recommended features** - See implementation issue for target paths
4. **Enhance XML documentation** - Add detailed comments
5. **Add integration tests** - Build on the 8 recommended unit tests
6. **Update documentation** - README, migration guides, etc.

## Key Design Decisions

**Keyed Services**:
- Uses .NET 8's keyed services for named resolution
- Blocks registered as singletons

**Backward Compatibility**:
- Separate `DataFlowGraphBuilderEx` class (doesn't modify original)
- Supports mixing DI-registered and inline blocks

**Error Handling**:
- Clear, actionable error messages
- Runtime validation with helpful guidance

## References

- [Research README](/research/di-service-registration/README.md)
- [Class-Based Addendum](/research/di-service-registration/addendum-class-based.md)
- [API Design Specification](/research/di-service-registration/design/api-design.md)
- [ADR](/poc/docs/adr/2025-11-17-di-service-registration.md)
- [Implementation Issue](/research/di-service-registration/handover/implementation-issue.md)

## Recommendation Summary

**✅ Implement**:
- Core DI registration (`AddDataFlows`, `DataFlowBuilder`)
- Extended graph builder (`DataFlowGraphBuilderEx.UseBlock`)
- Class-based definitions (`IDataFlowDefinition`, `AddDataFlowDefinition<T>`)

**❌ Do NOT Implement**:
- Isolated DataFlow API (`AddIsolatedDataFlow`, `IsolatedDataFlowBuilder`)
- Applications can achieve service isolation by creating their own service collection (see addendum)
