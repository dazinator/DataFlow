# DI Scoping and Coordinator Lifetime Analysis

**Date**: 2026-01-08  
**Phase**: 1 - Code Analysis

## Current Actor Instantiation Pattern

### EpochSourceBlock Pattern

```csharp
// File: poc/DataFlow/Blocks/EpochSourceBlock.cs
public sealed class EpochSourceBlock<T, TActor> : BlockBase<object, IEpochStream<T>>
    where TActor : ISourceActor<T>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public override async IAsyncEnumerable<IEpochStream<T>> ExecuteAsync(...)
    {
        // Creates a NEW scope for this source block execution
        await using var scope = _scopeFactory.CreateAsyncScope();  // ← NEW SCOPE
        
        var actor = scope.ServiceProvider.GetRequiredService<TActor>();  // ← Actor from NEW scope
        
        await foreach (var epochStream in actor.ProduceEpochsAsync(_context)...)
        {
            yield return epochStream;
        }
    }
}
```

### Key Observations

1. **Each source block creates its own DI scope** when `ExecuteAsync` is called
2. **Actor is resolved from that scope** 
3. **The scope lives for the duration of the block execution**
4. **Different source blocks = Different scopes**

## Current Coordinator Registration Pattern (from tests)

### Pattern 1: Singleton Registration

```csharp
services.AddSingleton<IEpochCoordinator>(sp => 
    new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
```

**Implications**:
- ✅ All actors on all graphs share the same coordinator instance
- ❌ **PROBLEM**: Multiple graphs will interfere with each other
- ❌ **PROBLEM**: Source IDs from different graphs could collide
- ✅ Actors on the same graph share the coordinator (coordination works)

### Pattern 2: Scoped Registration (hypothetical)

```csharp
services.AddScoped<IEpochCoordinator, EpochCoordinator>();
```

**Implications**:
- ❌ **PROBLEM**: Each `EpochSourceBlock` creates its own scope
- ❌ **PROBLEM**: Different source blocks would get **different coordinator instances**
- ❌ **FATAL**: Multi-source coordination would be **broken**

### Pattern 3: Transient Registration (hypothetical)

```csharp
services.AddTransient<IEpochCoordinator, EpochCoordinator>();
```

**Implications**:
- ❌ **FATAL**: Every actor resolution gets a new coordinator
- ❌ **FATAL**: Coordination completely broken

## The Fundamental Problem

### Issue: Mismatched Scoping Requirements

**Coordinator needs**:
- ✅ **Per-graph instance** - Each graph should have its own coordinator
- ✅ **Shared across source blocks** - All sources in a graph must share the same coordinator
- ✅ **Graph-isolated** - Different graphs must NOT interfere

**Current DI capabilities**:
- Singleton: ❌ Not per-graph (shared across all graphs)
- Scoped: ❌ Not shared across source blocks (each block creates own scope)
- Transient: ❌ Not shared at all

**Conclusion**: Standard DI lifetimes don't match our needs!

## Alternative Approaches

### Approach A: Graph-Owned Coordinator

**Concept**: The `DataFlowGraph` creates and owns the coordinator

```csharp
public class DataFlowGraph
{
    private readonly IEpochCoordinator _coordinator;  // ← Graph owns coordinator
    
    internal DataFlowGraph(..., IEpochCoordinator coordinator)
    {
        _coordinator = coordinator;
        // ...
    }
    
    // Expose to blocks that need it
    public IEpochCoordinator EpochCoordinator => _coordinator;
}
```

**How actors get it**:
```csharp
public sealed class EpochSourceBlock<T, TActor> : BlockBase<object, IEpochStream<T>>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEpochCoordinator _coordinator;  // ← Injected from graph
    
    public EpochSourceBlock(IBlockContext context, IServiceScopeFactory scopeFactory, IEpochCoordinator coordinator)
        : base(context)
    {
        _scopeFactory = scopeFactory;
        _coordinator = coordinator;  // ← From graph
    }
    
    public override async IAsyncEnumerable<IEpochStream<T>> ExecuteAsync(...)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        
        // Register coordinator in this scope so actor can resolve it
        var services = new ServiceCollection();
        services.AddSingleton(_coordinator);  // ← Add graph's coordinator to scope
        var scopedProvider = services.BuildServiceProvider();
        
        var actor = scopedProvider.GetRequiredService<TActor>();
        // ...
    }
}
```

**Pros**:
- ✅ Per-graph coordinator (perfect isolation)
- ✅ Shared across all source blocks in the graph
- ✅ Clear ownership (graph owns coordinator)

**Cons**:
- ⚠️ Requires passing coordinator through block constructors
- ⚠️ Actors can't just resolve `IEpochCoordinator` from standard DI
- ⚠️ Need to augment scope with graph-scoped services

### Approach B: Keyed Services (DI Extension)

**Concept**: Use keyed services with graph ID as key

```csharp
// During graph building
services.AddKeyedSingleton<IEpochCoordinator>(
    graphId,  // ← Key by graph ID
    (sp, key) => new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>())
);

// In actor resolution
var coordinator = scope.ServiceProvider.GetRequiredKeyedService<IEpochCoordinator>(graphId);
```

**Pros**:
- ✅ Per-graph coordinator
- ✅ Uses standard DI mechanisms

