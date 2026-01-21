# Current Architecture Analysis

## Date: 2026-01-20

## Execution Context Architecture

### 1. IExecutionContext (Graph-level)

**Location**: `/poc/DataFlow/Core/IExecutionContext.cs`

```csharp
public interface IExecutionContext
{
    CancellationToken CancellationToken { get; }
    IServiceProvider ServiceProvider { get; }
    Guid InvocationId { get; }
    ICheckpoint? RecoveryCheckpoint { get; }
    IDataFlowMetrics? Metrics { get; }
}
```

**Usage**:
- Passed to `graph.ExecuteAsync(IExecutionContext context)`
- Available to blocks via `ExecuteAsync(input, context)` method
- Propagated throughout the graph execution

**AsyncLocal Support**:
```csharp
public class ExecutionContext : IExecutionContext
{
    private static readonly AsyncLocal<ExecutionContext?> _current = new();
    
    public static ExecutionContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
```

### 2. IActorExecutionContext (Actor-level)

**Location**: `/poc/DataFlow/Core/IActorExecutionContext.cs`

```csharp
public interface IActorExecutionContext
{
    CancellationToken CancellationToken { get; }
    Guid InvocationId { get; }
    void RequestRotation();
    IEpochCoordinator? EpochCoordinator { get; }
}
```

**Usage**:
- Passed to actors: `IStreamActor<TIn,TOut>.RunAsync(input, IActorExecutionContext context)`
- Passed to source actors: `ISourceActor<T>.ProduceEpochsAsync(IActorExecutionContext context)`
- Provides actor-specific functionality (rotation, epoch coordination)

**Note**: This is a DIFFERENT context than IExecutionContext - it has subset of data plus actor-specific features.

### 3. BlockContext (Constructor-time)

**Usage**: Injected into block constructors via DI
- Provides graph-specific configuration at construction time
- Not execution-specific

## Key Findings

### Actor Context Access

**Question**: Can `IStreamActor<TIn,TOut>` obtain the execution context?

**Answer**: Partially
- Actors receive `IActorExecutionContext` (not `IExecutionContext`)
- `IActorExecutionContext` has: CancellationToken, InvocationId, RequestRotation(), EpochCoordinator
- `IActorExecutionContext` does NOT have: ServiceProvider, RecoveryCheckpoint, Metrics
- Actors CANNOT access the full `IExecutionContext` directly

### Context Propagation

**IExecutionContext → IActorExecutionContext mapping**:

Looking at `ActorExecutionContext.Reset()`:
```csharp
internal void Reset(
    CancellationToken cancellationToken,
    Guid invocationId,
    Action requestRotation,
    IEpochCoordinator? epochCoordinator = null)
```

- CancellationToken: ✅ Propagated from IExecutionContext
- InvocationId: ✅ Propagated from IExecutionContext  
- ServiceProvider: ❌ NOT propagated to actors
- RecoveryCheckpoint: ❌ NOT propagated to actors
- Metrics: ❌ NOT propagated to actors

**Implication**: Actors have limited context - they can't access ServiceProvider, which would prevent them from accessing trigger context stored in DI.

### AsyncLocal Mechanism

**Current Usage**: `ExecutionContext.Current` uses AsyncLocal for ambient access

**Robustness Questions**:
1. Does it propagate across `Task.Run()` boundaries? (Comment says it may not be reliable)
2. What's the performance cost?
3. Is it used in the hot path?

**Finding from code comments**:
```csharp
/// <remarks>
/// Note: AsyncLocal propagation may not work reliably across Task.Run() boundaries
/// in some scenarios. This is what we're testing.
/// </remarks>
```

This suggests AsyncLocal robustness is already a concern!

## Current Execution Flow

1. **Graph Start**: 
   ```csharp
   await graph.ExecuteAsync(IExecutionContext context)
   ```

2. **Block Execution**:
   ```csharp
   // In DataFlowGraph.ExecutionPipeline.BlockRuntimeModel.ExecuteAsync()
   var typedOutput = await adapter.ExecuteUntypedAsync(typedInput, context);
   ```
   - Each block receives full IExecutionContext

3. **Actor Invocation** (for ActorBlocks):
   ```csharp
   // In EpochActorBlock
   var actorOutput = actor.RunAsync(actorInput, _context);
   ```
   - Actors receive IActorExecutionContext (subset)

4. **Source Actor Invocation**:
   ```csharp
   // In EpochSourceBlock
   await foreach (var epochStream in _actor.ProduceEpochsAsync(_context))
   ```
   - Source actors also receive IActorExecutionContext

## Gaps Identified

1. **No trigger context storage**: IExecutionContext doesn't have a place for arbitrary trigger data
2. **Actor isolation**: Actors can't access ServiceProvider, limiting DI-based approaches
3. **AsyncLocal reliability**: Already noted as potentially unreliable
4. **Type safety**: No type-safe way to pass trigger-specific data

## Next Steps

Based on this analysis, I need to:
1. Document all possible approaches (extend IExecutionContext, DI, AsyncLocal, stream item)
2. Create prototypes for most promising approaches
3. Benchmark performance implications
4. Validate AsyncLocal reliability in dataflow context
