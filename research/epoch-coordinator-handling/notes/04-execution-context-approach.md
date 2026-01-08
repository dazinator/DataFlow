# Execution Context Approach for Epoch Coordinator

**Date**: 2026-01-08  
**Phase**: Follow-up Analysis - Final Recommendation

## Summary

Based on PR feedback, the **best approach** combines:
1. Keyed services (for per-graph coordinator isolation)
2. Execution context (for passing coordinator to actors)
3. Deferred initialization (for flexible DI setup)

This approach was suggested in PR comment feedback as a resolution to contradictions between earlier recommendations.

---

## The Problem with Previous Approaches

**ActivatorUtilities approach**: Awkward manual construction
**Keyed services alone**: Actors need to know graph ID
**New containers**: Breaks DI chain (not acceptable)

**Root issue**: How to get per-graph coordinator to actors that are resolved from application DI?

---

## The Solution: Execution Context

### Key Insight

`IActorExecutionContext` is already passed to every actor. It's the perfect channel to provide the coordinator!

```csharp
public interface ISourceActor<T>
{
    // Context already passed here
    IAsyncEnumerable<IEpochStream<T>> ProduceEpochsAsync(IActorExecutionContext context);
}
```

### Architecture

```
DataFlowGraph
  └─ Has GraphId
  └─ EpochSourceBlock (knows GraphId)
       └─ Resolves IEpochCoordinator via keyed service (using GraphId)
       └─ Passes coordinator through ActorExecutionContext
       └─ Resolves TActor from application DI
            └─ Actor gets coordinator from context.EpochCoordinator
```

---

## Implementation Details

### Step 1: Extend IActorExecutionContext

Add coordinator property to the execution context interface:

```csharp
namespace DataFlow.POC.Core;

public interface IActorExecutionContext
{
    CancellationToken CancellationToken { get; }
    Guid InvocationId { get; }
    void RequestRotation();
    
    /// <summary>
    /// Epoch coordinator for this execution context.
    /// Available for epoch-aware source actors.
    /// </summary>
    IEpochCoordinator? EpochCoordinator { get; }
}
```

### Step 2: Update ActorExecutionContext

```csharp
internal sealed class ActorExecutionContext : IActorExecutionContext
{
    private CancellationToken _cancellationToken;
    private Guid _invocationId;
    private Action? _requestRotation;
    private IEpochCoordinator? _epochCoordinator;
    
    public CancellationToken CancellationToken => _cancellationToken;
    public Guid InvocationId => _invocationId;
    public IEpochCoordinator? EpochCoordinator => _epochCoordinator;
    
    public void RequestRotation() => _requestRotation?.Invoke();
    
    internal void Reset(
        CancellationToken cancellationToken,
        Guid invocationId,
        Action requestRotation,
        IEpochCoordinator? epochCoordinator = null)
    {
        _cancellationToken = cancellationToken;
        _invocationId = invocationId;
        _requestRotation = requestRotation;
        _epochCoordinator = epochCoordinator;
    }
}
```

### Step 3: EpochSourceBlock Resolves and Passes Coordinator

```csharp
public sealed class EpochSourceBlock<T, TActor> : BlockBase<object, IEpochStream<T>>
    where TActor : ISourceActor<T>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ActorExecutionContext _context = new();
    private readonly string _graphId;
    
    public EpochSourceBlock(
        IBlockContext context, 
        IServiceScopeFactory scopeFactory,
        string graphId)
        : base(context)
    {
        _scopeFactory = scopeFactory;
        _graphId = graphId;
    }
    
    public override async IAsyncEnumerable<IEpochStream<T>> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        
        // Resolve coordinator using graph-specific keyed service
        var coordinator = scope.ServiceProvider
            .GetRequiredKeyedService<IEpochCoordinator>(_graphId);
        
        // Initialize context WITH coordinator
        InitializeActorContext(context, coordinator);
        
        // Resolve actor normally from application DI
        var actor = scope.ServiceProvider.GetRequiredService<TActor>();
        
        await foreach (var epochStream in actor.ProduceEpochsAsync(_context)
            .WithCancellation(context.CancellationToken))
        {
            yield return epochStream;
        }
    }
    
    private void InitializeActorContext(
        IExecutionContext context, 
        IEpochCoordinator coordinator)
    {
        _context.Reset(
            context.CancellationToken,
            context.InvocationId,
            () => { },
            coordinator);
    }
}
```

### Step 4: Simplify SourceActorBase

Actor constructor no longer needs coordinator parameter:

```csharp
public abstract class SourceActorBase<T> : ISourceActor<T>
{
    private readonly string _sourceId;
    
    // Simplified constructor - no coordinator!
    protected SourceActorBase(string sourceId)
    {
        _sourceId = sourceId ?? throw new ArgumentNullException(nameof(sourceId));
    }
    
    public abstract IAsyncEnumerable<IEpochStream<T>> ProduceEpochsAsync(
        IActorExecutionContext context);
    
    // Helper methods use coordinator from context
    protected async ValueTask<IEpochStream<T>> CreateEpochStreamAsync(
        IActorExecutionContext context,
        long sequence,
        IAsyncEnumerable<T> items,
        CancellationToken cancellationToken = default)
    {
        var coordinator = context.EpochCoordinator 
            ?? throw new InvalidOperationException(
                "Epoch coordinator not available. Ensure graph has epochs configured.");
        
        var vector = EpochVector.FromSingleSource(_sourceId, sequence);
        var epoch = await coordinator.GetOrCreateEpochAsync(
            _sourceId, vector, cancellationToken);
        
        return new EpochStream<T>(epoch, items);
    }
    
    protected void SignalReadyForNext(
        IActorExecutionContext context,
        long currentSequence, 
        long nextSequence)
    {
        var coordinator = context.EpochCoordinator 
            ?? throw new InvalidOperationException(
                "Epoch coordinator not available. Ensure graph has epochs configured.");
        
        var currentVector = EpochVector.FromSingleSource(_sourceId, currentSequence);
        var nextVector = EpochVector.FromSingleSource(_sourceId, nextSequence);
        coordinator.SignalReadyForNext(_sourceId, currentVector, nextVector);
    }
}
```