**Cons**:
- ❌ Requires .NET 8+ keyed services
- ❌ Actors need to know graph ID
- ❌ More complex registration

### Approach C: Builder Creates and Injects

**Concept**: `DataFlowGraphBuilder` creates coordinator and injects into blocks during `UseBlock`

```csharp
public class DataFlowGraphBuilder
{
    private IEpochCoordinator? _graphCoordinator;
    
    public DataFlowGraphBuilder ConfigureEpochs(...)
    {
        // Create coordinator owned by this builder/graph
        _graphCoordinator = coordinatorFactory(...);
        // ...
    }
    
    public DataFlowGraphBuilder UseBlock(string name)
    {
        var block = _registry.GetBlock(_serviceProvider, name);
        
        // If block needs coordinator, inject it
        if (block is IRequiresEpochCoordinator requiresCoordinator)
        {
            requiresCoordinator.SetCoordinator(_graphCoordinator);
        }
        
        // ...
    }
}
```

**Pros**:
- ✅ Per-graph coordinator
- ✅ Builder manages lifecycle
- ✅ Clean separation

**Cons**:
- ⚠️ Requires interface for blocks that need coordinator
- ⚠️ Breaks pure DI resolution

### Approach D: Graph-Scoped Service Provider

**Concept**: Create a graph-specific service provider that includes the coordinator

```csharp
public static class DataFlowGraphBuilderExtensions
{
    public static DataFlowGraphBuilder ConfigureEpochs(...)
    {
        // Create coordinator for this graph
        var coordinator = coordinatorFactory(...);
        
        // Create graph-specific service collection
        var graphServices = new ServiceCollection();
        graphServices.AddSingleton(coordinator);  // ← Graph-specific coordinator
        
        // Build graph-specific provider that delegates to root provider
        var graphProvider = new DelegatingServiceProvider(
            rootProvider: builder.GetServiceProvider(),
            graphServices.BuildServiceProvider()
        );
        
        // Store graph provider in builder
        builder.SetGraphServiceProvider(graphProvider);
        
        // ...
    }
}
```

**Pros**:
- ✅ Per-graph coordinator
- ✅ Actors resolve coordinator normally from DI
- ✅ Clean isolation

**Cons**:
- ⚠️ Requires custom service provider implementation
- ⚠️ Complex delegating provider logic
- ⚠️ May have performance implications

## Recommended Approach

**Approach A (Graph-Owned Coordinator)** seems most pragmatic because:

1. **Clear Ownership**: Graph explicitly owns its coordinator
2. **Explicit Dependencies**: Blocks that need coordinator explicitly request it
3. **No Magic**: No custom DI plumbing required
4. **Testable**: Easy to test with explicit coordinator injection
5. **Backward Compatible**: Can coexist with current patterns

## Implementation Strategy for Approach A

### Step 1: Remove Coordinator from EpochSourceNode

```csharp
public sealed class EpochSourceNode
{
    // Remove _coordinator field
    // Remove Coordinator property
    // Just manage the epoch channel
}
```

### Step 2: Add Coordinator to DataFlowGraph

```csharp
public class DataFlowGraph
{
    private readonly IEpochCoordinator? _epochCoordinator;
    
    public IEpochCoordinator? EpochCoordinator => _epochCoordinator;
    
    internal void SetEpochCoordinator(IEpochCoordinator coordinator)
    {
        _epochCoordinator = coordinator;
    }
}
```

### Step 3: Update ConfigureEpochs

```csharp
public static DataFlowGraphBuilder ConfigureEpochs(...)
{
    var coordinator = coordinatorFactory(...);
    
    // Store in graph, not in EpochSourceNode
    builder.SetEpochCoordinator(coordinator);
    
    var sourceNode = new EpochSourceNode();  // ← No coordinator parameter
    builder.SetEpochSource(sourceNode);
    
    // ...
}
```

### Step 4: Update EpochSourceBlock

```csharp
public sealed class EpochSourceBlock<T, TActor> : BlockBase<object, IEpochStream<T>>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Func<IEpochCoordinator> _coordinatorAccessor;
    
    public EpochSourceBlock(
        IBlockContext context,
        IServiceScopeFactory scopeFactory,
        Func<IEpochCoordinator> coordinatorAccessor)  // ← Accessor from graph
        : base(context)
    {
        _scopeFactory = scopeFactory;
        _coordinatorAccessor = coordinatorAccessor;
    }
    
    public override async IAsyncEnumerable<IEpochStream<T>> ExecuteAsync(...)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        
        // Create augmented service provider with coordinator
        var coordinator = _coordinatorAccessor();
        var augmentedProvider = CreateAugmentedProvider(scope.ServiceProvider, coordinator);
        
        var actor = augmentedProvider.GetRequiredService<TActor>();
        // ...
    }
    
    private IServiceProvider CreateAugmentedProvider(IServiceProvider baseProvider, IEpochCoordinator coordinator)
    {
        // Wrap provider to add coordinator
        return new AugmentedServiceProvider(baseProvider, coordinator);
    }
}
```

## Next Steps

1. Prototype Approach A with minimal code changes
2. Validate multi-source coordination still works
3. Validate graph isolation
4. Measure complexity vs benefit
