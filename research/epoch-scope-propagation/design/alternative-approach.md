# Alternative Approach: IEpochStream with DI Scope Propagation

**Status**: Proposed alternative being evaluated  
**Source**: Issue question exploring alternative architecture

---

## Overview

The alternative approach proposes **coupling DI scope directly to epoch streams**:
- DI scope is a property on `IEpochStream` itself
- Scope created eagerly when epoch stream is created (at source)
- Scope propagated to output epoch streams as streams flow through blocks
- Lifecycle tied to stream consumption rather than reference counting
- No separate `IEpoch` object or `IEpochManager`

---

## Core Concept

### Philosophical Shift

**Current**: Epoch object is a separate concern, managed centrally
```
IEpochStream<T> {vector, items} → (separate) → IEpoch {vector, scope, services}
```

**Alternative**: Epoch scope travels with the stream
```
IEpochStream<T> {vector, items, scope, services}
```

The DI scope becomes **first-class metadata** on the stream, just like the vector.

---

## Proposed Components

### 1. Enhanced IEpochStream Interface

```csharp
/// <summary>
/// Represents a stream of items belonging to a specific epoch.
/// Carries the epoch vector and DI scope for epoch-scoped services.
/// </summary>
public interface IEpochStream<out T> : IAsyncDisposable
{
    /// <summary>
    /// The epoch vector identifying this stream's position in the dataflow.
    /// </summary>
    EpochVector Vector { get; }

    /// <summary>
    /// The data items belonging to this epoch.
    /// The stream completes when all epoch data has been yielded.
    /// </summary>
    IAsyncEnumerable<T> Items { get; }
    
    /// <summary>
    /// The DI scope for services scoped to this epoch.
    /// Multiple blocks can resolve services from this scope.
    /// Scope is disposed when the stream is disposed.
    /// </summary>
    IServiceProvider ServiceProvider { get; }
    
    /// <summary>
    /// Resolves a service from this epoch's DI scope.
    /// </summary>
    T GetService<TService>() where TService : notnull;
    
    /// <summary>
    /// Resolves a service from this epoch's DI scope, returning null if not registered.
    /// </summary>
    TService? GetServiceOrNull<TService>() where TService : class;
}
```

**Key Changes**:
- Adds `ServiceProvider` property
- Adds service resolution methods
- Implements `IAsyncDisposable` for scope cleanup
- Scope is part of stream metadata (like vector)

### 2. Implementation

```csharp
internal sealed class EpochStream<T> : IEpochStream<T>
{
    private readonly IServiceScope _scope;
    
    public EpochVector Vector { get; }
    public IAsyncEnumerable<T> Items { get; }
    public IServiceProvider ServiceProvider => _scope.ServiceProvider;

    public EpochStream(EpochVector vector, IAsyncEnumerable<T> items, IServiceScope scope)
    {
        Vector = vector ?? throw new ArgumentNullException(nameof(vector));
        Items = items ?? throw new ArgumentNullException(nameof(items));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }
    
    public T GetService<TService>() where TService : notnull
    {
        return ServiceProvider.GetRequiredService<TService>();
    }
    
    public TService? GetServiceOrNull<TService>() where TService : class
    {
        return ServiceProvider.GetService<TService>();
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
    }
}
```

**Key Characteristics**:
- Owns the DI scope
- Scope lifecycle = stream lifecycle
- No separate epoch object needed

### 3. No IEpochManager

The centralized manager is **eliminated**:
- No dictionary of active epochs
- No reference counting
- No explicit lifecycle notifications
- Scope creation/disposal is stream creation/disposal

---

## Lifecycle Flow

### 1. Epoch Stream Creation (Source Block)

```
Source Block Creates New Epoch
    │
    ├─ Determine epoch vector (e.g., {sourceId=5})
    │
    ├─ Create IServiceScope from root provider
    │   (Each epoch gets its own scope)
    │
    └─ Create EpochStream(vector, items, scope)
        │
        └─ Stream now carries:
            - Vector: {sourceId=5}
            - Items: async enumerable
            - Scope: DI scope for epoch-scoped services
```

**Key Points**:
- Scope created eagerly at source
- No manager lookup needed
- Scope is part of stream from creation

### 2. Block Processing (Propagation)

