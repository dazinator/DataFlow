# Research Notes: Graph Execution Pattern Pain Points

**Date**: 2026-03-18  
**Context**: Follow-up questions from PR review of #143

---

## Questions from Code Review

The following questions arose from reviewing a real application's `JournalProcessingDataFlowJob` consumer class.

---

## Q1: EpochCoordinator is null on the graph — is this correct?

**Short answer**: ✅ Yes, null is expected here.

`DataFlowGraph.EpochCoordinator` is only non-null when the graph builder's `ConfigureEpochs()` extension method is called. That method enables *graph-level* epoch coordination features (custom checkpointing via `IEpochCoordinator` at the graph level).

The block-level coordinator (inside each `EpochSourceBlock`) is a separate, independently DI-resolved instance stored on the block — not on the graph. That block-level coordinator IS being injected correctly via:

```csharp
// In AddSourceBlock<T, TActor>():
var coordinator = sp.GetRequiredService<IEpochCoordinator>();
return new EpochSourceBlock<T, TActor>(context, scopeFactory, coordinator);
```

**Conclusion**: `DataFlowGraph.EpochCoordinator == null` simply means graph-level epoch config wasn't activated. The per-block coordinators are separate and are working correctly.

---

## Q2: Is IServiceProvider needed in ExecutionContext?

**Short answer**: In the current framework, `context.ServiceProvider` is **not used by any framework code** during graph execution.

Tracing all usages in `poc/DataFlow/`:
- `EpochSourceBlock` uses its own `_scopeFactory.CreateAsyncScope()` to resolve the actor
- `EpochActorBlock` uses its own `_scopeFactory.CreateAsyncScope()` to resolve the actor
- Neither reads `context.ServiceProvider`

The property exists on `IExecutionContext` as a "pass-through" for custom actors that might want to access application-level services directly. However, since actors already receive dependencies via DI injection through their own `IServiceScopeFactory`-based scope, the need for `context.ServiceProvider` is dubious.

**Note on tests**: The library tests pass a *scoped* `ServiceProvider` (`scope.ServiceProvider`, not root SP):
```csharp
using var scope = serviceProvider.CreateScope();
var context = new ExecutionContext(scope.ServiceProvider, CancellationToken.None);
```

But the application is passing the raw root `IServiceProvider`, which is the wrong pattern (see Q3).

---

## Q3: Should a DI scope be created to resolve the graph?

**Short answer**: ✅ Yes — the graph should be resolved from a **fresh scope per execution**, not from the root.

**Why this matters:**

The graph is registered as `AddKeyedScoped<DataFlowGraph>`. Both the graph AND its constituent blocks (`IBlock`) are scoped:

```csharp
_services.AddKeyedScoped<IBlock>(fullKey, ...);         // scoped
_services.AddKeyedScoped<DataFlowGraph>(fullKey, ...);  // scoped
```

The `IEpochCoordinator` is also scoped:
```csharp
services.TryAddScoped<IEpochCoordinator>(sp => new EpochCoordinator(...));
```

If you resolve the graph from the **root** `IServiceProvider`:
- The graph (and all its blocks) are instantiated once and live forever (effectively singleton)
- The `EpochCoordinator` inside each block is also root-scoped — this means epochs from previous executions **accumulate** in the coordinator and are never cleaned up

The correct pattern is:
```csharp
// Per execution: create a fresh scope
using var scope = serviceProvider.CreateScope();

// Resolve graph from scope — fresh blocks and fresh coordinator per scope
var graph = scope.ServiceProvider.GetRequiredKeyedService<DataFlowGraph>(graphKey);

var context = new ExecutionContext(
    scope.ServiceProvider,  // scoped SP, not root
    cancellationToken,
    invocationId,
    ...);

await graph.ExecuteAsync(context);
// scope is disposed here, cleaning up all block instances and coordinator
```

The library tests follow exactly this pattern (see `BlockLifetimeAndGraphReuseTests.cs:88-94`).

**The current application code has a bug**: it resolves the graph from the root `IServiceProvider`, meaning a single graph + coordinator instance is shared across all message invocations. This could cause epoch state leakage between runs.

---

## Q4: Could a service simplify graph execution?

**Short answer**: ✅ Yes — an `IDataFlowExecutor` service would eliminate the boilerplate and prevent the scoping bug described in Q3.

**Proposed interface:**
```csharp
public interface IDataFlowExecutor
{
    Task ExecuteAsync(
        string graphKey,
        ITriggerContext? triggerContext = null,
        CancellationToken cancellationToken = default);

    Task ExecuteAsync(
        string graphKey,
        Guid invocationId,
        ITriggerContext? triggerContext = null,
        ICheckpoint? recoveryCheckpoint = null,
        CancellationToken cancellationToken = default);
}
```

**Implementation responsibilities:**
1. Create a DI scope per invocation (`IServiceScopeFactory.CreateScope()`)
2. Resolve the graph from the scope
3. Build the `ExecutionContext` with the scope's service provider
4. Call `graph.ExecuteAsync(context)`
5. Dispose the scope on completion (success or failure)

**Application code simplifies to:**
```csharp
public class JournalProcessingDataFlowJob : IConsumer<ProcessJournalsMessage>
{
    private readonly IDataFlowExecutor _executor;
    
    public JournalProcessingDataFlowJob(IDataFlowExecutor executor)
        => _executor = executor;
    
    public async Task Consume(ConsumeContext<ProcessJournalsMessage> context)
    {
        var triggerData = new JsonObject { ["batchId"] = context.Message.BatchId };
        // ... build triggerContext ...
        
        await _executor.ExecuteAsync(
            JournalProcessingV2Registration.GraphKey,
            triggerContext,
            context.CancellationToken);
    }
}
```

The hidden concerns (scoping, service provider, invocation ID generation) are encapsulated in the executor.

---

## Recommended Actions

These findings should be added to the implementation issue #145:

| Finding | Action |
|---------|--------|
| `EpochCoordinator == null` on graph | Document (expected, not a bug) |
| `context.ServiceProvider` unused by framework | Consider removing from `IExecutionContext` or marking as candidate for removal |
| Graph resolved from root scope → scoping bug | Add to #145: implement `IDataFlowExecutor` |
| Execution boilerplate | Add to #145: implement `IDataFlowExecutor` |
