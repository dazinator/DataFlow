# DI Scope Lifetimes

This document captures the three distinct DI scope lifetimes in the DataFlow runtime and the reasoning behind each.

## Overview

There are three scope tiers, each owned by a different layer of the runtime:

| Scope | Owner | Created by | Lifetime |
|-------|-------|-----------|----------|
| **Flow scope** | `DataFlowGraph` | `context.ScopeFactory` | `FlowStarted` → `FlowCompleted` |
| **Block scope** | `DataFlowGraph.BlockRuntimeModel` | `context.ScopeFactory` | `BlockStarted` → `BlockCompleted` |
| **Actor scope** | `EpochActorBlock` / `EpochSourceBlock` | injected `IServiceScopeFactory` | Per epoch stream; rotatable mid-stream |

These scopes are independent and may nest, but they do not share state. Each resolves its own `IFlowEventSink` (and any other scoped services) from its own scope root.

---

## 1. Flow Scope

**Owner:** `DataFlowGraph.ExecuteAsync`

**Source of factory:** `context.ScopeFactory` (resolved from `IExecutionContext`)

**Created/disposed:**
```csharp
// DataFlowGraph.ExecuteAsync
await using var flowScope = context.ScopeFactory?.CreateAsyncScope();
var eventSink = flowScope?.ServiceProvider.GetService<IFlowEventSink>();
```

The scope is created before `FlowStarted` is emitted and is disposed when `ExecuteAsync` returns — whether that is normal completion, cancellation, or an unhandled exception. `await using` on a regular `async Task` method guarantees disposal at method exit.

**Purpose:**
- Provides an isolated `IFlowEventSink` instance for flow-level lifecycle events (`FlowStarted`, `FlowCompleted`)
- Prevents the sink from accumulating state across multiple flow runs if the host application reuses the same DI container

**Nullability:** If `context.ScopeFactory` is null (no event infrastructure registered, typical in unit tests), the scope and sink are both null and event emission is silently skipped.

---

## 2. Block Scope

**Owner:** `DataFlowGraph.BlockRuntimeModel.ExecuteAsync`

**Source of factory:** `context.ScopeFactory` (same `IExecutionContext` passed down from the graph)

**Created/disposed:**
```csharp
// DataFlowGraph.BlockRuntimeModel.ExecuteAsync
await using var blockScope = context.ScopeFactory?.CreateAsyncScope();
var eventSink = blockScope?.ServiceProvider.GetService<IFlowEventSink>();
```

The scope is created once per block execution before `BlockStarted` is emitted. It is disposed via `await using` when the block's `ExecuteAsync` and all downstream routing completes — including in the `catch` path for failures.

**Purpose:**
- Gives each block its own `IFlowEventSink` instance for block-level lifecycle events (`BlockStarted`, `BlockCompleted`)
- If the sink wraps a `DbContext` or similar stateful scoped service, each block gets a fresh instance rather than sharing one across concurrent blocks

**Why block events live in `DataFlowGraph`, not `BlockBase`:**

The graph executes blocks through a typed `ExecutableBlockAdapter` that calls `IBlock<TIn,TOut>.ExecuteAsync` directly:

```csharp
// TypedBlockExecutor.cs
var typedOutput = _typedBlock.ExecuteAsync(typedInput, context);  // typed interface
```

This bypasses `IBlock.ExecuteAsync` (the untyped wrapper in `BlockBase`), which was designed to eliminate per-item boxing. Because `DataFlowGraph.BlockRuntimeModel` is the only layer that observes the full block execution lifecycle — including the routing and enumeration that follows `ExecuteAsync` — it is the only correct place to emit `BlockStarted`/`BlockCompleted`. Adding events to `BlockBase.IBlock.ExecuteAsync` would require routing through the untyped interface (reintroducing boxing) or renaming the abstract method in every concrete block.

**Lifetime guarantee:** One scope per block per graph execution, disposed regardless of success or failure.

---

## 3. Actor Scope

**Owner:** `EpochActorBlock` / `EpochSourceBlock`

