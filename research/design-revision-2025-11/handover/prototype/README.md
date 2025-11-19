# Prototype Code

This directory contains the working prototype code from the design revision research.

## Purpose

These files demonstrate the validated solution to the **six concerns** from issue #473:
1. Single enhanced builder (no parallel structures)
2. Scoped default lifetime
3. Duplicate registration detection
4. Integrated graph building via AddGraph()
5. Namespace support for modular monoliths
6. DI-friendly block registration with typed helpers

## Files

### ServiceCollectionExtensions.cs
- **Purpose**: Registration API with lifetime-specific methods and namespace support
- **Location in prototype**: `/poc/DataFlow.POC/DependencyInjection/ServiceCollectionExtensions.cs`
- **Classes**:
  - `DataFlowBuilder` - Registration builder with lifetime methods and namespace support
  - `IDataFlowDefinition` - Interface for class-based graph definitions
  - `ServiceCollectionExtensions` - Extension methods for IServiceCollection
- **Status**: ✅ Recommended for implementation

**Key Features**:
- `AddBlock()` defaults to scoped lifetime
- Explicit `AddScopedBlock`, `AddSingletonBlock`, `AddTransientBlock` methods
- `AddStrategy()` methods with lifetime variants
- `AddGraph()` for integrated topology registration
- `AddGraphDefinition<T>()` for class-based definitions
- Duplicate detection with clear error messages
- Namespace support via `AddDataFlows(string namespacePrefix, ...)`
- Typed helpers: `AddActorBlock<TIn, TOut, TActor>()`
- IBlockContext initialization pattern for block names

### BlockBase.cs
- **Purpose**: Enhanced base class with IBlockContext support
- **Location in prototype**: `/poc/DataFlow.POC/Core/BlockBase.cs`
- **Changes**:
  - Added `SetContext(IBlockContext)` method
  - Updated `Name` property to use `_context?.BlockName`
  - Single-call enforcement for `SetContext()`
- **Status**: ✅ Recommended for implementation

**Key Features**:
- Lifecycle initialization pattern (construction → initialization)
- Proper encapsulation (SetContext called once)
- Extensible via IBlockContext.Metadata

### ActorBlock.cs  
- **Purpose**: Example block with parameterless constructor for DI
- **Location in prototype**: `/poc/DataFlow.POC/Blocks/ActorBlock.cs`
- **Changes**:
  - Added parameterless constructor
  - Constructor parameters resolved via DI
- **Status**: ✅ Reference for DI-friendly block pattern

### DataFlowGraphBuilder.diff
- **Purpose**: Changes to enhance existing builder with namespace support
- **Original file**: `/poc/DataFlow.POC/Builder/DataFlowGraphBuilder.cs`
- **Changes**:
  - Added optional `IServiceProvider` constructor parameter
  - Added `UseBlock(string name)` method to resolve from DI
  - Added namespace-aware key resolution
  - No changes to existing methods (backward compatible)
- **Status**: ✅ Recommended for implementation

**Key Features**:
- Backward compatible (service provider is optional)
- `UseBlock()` resolves blocks from keyed services with namespace support
- Clear error messages for missing provider or blocks
- Supports hybrid usage (mix DI and direct blocks)
- Namespace property for introspection

### RevisedDiDesignTests.cs
- **Purpose**: Comprehensive test suite validating the design
- **Location in prototype**: `/poc/DataFlow.POC.Tests/RevisedDiDesignTests.cs`
- **Test Coverage**: 32 tests, all passing ✅

**Test Categories**:
1. **Single Builder Pattern** (6 tests)
   - Backward compatibility without service provider
   - DI support with service provider
   - Hybrid usage (mix DI and direct blocks)
   - Error handling (no provider, missing blocks)

2. **Lifetime Scopes** (6 tests)
   - Default scoped lifetime verification
   - Explicit singleton, scoped, transient registration
   - Different instances across scopes (scoped)
   - Same instance across scopes (singleton)

3. **Registration Idempotence** (3 tests)
   - Throws on duplicate block names
   - Multiple AddDataFlows calls with different blocks
   - Error on same name across calls

4. **Graph Integration** (4 tests)
   - AddGraph registers in DI
   - Graph resolution from DI
   - Class-based definitions
   - Unified registration example
   - Dynamic graphs still supported

5. **Namespace Support** (10 tests)
   - Global namespace default
   - Custom namespace prefixes
   - Cross-namespace references
   - Namespace-aware block resolution
   - Namespace-aware graph resolution
   - Duplicate detection within/across namespaces