```
Transform Block Processes Epoch Stream
    │
    ├─ Receive input: IEpochStream<TIn>
    │
    ├─ Access epoch-scoped services:
    │   var service = inputStream.GetService<MyService>()
    │
    ├─ Transform items
    │
    └─ Create output stream: IEpochStream<TOut>
        │
        ├─ Vector: Same as input (or modified per block logic)
        │
        ├─ Items: Transformed items
        │
        └─ Scope: PROPAGATE from input stream
            (Pass same IServiceScope to output)
```

**Key Points**:
- Input stream's scope is **propagated** to output
- No new scope created during propagation
- All streams for same epoch share same scope

### 3. Vector Changes (Propagation with Vector Update)

```
Block Updates Epoch Vector (e.g., increment)
    │
    ├─ Input vector: {s1=5}
    │
    ├─ New vector: {s1=6}  (source advances)
    │
    └─ Create output stream: IEpochStream<T>
        │
        ├─ Vector: {s1=6}  (NEW)
        │
        ├─ Items: Processed items
        │
        └─ Scope: SAME as input stream
            (Scope propagates even when vector changes)
```

**Key Points**:
- Vector can change independently
- Scope propagates regardless
- One scope can serve multiple vector values (during transitions)

### 4. Fan-In with Vector Merging

```
BufferNode Merges Two Epoch Streams
    │
    ├─ Stream A: {vector={s1=5}, scope=scopeA}
    ├─ Stream B: {vector={s2=3}, scope=scopeB}
    │
    └─ Merged Stream
        │
        ├─ Vector: {s1=5, s2=3}  (element-wise max)
        │
        ├─ Items: Merged items from A and B
        │
        └─ Scope: ???
            │
            ├─ OPTION 1: Pick scope from one stream (arbitrary)
            ├─ OPTION 2: Create new scope, dispose old ones
            └─ OPTION 3: Keep both scopes somehow
```

**Challenge**: What happens to DI scopes when merging?

### 5. Stream Disposal

```
Stream Consumption Completes
    │
    ├─ All items yielded from stream.Items
    │
    └─ Stream.DisposeAsync() called
        │
        └─ Scope.DisposeAsync() called
            │
            ├─ All epoch-scoped services disposed
            └─ DI scope cleaned up
```

**Key Points**:
- Automatic disposal when stream is done
- No reference counting needed
- Lifecycle tied to stream consumption

---

## Propagation Pattern

### Block Template

Every block follows this propagation pattern:

```csharp
public async IAsyncEnumerable<IEpochStream<TOut>> ProcessAsync(
    IAsyncEnumerable<IEpochStream<TIn>> input,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await foreach (var inputStream in input.WithCancellation(cancellationToken))
    {
        // Access epoch-scoped services from input stream
        var service = inputStream.GetService<MyService>();
        
        // Process items
        var outputItems = ProcessItems(inputStream.Items, service, cancellationToken);
        
        // Create output stream, PROPAGATING scope
        yield return new EpochStream<TOut>(
            vector: inputStream.Vector,           // Keep or modify vector
            items: outputItems,                    // Transformed items
            scope: inputStream._scope);            // PROPAGATE scope (same instance)
            
        // NOTE: Don't dispose inputStream here - scope is still in use!
    }
}
```

**Key Pattern**: Scope flows with stream through pipeline

### Scope Ownership

**Question**: Who owns the scope for disposal?

