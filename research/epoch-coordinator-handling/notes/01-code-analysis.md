# Code Analysis: EpochCoordinator Usage

**Date**: 2026-01-08  
**Phase**: 1 - Code Analysis

## Key Finding #1: EpochSourceNode.Coordinator is Never Used

### Evidence

After comprehensive search of the codebase:

```bash
# Search for any uses of .Coordinator property from EpochSourceNode
grep -rn "sourceNode\.Coordinator\|epochSource\.Coordinator\|_epochSource\.Coordinator" poc/
# Result: NO MATCHES
```

```bash
# Check EpochSourceNode implementation
# File: poc/DataFlow/Core/EpochSourceNode.cs
```

The `EpochSourceNode` class:
1. **Stores** the coordinator in a private field `_coordinator` (line 13)
2. **Exposes** it via a public property `Coordinator` (line 61)
3. **Never uses it internally** - The node only:
   - Manages a channel for epoch streaming
   - Publishes epochs to the channel
   - Signals completion

### Code Structure

```csharp
public sealed class EpochSourceNode
{
    private readonly IEpochCoordinator _coordinator;  // ← Stored but unused
    private readonly Channel<IEpoch> _epochStream;

    public EpochSourceNode(IEpochCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw...;
        
        _epochStream = Channel.CreateUnbounded<IEpoch>(...);
        // ← Note: coordinator is NOT used here
    }

    public ChannelReader<IEpoch> EpochReader => _epochStream.Reader;
    
    public async Task PublishEpochAsync(IEpoch epoch) { ... }
    
    public void SignalCompletion() { ... }
    
    public IEpochCoordinator Coordinator => _coordinator;  // ← Only exposed, never used
}
```

## Key Finding #2: Coordinator is Used by SourceActorBase

### Evidence

```csharp
// File: poc/DataFlow/Core/ISourceActor.cs
public abstract class SourceActorBase<T> : ISourceActor<T>
{
    private readonly IEpochCoordinator _coordinator;  // ← ACTUALLY USED
    private readonly string _sourceId;

    protected SourceActorBase(IEpochCoordinator coordinator, string sourceId)
    {
        _coordinator = coordinator ?? throw...;
        _sourceId = sourceId ?? throw...;
    }

    // Uses coordinator here ↓
    protected async ValueTask<IEpochStream<T>> CreateEpochStreamAsync(
        long sequence,
        IAsyncEnumerable<T> items,
        CancellationToken cancellationToken = default)
    {
        var vector = EpochVector.FromSingleSource(_sourceId, sequence);
        var epoch = await _coordinator.GetOrCreateEpochAsync(_sourceId, vector, cancellationToken);
        return new EpochStream<T>(epoch, items);
    }

    // Uses coordinator here ↓
    protected void SignalReadyForNext(long currentSequence, long nextSequence)
    {
        var currentVector = EpochVector.FromSingleSource(_sourceId, currentSequence);
        var nextVector = EpochVector.FromSingleSource(_sourceId, nextSequence);
        _coordinator.SignalReadyForNext(_sourceId, currentVector, nextVector);
    }
}
```

**Key Insight**: The `SourceActorBase` uses the coordinator for:
1. Creating coordinated epochs via `GetOrCreateEpochAsync`
2. Signaling readiness for next epoch via `SignalReadyForNext`

## Key Finding #3: ConfigureEpochs Creates Coordinator but Passes to Unused Node

### Evidence

```csharp
// File: poc/DataFlow/Builder/EpochConfigurationExtensions.cs
public static DataFlowGraphBuilder ConfigureEpochs(...)
{
    // ... create config ...
    
    // Create coordinator
    var coordinator = coordinatorFactory(config.CheckpointStrategy);  // ← Created
    
    // Pass to EpochSourceNode (which doesn't use it)
    var sourceNode = new EpochSourceNode(coordinator);  // ← Passed but unused
    builder.SetEpochSource(sourceNode);
    
    // Create epoch processor nodes
    foreach (var processorName in config.Processors)
    {
        var processor = new EpochProcessorNode(sourceNode, config.Hooks);
        builder.AddEpochProcessor(processor);
    }
    
    return builder;
}
```

**Problem**: The coordinator is created and passed to `EpochSourceNode`, but:
- `EpochSourceNode` doesn't use it
- `SourceActorBase` (which needs it) gets it from **DI injection**
- There's no connection between the coordinator passed to `EpochSourceNode` and the one injected into actors

## Key Finding #4: Current Coordinator Injection Pattern

### Evidence from Tests

```csharp
// Pattern 1: Manual registration (from tests)
services.AddSingleton<IEpochCoordinator>(sp => 
    new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
    
// Then actors resolve it
var actor = new TestSourceActor(
    sp.GetRequiredService<IEpochCoordinator>(),  // ← From DI
    "source1"
);
```

```csharp
// Pattern 2: ConfigureEpochs with factory (current API)
builder.ConfigureEpochs(
    config => { ... },
    _ => _coordinator  // ← Factory creates coordinator
);
// But this coordinator goes to EpochSourceNode, not to actors!
```

## Key Finding #5: The Disconnect

**The Core Problem**:

1. `ConfigureEpochs` creates a coordinator via factory
2. This coordinator is passed to `EpochSourceNode` 
3. `EpochSourceNode` stores but never uses it
4. `SourceActorBase` (which actually needs the coordinator) gets it from **DI**
5. The coordinator from step 1 and step 4 may be **different instances**

### Implications

- **Graph Isolation Issue**: If coordinator is registered as singleton in DI, multiple graphs share the same coordinator (BAD)
- **API Confusion**: The factory parameter in `ConfigureEpochs` doesn't actually configure what actors use
- **Dead Code**: `EpochSourceNode.Coordinator` property is never read

## Architectural Questions

### Q1: What was the original intent of passing coordinator to EpochSourceNode?

**Hypothesis**: Originally, the design may have intended for `EpochSourceNode` to:
- Create epochs itself, OR
- Be a source of coordinator reference for other components

**Reality**: The node is just a channel wrapper for epoch streaming. It doesn't create epochs.

### Q2: Where are epochs actually created?

**Answer**: In `SourceActorBase.CreateEpochStreamAsync()` via:
```csharp
var epoch = await _coordinator.GetOrCreateEpochAsync(_sourceId, vector, cancellationToken);
```

This happens in actor code, not in the node.

### Q3: How do actors get their coordinator currently?

**Answer**: Through DI injection in their constructor:
```csharp
protected SourceActorBase(IEpochCoordinator coordinator, string sourceId)
```

This coordinator comes from the DI container, NOT from `EpochSourceNode`.

## Conclusion

**Primary Issue**: There's a architectural disconnect where:
- `EpochSourceNode` accepts but doesn't use the coordinator
- `SourceActorBase` needs and uses the coordinator from DI
- `ConfigureEpochs` creates a coordinator but it goes to the wrong place

**Root Cause**: Design mismatch between node-based architecture and actor-based epoch creation.

## Next Steps

1. Prototype removing coordinator from `EpochSourceNode`
2. Design proper coordinator lifecycle management
3. Ensure graph-scoped coordinator instances
4. Simplify `ConfigureEpochs` API