**Source of factory:** Constructor-injected `IServiceScopeFactory` (from the application's DI container, not from `IExecutionContext`)

```csharp
// EpochActorBlock constructor
public EpochActorBlock(IBlockContext context, IServiceScopeFactory scopeFactory)
```

**Created/disposed (per epoch, with rotation):**
```csharp
// EpochActorBlock.ProcessEpochItems
while (!cancellationToken.IsCancellationRequested)
{
    bool rotationRequested = false;

    await using (var scope = _scopeFactory.CreateAsyncScope())
    {
        var actor = scope.ServiceProvider.GetRequiredService<TActor>();
        // ... process items, yield results ...
    }
    // scope disposed here — actor and all its scoped dependencies released

    if (!rotationRequested) yield break;
    // rotation: loop continues, new scope created for next actor instance
}
```

**Purpose:**
- Actors resolve their dependencies (e.g. `DbContext`) from their own scope
- Actor scope lifetime is per-epoch by default — one actor instance processes one epoch stream
- **Rotation** allows a long-running actor to voluntarily discard its scope mid-stream and receive a fresh one, without restarting the block or losing position in the input stream. This is used when a scoped dependency accumulates state over time (e.g. a `DbContext` growing its change tracker over thousands of items)

**Rotation semantics:**
- The actor calls `context.RequestRotation()` to signal intent
- The block detects this after the actor's `RunAsync` completes
- The `while` loop creates a new `AsyncServiceScope`, resolving a fresh actor instance
- The *input stream enumerator is preserved* across rotations — the new actor continues from where the previous one stopped

**Key difference from block/flow scopes:** Actor scopes come from a factory injected at construction time, not from `context.ScopeFactory`. This is intentional: actor scope management is the block's own concern and is independent of the event infrastructure. A block can rotate actors even when no event sink is registered.

---

## Source of `IServiceScopeFactory`

There are two distinct sources of `IServiceScopeFactory` in the runtime:

| Source | Used by | Purpose |
|--------|---------|---------|
| `context.ScopeFactory` (`IExecutionContext`) | `DataFlowGraph` (flow + block scopes) | Event emission infrastructure |
| Constructor-injected `IServiceScopeFactory` | `EpochActorBlock`, `EpochSourceBlock` | Actor lifecycle management |

`context.ScopeFactory` is nullable. It is derived in `ExecutionContext` from the host `IServiceProvider`:

```csharp
ScopeFactory = serviceProvider.GetService<IServiceScopeFactory>();
```

When the Blazor visualisation package (`Uniun.DataFlow.Blazor.Server`) is not registered, `GetService` returns null, and all event-related scope creation is skipped throughout the runtime without any code-path changes.

---

## Diagram

```
Graph.ExecuteAsync()
│
├── await using flowScope           ← Flow scope (context.ScopeFactory)
│   └── FlowStarted
│
├── BlockRuntimeModel.ExecuteAsync() [per block, concurrent]
│   ├── await using blockScope      ← Block scope (context.ScopeFactory)
│   │   └── BlockStarted
│   │
│   └── block.ExecuteAsync()        ← typed IBlock<TIn,TOut>.ExecuteAsync
│       │
│       └── [EpochActorBlock only]
│           └── per epoch:
│               ├── await using actorScope   ← Actor scope (injected factory)
│               │   └── actor.RunAsync()
│               └── [rotation?] → new actorScope, same input position
│
│   ├── BlockCompleted (success)
│   └── BlockCompleted (failure)    ← both in finally, scope disposed
│
└── FlowCompleted                   ← flowScope disposed
```

---

## Invariants

1. **Scopes never cross tier boundaries.** The flow scope is not passed to blocks; the block scope is not passed to actors.
2. **Each scope resolves its own sink.** If `IFlowEventSink` is scoped, three separate instances exist simultaneously during block execution (one in the flow scope, one per concurrent block scope).
3. **Null propagates cleanly.** `context.ScopeFactory` being null short-circuits all three tiers without conditional branches — the scope variable is null, the sink is null, emission is skipped.
4. **Actor rotation is independent of event infrastructure.** Rotation is driven by the injected `IServiceScopeFactory`, not `context.ScopeFactory`, so it works with or without event sinks registered.
5. **Disposal is always in `finally` or `await using`.** No scope can be leaked by a thrown exception.