6. **DI-Friendly Block Registration** (3 tests)
   - Typed helper methods (AddActorBlock)
   - IBlockContext initialization
   - Name from context matches registration key

**Status**: ✅ All tests should be ported to implementation

## Validation Results

✅ **All Concerns Addressed**:
1. ✅ Single builder (DataFlowGraphBuilder enhanced)
2. ✅ Scoped default lifetime (safe for multi-instance)
3. ✅ Duplicate detection (clear errors)
4. ✅ Integrated API (AddGraph method)
5. ✅ Namespace support (modular monoliths)
6. ✅ DI-friendly blocks (typed helpers + IBlockContext)

✅ **Test Results**: 32/32 passing

✅ **Performance**: Negligible overhead (<0.1%)

✅ **Backward Compatibility**: Fully compatible

✅ **Scoped Service Disposal**: Properly tracked (uses GetRequiredService, not ActivatorUtilities)

## How to Use (for Implementation Team)

### Step 1: Implement ServiceCollectionExtensions.cs

Copy the file to production location:
- Target: `/poc/DataFlow.POC/DependencyInjection/ServiceCollectionExtensions.cs`
- Review and enhance XML documentation
- Add any additional validation as needed

### Step 2: Apply BlockBase Changes

Enhance the existing base class:
- Target: `/poc/DataFlow.POC/Core/BlockBase.cs`
- Add `SetContext(IBlockContext)` method
- Update `Name` property to use context
- Ensure single-call enforcement for SetContext

### Step 3: Apply DataFlowGraphBuilder Changes

Apply the diff to enhance the existing builder:
- Target: `/poc/DataFlow.POC/Builder/DataFlowGraphBuilder.cs`
- Add `IServiceProvider?` field and constructor parameter
- Add `UseBlock(string name)` method
- Add namespace-aware resolution
- Review error messages

### Step 4: Update Block Implementations

For DI-friendly registration, blocks need parameterless constructors:
- Reference: `ActorBlock.cs` in this directory
- Add parameterless constructors to block types
- Dependencies resolved via DI

### Step 5: Port Tests

Copy test file and adapt as needed:
- Target: `/poc/DataFlow.POC.Tests/` (choose appropriate name)
- All 32 tests should pass
- Consider adding integration tests

### Step 6: Update Documentation

- README updates with new patterns
- Migration guide from previous #473 design
- Lifetime selection guidance
- Examples demonstrating all features
- Namespace usage guide
- DI-friendly block registration guide

## Key Design Decisions

**Scoped Default**:
- Blocks registered as scoped by default (not singleton)
- Safe for scoped dependencies (DbContext, etc.)
- Supports multi-instance graph execution

**Duplicate Detection**:
- Throws `InvalidOperationException` on duplicate names
- Clear error messages for debugging
- Prevents configuration mistakes

**Single Builder**:
- Enhanced existing `DataFlowGraphBuilder`
- No parallel `DataFlowGraphBuilderEx`
- Optional service provider for DI support

**Graph Integration**:
- `AddGraph()` method for inline topology
- `AddGraphDefinition<T>()` for class-based definitions
- Dynamic graphs still supported via builder

**Namespace Support**:
- Optional namespace prefix for modular monoliths
- Default "global:" namespace for consistency
- Colon (`:`) separator for fully-qualified keys
- Cross-namespace references supported

**DI-Friendly Blocks**:
- Typed helpers eliminate name duplication
- IBlockContext initialization pattern
- Uses standard DI resolution (GetRequiredService)
- Proper scoped service disposal tracking
- Extensible via IBlockContext

## Differences from Previous Design (Issue #473)

| Aspect | Issue #473 | This Revision |
|--------|------------|---------------|
| **Builder** | DataFlowGraphBuilderEx (new) | DataFlowGraphBuilder (enhanced) |
| **Default Lifetime** | Singleton | Scoped |
| **Idempotence** | Not specified | Throws on duplicates |
| **Graph Integration** | Separate | AddGraph() method |
| **Namespace Support** | Not supported | Optional prefixes |
| **Block Registration** | Name in constructor | Typed helpers + IBlockContext |
| **Backward Compat** | New builder required | Fully compatible |

## References

- **Research README**: `/research/design-revision-2025-11/README.md`
- **API Specification**: `/research/design-revision-2025-11/design/api-spec.md`
- **ADR**: `/poc/docs/adr/2025-11-19-revised-di-service-registration.md`
- **Problem Analysis**: `/research/design-revision-2025-11/notes/analysis.md`

## Recommendation

✅ **Proceed to implementation** using this revised design. Supersede implementation issue #475 from the previous research.

**This revision resolves all six concerns identified during implementation attempts.**
