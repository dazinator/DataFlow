# Prototype Code

This directory contains the working prototype from research validation.

## Purpose

Demonstrates the viability of moving `IServiceProvider` from `DataFlowGraphBuilder` constructor to `Build()` method.

## Key Files

### DataFlowGraphBuilder.cs
**Changes**:
- Removed `IServiceProvider` and `IBlockTypeRegistry` from constructor
- Removed obsolete constructor
- Changed `UseBlock()` to store block names for deferred resolution
- Changed `SetEpochCoordinator()` to `SetEpochConfiguration()` for deferred coordinator creation
- Updated `Build()` to accept `IServiceProvider` and `IBlockTypeRegistry` parameters
- Added logic to resolve blocks and create coordinator in `Build()`

### EpochConfigurationExtensions.cs
**Changes**:
- Removed `GetServiceProvider()` call
- Changed to call `SetEpochConfiguration()` instead of creating coordinator immediately
- Updated XML documentation

### ServiceCollectionExtensions.cs
**Changes**:
- Updated `AddGraph()` to pass service provider and registry to `Build()`
- Updated `AddGraphDefinition()` to pass service provider and registry to `Build()`

### GraphHelpers.cs (Test Utility)
**Changes**:
- Updated `CreateGraphBuilder()` to use new constructor
- Updated `CreateGraph()` to pass service provider and registry to `Build()`

## Build Status

✅ **Core Library**: Builds successfully without errors

⚠️ **Test Library**: ~100+ test sites need mechanical updates to pass `Build()` parameters

## How to Use

### For Implementation Team

1. Review the changes in each file
2. Note the patterns used for deferred resolution
3. Use as reference for production implementation
4. Update all test sites to pass `Build()` parameters

### Key Patterns

**Deferred Block Resolution**:
```csharp
// Store names during building
private readonly List<string> _pendingBlockNames = new();

public DataFlowGraphBuilder UseBlock(string name)
{
    _pendingBlockNames.Add(name);
    return this;
}

// Resolve during Build()
foreach (var blockName in _pendingBlockNames)
{
    var key = ResolveBlockKey(blockName);
    var block = registry.GetBlock(serviceProvider, key);
    graph.AddBlock(block);
}
```

**Deferred Coordinator Creation**:
```csharp
// Store configuration during ConfigureEpochs()
private EpochConfiguration? _epochConfig;
private Func<ICheckpointStrategy?, IEpochCoordinator>? _epochCoordinatorFactory;

internal void SetEpochConfiguration(
    EpochConfiguration config,
    Func<ICheckpointStrategy?, IEpochCoordinator>? coordinatorFactory)
{
    _epochConfig = config;
    _epochCoordinatorFactory = coordinatorFactory;
}

// Create coordinator during Build()
if (_epochConfig != null)
{
    var factory = _epochCoordinatorFactory ?? CreateDefaultCoordinatorFactory(serviceProvider);
    var coordinator = factory(_epochConfig.CheckpointStrategy);
    graph.SetEpochCoordinator(coordinator);
}
```

## Validation Notes

- Core library compiles without warnings
- All deferred resolution happens successfully in `Build()`
- Service registration pattern works correctly
- Test helper pattern is straightforward

## Next Steps

1. ✅ Prototype saved
2. ⬜ Revert exploratory changes from /poc/
3. ⬜ Implementation team uses this as reference
4. ⬜ Update all test sites (mechanical)
5. ⬜ Validate all tests pass
