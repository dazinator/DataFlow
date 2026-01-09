# Prototype Approach: Graph-Owned Coordinator

**Date**: 2026-01-08  
**Phase**: 2 - Prototyping

## Approach Overview

**Core Concept**: The `DataFlowGraph` creates and owns the coordinator, eliminating the need for `EpochSourceNode` to store it.

## Key Changes

### Change 1: Remove Coordinator from EpochSourceNode

**Before**:
```csharp
public sealed class EpochSourceNode
{
    private readonly IEpochCoordinator _coordinator;  // ← REMOVE
    
    public EpochSourceNode(IEpochCoordinator coordinator)  // ← REMOVE PARAMETER
    {
        _coordinator = coordinator ?? throw...;
        _epochStream = Channel.CreateUnbounded<IEpoch>(...);
    }
    
    public IEpochCoordinator Coordinator => _coordinator;  // ← REMOVE
}
```

**After**:
```csharp
public sealed class EpochSourceNode
{
    // No coordinator field
    
    public EpochSourceNode()  // ← No parameters
    {
        _epochStream = Channel.CreateUnbounded<IEpoch>(...);
    }
    
    // No Coordinator property
}
```

**Rationale**: The node never uses the coordinator, so it shouldn't store it.

### Change 2: Graph Owns and Exposes Coordinator

**Before**:
```csharp
public class DataFlowGraph
{
    private EpochSourceNode? _epochSource;
    
    public EpochSourceNode? EpochSource => _epochSource;
}
```

**After**:
```csharp
public class DataFlowGraph
{
    private EpochSourceNode? _epochSource;
    private IEpochCoordinator? _epochCoordinator;  // ← ADD
    
    public EpochSourceNode? EpochSource => _epochSource;
    public IEpochCoordinator? EpochCoordinator => _epochCoordinator;  // ← ADD
    
    internal void SetEpochCoordinator(IEpochCoordinator coordinator)  // ← ADD
    {
        _epochCoordinator = coordinator;
    }
}
```

**Rationale**: Graph is the natural owner - it has the right lifetime scope.

### Change 3: Update ConfigureEpochs

**Before**:
```csharp
public static DataFlowGraphBuilder ConfigureEpochs(...)
{
    var coordinator = coordinatorFactory(config.CheckpointStrategy);
    
    var sourceNode = new EpochSourceNode(coordinator);  // ← Pass coordinator
    builder.SetEpochSource(sourceNode);
    // ...
}
```

**After**:
```csharp
public static DataFlowGraphBuilder ConfigureEpochs(...)
{
    var coordinator = coordinatorFactory(config.CheckpointStrategy);
    
    // Store coordinator in builder (will go to graph)
    builder.SetEpochCoordinator(coordinator);  // ← NEW
    
    var sourceNode = new EpochSourceNode();  // ← No coordinator parameter
    builder.SetEpochSource(sourceNode);
    // ...
}
```

**Rationale**: Coordinator goes to graph, not to node.

### Change 4: Builder Stores Coordinator

**Before**:
```csharp
public class DataFlowGraphBuilder
{
    private EpochSourceNode? _epochSource;
    // No coordinator field
}
```

**After**:
```csharp
public class DataFlowGraphBuilder
{
    private EpochSourceNode? _epochSource;
    private IEpochCoordinator? _epochCoordinator;  // ← ADD
    
    internal void SetEpochCoordinator(IEpochCoordinator coordinator)  // ← ADD
    {
        _epochCoordinator = coordinator;
    }
    
    public DataFlowGraph Build()
    {
        var graph = new DataFlowGraph(...);
        if (_epochCoordinator != null)
        {
            graph.SetEpochCoordinator(_epochCoordinator);  // ← ADD
        }
        // ...
        return graph;
    }
}
```

**Rationale**: Builder needs to pass coordinator to graph during Build().

## API Impact

### Before (Current - Awkward)

```csharp
// Test must manually create and manage coordinator
private readonly EpochCoordinator _coordinator;

public TestClass()
{
    _coordinator = new EpochCoordinator(_serviceProvider.GetRequiredService<IServiceScopeFactory>());
}

[Fact]
public void Test()
{
    var builder = GraphHelpers.CreateGraphBuilder("test");
    
    builder.ConfigureEpochs(config =>
    {
        config.SetPolicy(EpochPolicy.ByCount(100));
        config.AddProcessor("processor1");
    }, _ => _coordinator);  // ← Awkward - parameter ignored but required
    
    var graph = builder.Build();
    // ...
}
```

### After (Proposed - Clean)

```csharp
// No manual coordinator management needed

[Fact]
public void Test()
{
    var builder = GraphHelpers.CreateGraphBuilder("test");
    
    builder.ConfigureEpochs(config =>
    {
        config.SetPolicy(EpochPolicy.ByCount(100));
        config.AddProcessor("processor1");
    });  // ← Clean - factory is optional, default used
    
    var graph = builder.Build();
    
    // Coordinator is accessible from graph if needed
    var coordinator = graph.EpochCoordinator;
    Assert.NotNull(coordinator);
}
```

## Actor Resolution Strategy

**Problem**: Actors need the coordinator, but they're resolved in a per-block DI scope.

**Solution**: Two options:

### Option A: Pass Coordinator to Block Constructor