### Step 5: Register Coordinator with Keyed Services

```csharp
// During graph building
var graphId = Guid.NewGuid().ToString();

// Register coordinator with graph-specific key
services.AddKeyedSingleton<IEpochCoordinator>(
    graphId,
    (sp, key) =>
    {
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        
        // Use deferred initialization pattern
        var coordinator = new EpochCoordinator(
            operationsQueueCapacity: 100,
            checkpointStrategy: config.CheckpointStrategy);
        
        coordinator.Initialize(scopeFactory);
        return coordinator;
    });

// Store graph ID for blocks to use
graph.GraphId = graphId;
```

---

## Benefits

### 1. Uses Keyed Services ✅
Consistent with existing `DataFlowBuilder` patterns for isolation

### 2. No New Containers ✅
Actors resolved from application DI - full dependency injection support

### 3. Simplified Actor Constructors ✅
No coordinator parameter needed - gets from context instead

### 4. Natural Flow ✅
Context already passed to actors - no new patterns needed

### 5. Per-Graph Isolation ✅
Keyed services ensure coordinators don't interfere across graphs

### 6. Backward Compatible ✅
Non-epoch actors simply ignore `context.EpochCoordinator` (null for them)

### 7. Testable ✅
Easy to create test contexts with or without coordinator

### 8. Deferred Initialization ✅
Combines with deferred initialization pattern from PR feedback

---

## Comparison with Other Approaches

| Approach | Pros | Cons |
|----------|------|------|
| **New Containers** | Simple | ❌ Breaks DI chain |
| **ActivatorUtilities** | Works | ⚠️ Awkward, manual construction |
| **Keyed Services Alone** | Uses DI | ⚠️ Actors need graph ID |
| **Execution Context** ✅ | All benefits | None identified |

---

## Migration Impact

### Breaking Changes

**SourceActorBase constructor**:
```csharp
// Before
protected SourceActorBase(IEpochCoordinator coordinator, string sourceId)

// After
protected SourceActorBase(string sourceId)
```

### Helper Method Changes

**CreateEpochStreamAsync**:
```csharp
// Before
protected async ValueTask<IEpochStream<T>> CreateEpochStreamAsync(
    long sequence,
    IAsyncEnumerable<T> items,
    CancellationToken cancellationToken = default)

// After
protected async ValueTask<IEpochStream<T>> CreateEpochStreamAsync(
    IActorExecutionContext context,  // NEW parameter
    long sequence,
    IAsyncEnumerable<T> items,
    CancellationToken cancellationToken = default)
```

**SignalReadyForNext**:
```csharp
// Before
protected void SignalReadyForNext(long currentSequence, long nextSequence)

// After
protected void SignalReadyForNext(
    IActorExecutionContext context,  // NEW parameter
    long currentSequence, 
    long nextSequence)
```

### Migration Strategy

1. Add `EpochCoordinator` property to `IActorExecutionContext`
2. Update `ActorExecutionContext.Reset()` to accept coordinator
3. Update `EpochSourceBlock` to resolve coordinator via keyed service
4. Update `SourceActorBase` constructor and helper methods
5. Update all derived actors to:
   - Remove coordinator from constructor
   - Pass context to helper methods
6. Register coordinators with keyed services during graph building

---

## Example Usage

### Before

```csharp
public class MySourceActor : SourceActorBase<int>
{
    public MySourceActor(IEpochCoordinator coordinator) 
        : base(coordinator, "my-source")
    {
    }
    
    public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
        IActorExecutionContext context)
    {
        var items = ProduceItems();
        yield return await CreateEpochStreamAsync(1, items);
        
        SignalReadyForNext(1, 2);
        
        var moreItems = ProduceMoreItems();
        yield return await CreateEpochStreamAsync(2, moreItems);
    }
}
```

### After

```csharp
public class MySourceActor : SourceActorBase<int>
{
    // Simplified constructor
    public MySourceActor() : base("my-source")
    {
    }
    
    public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
        IActorExecutionContext context)
    {
        var items = ProduceItems();
        yield return await CreateEpochStreamAsync(context, 1, items);
        
        SignalReadyForNext(context, 1, 2);
        
        var moreItems = ProduceMoreItems();
        yield return await CreateEpochStreamAsync(context, 2, moreItems);
    }
}
```

---

## Recommendation

**This is the recommended approach** for implementing graph-owned coordinators because it:

1. Resolves all concerns from PR feedback
2. Uses established patterns (keyed services)
3. Maintains DI architecture integrity
4. Simplifies actor implementation
5. Provides natural coordinator flow via context

The execution context is the perfect communication channel between blocks and actors, making this solution elegant and maintainable.
