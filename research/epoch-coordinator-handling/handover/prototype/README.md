# Prototype: Graph-Owned Coordinator

This directory contains the working prototype code from the EpochCoordinator research.

## Purpose

This prototype demonstrates that moving coordinator ownership from `EpochSourceNode` to `DataFlowGraph` successfully:
1. Removes dead code (unused coordinator in node)
2. Provides per-graph isolation
3. Maintains multi-source coordination
4. Simplifies the API

## Key Files

### Core Changes

1. **EpochSourceNode.cs**
   - Removed `_coordinator` field
   - Removed `coordinator` constructor parameter  
   - Removed `Coordinator` property
   - Node now only manages epoch channel (its actual responsibility)

2. **DataFlowGraph.cs**
   - Added `_epochCoordinator` field
   - Added `EpochCoordinator` property to expose coordinator
   - Added `SetEpochCoordinator()` method for builder

3. **DataFlowGraphBuilder.cs**
   - Added `_epochCoordinator` field
   - Added `SetEpochCoordinator()` method
   - Updated `Build()` to pass coordinator to graph

4. **EpochConfigurationExtensions.cs**
   - Updated `ConfigureEpochs` to store coordinator in builder
   - Changed `EpochSourceNode` creation to not pass coordinator

### Validation Tests

5. **GraphOwnedCoordinatorPrototypeTests.cs**
   - Tests demonstrating graph-owned coordinator works
   - Per-graph isolation validation
   - Backward compatibility validation

## How to Use This Prototype

### For Implementation Team

These files show the exact changes needed:

1. **Copy changes from**:
   - `EpochSourceNode.cs` → Remove coordinator storage
   - `DataFlowGraph.cs` → Add coordinator ownership
   - `DataFlowGraphBuilder.cs` → Add coordinator management
   - `EpochConfigurationExtensions.cs` → Update to use builder

2. **Update tests**:
   - Remove coordinator parameter from `EpochSourceNode` constructor calls
   - 12 test locations need updating (see research README)

3. **Validate**:
   - Run tests from `GraphOwnedCoordinatorPrototypeTests.cs`
   - Verify multi-source coordination still works
   - Verify multi-graph isolation

## Testing the Prototype

The prototype validates these key scenarios:

1. ✅ `EpochSourceNode` can be created without coordinator parameter
2. ✅ Graph exposes coordinator when epochs configured
3. ✅ Graph has null coordinator when epochs not configured
4. ✅ Multiple graphs have independent coordinator instances
5. ✅ `ConfigureEpochs` works without factory (uses default)
6. ✅ `ConfigureEpochs` still accepts factory (backward compatibility)

## API Comparison

### Before (Current)

```csharp
// Test setup - manual coordinator management
private readonly EpochCoordinator _coordinator;

public TestClass()
{
    _coordinator = new EpochCoordinator(...);
}

// Usage - awkward factory parameter
builder.ConfigureEpochs(
    config => { /* ... */ },
    _ => _coordinator  // ← Ignored but required
);
```

### After (Prototype)

```csharp
// Test setup - no manual coordinator needed

// Usage - clean API
builder.ConfigureEpochs(config => { /* ... */ });

// Coordinator accessible from graph if needed
var coordinator = graph.EpochCoordinator;
```

## Performance Impact

**None** - The changes are purely architectural:
- Same coordinator instance created
- Same initialization path  
- No additional allocations
- No performance overhead

## Breaking Changes

**None** - The factory parameter in `ConfigureEpochs` is still accepted for backward compatibility.

## Migration Path

Existing code continues to work as-is. New code can optionally omit the factory parameter for cleaner API.

## Related Documentation

- **Research README**: `/research/epoch-coordinator-handling/README.md`
- **Design Document**: `/research/epoch-coordinator-handling/design/graph-owned-coordinator.md`
- **Code Analysis**: `/research/epoch-coordinator-handling/notes/01-code-analysis.md`
- **DI Scoping Analysis**: `/research/epoch-coordinator-handling/notes/02-di-scoping-analysis.md`