```csharp
public sealed class EpochSourceBlock<T, TActor> : BlockBase<object, IEpochStream<T>>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEpochCoordinator _coordinator;  // ← ADD
    
    public EpochSourceBlock(
        IBlockContext context,
        IServiceScopeFactory scopeFactory,
        IEpochCoordinator coordinator)  // ← ADD PARAMETER
        : base(context)
    {
        _scopeFactory = scopeFactory;
        _coordinator = coordinator;
    }
    
    public override async IAsyncEnumerable<IEpochStream<T>> ExecuteAsync(...)
    {
        // Create augmented scope that includes coordinator
        await using var scope = _scopeFactory.CreateAsyncScope();
        var augmentedProvider = CreateProviderWithCoordinator(scope.ServiceProvider, _coordinator);
        
        var actor = augmentedProvider.GetRequiredService<TActor>();
        // Actor can now resolve IEpochCoordinator from DI
        // ...
    }
}
```

**Pros**:
- ✅ Explicit dependency
- ✅ Testable
- ✅ Clear data flow

**Cons**:
- ⚠️ Requires augmented service provider
- ⚠️ More complex block constructor

### Option B: Actors Resolve from Graph

```csharp
// Actor base class accepts graph instead of coordinator
protected SourceActorBase(IDataFlowGraph graph, string sourceId)
{
    _coordinator = graph.EpochCoordinator ?? throw...;
    _sourceId = sourceId;
}
```

**Pros**:
- ✅ Simple
- ✅ No augmented provider needed

**Cons**:
- ❌ Couples actors to graph interface
- ❌ Breaks existing actor API

**Decision**: Use Option A - more explicit and maintainable.

## Testing Strategy

### Test 1: Coordinator is Not Required by EpochSourceNode

```csharp
[Fact]
public void EpochSourceNode_CanBeCreated_WithoutCoordinator()
{
    // Should not throw
    var node = new EpochSourceNode();
    Assert.NotNull(node);
}
```

### Test 2: Graph Exposes Coordinator

```csharp
[Fact]
public void Graph_ExposesCoordinator_WhenConfigured()
{
    var builder = GraphHelpers.CreateGraphBuilder("test");
    builder.ConfigureEpochs(config =>
    {
        config.SetPolicy(EpochPolicy.ByCount(100));
        config.AddProcessor("processor1");
    });
    
    var graph = builder.Build();
    
    Assert.NotNull(graph.EpochCoordinator);
}
```

### Test 3: Multiple Graphs Have Independent Coordinators

```csharp
[Fact]
public void MultipleGraphs_HaveIndependentCoordinators()
{
    var builder1 = GraphHelpers.CreateGraphBuilder("graph1");
    builder1.ConfigureEpochs(config => { /* ... */ });
    var graph1 = builder1.Build();
    
    var builder2 = GraphHelpers.CreateGraphBuilder("graph2");
    builder2.ConfigureEpochs(config => { /* ... */ });
    var graph2 = builder2.Build();
    
    // Different coordinator instances
    Assert.NotNull(graph1.EpochCoordinator);
    Assert.NotNull(graph2.EpochCoordinator);
    Assert.NotSame(graph1.EpochCoordinator, graph2.EpochCoordinator);
}
```

### Test 4: Multi-Source Coordination Still Works

```csharp
[Fact]
public async Task MultiSource_Coordination_StillWorks()
{
    var builder = GraphHelpers.CreateGraphBuilder("test");
    builder.ConfigureEpochs(config =>
    {
        config.SetPolicy(EpochPolicy.ByCount(100));
        config.AddProcessor("processor1");
    });
    
    // Add multiple source blocks
    builder.UseBlock("source1");
    builder.UseBlock("source2");
    
    var graph = builder.Build();
    
    // Execute and verify epochs are coordinated
    // (source1 and source2 should share epochs via graph's coordinator)
    // ...
}
```

## Migration Path

### For Existing Code

**No breaking changes required** - the factory function is still supported:

```csharp
// Old code continues to work
builder.ConfigureEpochs(
    config => { /* ... */ },
    _ => customCoordinator  // ← Still accepted
);
```

**But simpler code is now possible**:

```csharp
// New simplified code
builder.ConfigureEpochs(config => { /* ... */ });  // ← Factory optional
```

### For Tests

Tests can stop creating and managing coordinators manually:

**Before**:
```csharp
private readonly EpochCoordinator _coordinator;
public TestClass() { _coordinator = new EpochCoordinator(...); }
```

**After**:
```csharp
// No manual coordinator needed - graph manages it
```

## Implementation Checklist

- [ ] Remove `_coordinator` field from `EpochSourceNode`
- [ ] Remove `coordinator` parameter from `EpochSourceNode` constructor
- [ ] Remove `Coordinator` property from `EpochSourceNode`
- [ ] Add `_epochCoordinator` field to `DataFlowGraph`
- [ ] Add `EpochCoordinator` property to `DataFlowGraph`
- [ ] Add `SetEpochCoordinator()` method to `DataFlowGraph`
- [ ] Add `_epochCoordinator` field to `DataFlowGraphBuilder`
- [ ] Add `SetEpochCoordinator()` method to `DataFlowGraphBuilder`
- [ ] Update `DataFlowGraphBuilder.Build()` to pass coordinator to graph
- [ ] Update `ConfigureEpochs` to call `builder.SetEpochCoordinator()`
- [ ] Update `ConfigureEpochs` to create `EpochSourceNode` without coordinator
- [ ] Create `AugmentedServiceProvider` helper for blocks
- [ ] Update `EpochSourceBlock` to accept coordinator in constructor
- [ ] Update all tests to use new API
- [ ] Validate multi-source coordination
- [ ] Validate multi-graph isolation

## Next Steps

1. Implement the prototype changes
2. Run existing tests to check for breaks
3. Add new tests for validation
4. Document findings