**Option A**: Last consumer disposes
- Source creates scope
- Blocks propagate (don't dispose)
- Terminal block disposes after consuming

**Option B**: Shared ownership with reference counting
- Each stream holds reference to scope
- Scope disposed when all references released
- Back to reference counting problem...

**Option C**: Scope wrapper with tracking
- Wrapper around IServiceScope
- Tracks how many streams reference it
- Auto-disposes when all streams disposed

---

## Strengths of Alternative Approach

### 1. Direct Coupling

- Scope travels with stream (no indirection)
- Service resolution is directly on stream
- Clear that scope belongs to this epoch stream

### 2. Eager Creation

- Scope created at source (point of origin)
- No lazy lookup or dictionary access
- Scope exists from stream creation

### 3. Natural Propagation

- Blocks receive input stream with scope
- Blocks create output stream with same scope
- Propagation is explicit in block code

### 4. Simpler Lifecycle

- No reference counting needed (maybe?)
- Disposal tied to stream consumption
- Framework doesn't need to track completion

### 5. No Centralized Manager

- No IEpochManager needed
- No global dictionary of epochs
- Less coordination overhead

### 6. Block Simplicity

- Block just uses inputStream.GetService<T>()
- No need for IBlockContext.CurrentEpoch
- More direct API

---

## Weaknesses of Alternative Approach

### 1. Fan-In Scope Merging

**Major Challenge**: What happens when two epoch streams merge?

```csharp
Stream A: {vector={s1=5}, scope=scopeA}
Stream B: {vector={s2=3}, scope=scopeB}
Merged:   {vector={s1=5, s2=3}, scope=???}
```

**Options**:
- **Pick one scope arbitrarily**: But scopeA and scopeB may have different service instances!
- **Create new scope**: Loses services already resolved in scopeA/scopeB
- **Merge scopes**: Not supported by DI containers

**Problem**: The whole point is sharing service instances across blocks in same epoch. But with fan-in, we have TWO different epochs (with different scopes) merging into ONE epoch.

**This breaks the fundamental requirement**: Blocks processing the same epoch must access the same service instances.

### 2. Scope Ownership and Disposal

**Challenge**: Who disposes the scope?

- If **input stream** is responsible: Can't dispose because scope is propagated to output
- If **output stream** is responsible: But multiple outputs might share scope
- Need some form of **reference tracking** anyway

### 3. Vector vs Scope Mismatch

Epoch vectors can change (increment, subsume) but scope should remain same:

```
Stream 1: {vector={s1=5}, scope=scope1}
Stream 2: {vector={s1=6}, scope=scope1}  (same scope, different vector)
```

But with propagation, we have:
```
Input:  {vector={s1=5}, scope=scope1}
Output: {vector={s1=6}, scope=???}
```

Should output get scope1? Or new scope? If scope1, how does it know to use it when vector changed?

**Confusion**: Vector and scope have different update semantics.

### 4. Subsume Semantics

When epoch A is subsumed into epoch B:
```
Epoch A: {vector={s1=5}, scope=scopeA}
Epoch B: {vector={s2=3}, scope=scopeB}
```

Current design: Extend scopeA lifetime to cover B's vector space.

Alternative: ???

How do we "extend" a scope that's embedded in a stream? Do we:
- Keep both scopes alive somehow?
- Merge them (can't)?
- Pick one (arbitrary)?

### 5. Concurrent Access

Multiple blocks may process same epoch stream concurrently (if stream is shared):

```
     ┌─ Block A (accesses stream.GetService<T>())
Stream ┤
     └─ Block B (accesses stream.GetService<T>())
```

Both access same scope → Good (shared services)

But:
```
Stream A ─────┐
              ├─ Merged Stream (scope=???)
Stream B ─────┘

Block C accesses merged stream.GetService<T>()
Block D accesses Stream A.GetService<T>()
```

Are Block C and Block D accessing the same service instance? Depends on merge strategy...

### 6. Epoch Identity

With current approach, epoch identity is clear:
- Epoch object = unique identity
- Dictionary maps vector → epoch
- Same vector = same epoch

With alternative:
- Epoch identity = ???
- If scope is propagated, same scope = same epoch?
- But fan-in creates ambiguity

### 7. Global Epoch Alignment

Current design integrates with `IEpochLifecycleParticipant` for global alignment:
- EpochManager can notify when epoch completes
- Lifecycle events tied to epoch object

Alternative:
- No epoch object, no manager
- How do we trigger lifecycle events?
- When does "epoch completion" happen?

---

## Fan-In Problem Deep Dive

This is the **critical challenge** for the alternative approach.

### Scenario

```
Source A: Creates {vector={A=1}, scope=scopeA}
Source B: Creates {vector={B=1}, scope=scopeB}
           │
           ├─ Stream A: {vector={A=1}, scope=scopeA}
           ├─ Stream B: {vector={B=1}, scope=scopeB}
           │
           └─ BufferNode merges them
              │
              └─ Merged Stream: {vector={A=1, B=1}, scope=???}
```

### Why This Matters

The **purpose of epoch-scoped services** is:
> "Multiple concurrent blocks processing the same epoch can resolve the same service instance"

With fan-in, we have:
- Before merge: Two different epochs (A and B), two different scopes
- After merge: One unified epoch (A ∪ B), ??? scope

If merged stream uses scopeA:
- Services resolved from scopeA are used
- But scopeB services are lost/inaccessible

If merged stream uses scopeB:
- Services resolved from scopeB are used
- But scopeA services are lost/inaccessible

If merged stream creates new scopeC:
- Fresh services, but neither scopeA nor scopeB
- Breaks continuity - services resolved before merge are different from after

### Current Approach Solution

Current design uses **epoch vector subsume semantics**:

```
EpochManager has:
- Epoch for {A=1} with scopeA
- Epoch for {B=1} with scopeB

After merge:
- Epoch for {A=1, B=1} → Either extend scopeA or scopeB
- NotifyEpochSubsumed({A=1}, {A=1, B=1})
- NotifyEpochSubsumed({B=1}, {A=1, B=1})
- One scope "wins" and covers the merged vector space
```

**Strategy**: Pick one scope, dispose the other (or keep both alive until both complete).

### Alternative Approach Challenge

With streams carrying scopes:

```
Stream A: {vector={A=1}, scope=scopeA}
Stream B: {vector={B=1}, scope=scopeB}
```

BufferNode must merge them:

```csharp
public async IAsyncEnumerable<IEpochStream<T>> MergeStreams(
    IEpochStream<T> streamA,
    IEpochStream<T> streamB)
{
    // Merge vectors (element-wise max)
    var mergedVector = streamA.Vector.ElementWiseMax(streamB.Vector);
    
    // Merge items
    var mergedItems = MergeItems(streamA.Items, streamB.Items);
    
    // Merge scopes ??? HOW ???
    var mergedScope = ??? // scopeA? scopeB? new? both?
    
    yield return new EpochStream<T>(mergedVector, mergedItems, mergedScope);
    
    // What about disposing streamA and streamB scopes?
}
```

**No clear answer** without reverting to some form of centralized management or reference counting.

---

## Possible Solutions to Fan-In

### Solution 1: Scope Hierarchy

Create a **hierarchical scope** that can resolve from multiple parent scopes:

```csharp
public class MergedServiceProvider : IServiceProvider
{
    private readonly IServiceProvider _scopeA;
    private readonly IServiceProvider _scopeB;
    
    public object? GetService(Type serviceType)
    {
        // Try scopeA first, fallback to scopeB
        return _scopeA.GetService(serviceType) 
            ?? _scopeB.GetService(serviceType);
    }
}
```

**Pros**: Can access services from both scopes
**Cons**: 
- Arbitrary resolution order (scopeA before scopeB)
- If same service type in both scopes, only one is accessible
- Doesn't truly "merge" scopes

### Solution 2: Scope Registry (Back to Manager)

Reintroduce a **registry** that tracks scope ownership:

```csharp
internal class EpochScopeRegistry
{
    private readonly ConcurrentDictionary<EpochVector, IServiceScope> _scopes = new();
    
    public IServiceScope GetOrCreateScope(EpochVector vector, IServiceProvider rootProvider)
    {
        // Similar to EpochManager...
    }
}
```

**Pros**: Solves fan-in by lookup
**Cons**: Back to centralized management (defeats purpose of alternative)

### Solution 3: Accept Arbitrary Scope Selection

Simply **pick one scope** (e.g., from first stream in fan-in):

```csharp
var mergedScope = streamA.Scope; // Arbitrary choice
```

**Pros**: Simple, no coordination needed
**Cons**: 
- Services from scopeB are inaccessible
- Breaks semantic of "same epoch = same services"
- Confusing for developers

### Solution 4: No Fan-In Support

**Don't support epoch-scoped services with fan-in** at all:

- If blocks do fan-in, epoch scoping is undefined
- Blocks must handle scope merging themselves
- Fallback to manual scope management for fan-in cases

**Pros**: Alternative approach viable for non-fan-in cases
**Cons**: Major limitation, defeats purpose for complex topologies

---

## Summary

**Alternative Approach Philosophy**: 
> "DI scope is first-class stream metadata, propagated eagerly through pipeline"

**Key Trade-Off**:
- **Pro**: Direct coupling, eager creation, simpler propagation for linear pipelines
- **Con**: Fan-in scope merging is extremely challenging, possibly unsolvable elegantly

**Works Well For**:
- Linear pipelines (no fan-in)
- Simple topologies
- Blocks that don't need shared epoch services across merge points

**Critical Challenge**:
- **Fan-in scope merging** has no clean solution
- Requires either:
  - Reverting to centralized management (defeats purpose)
  - Accepting arbitrary scope selection (breaks semantics)
  - Not supporting fan-in (major limitation)
  
**Verdict**: Alternative is **feasible for simple cases** but **problematic for general solution**.
