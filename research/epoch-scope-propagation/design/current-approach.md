# Current Approach: EpochManager with Lazy Resolution

**Status**: Documented from existing design  
**Source**: `/docs/design/epoch-scoped-services/README.md`

---

## Overview

The current design proposes a **centralized epoch management** approach where:
- `IEpochManager` creates and manages `IEpoch` objects
- Each `IEpoch` has its own DI scope (`IServiceScope`)
- Blocks access epochs via `IBlockContext.CurrentEpoch` (lazy resolution)
- EpochManager handles lifecycle via reference counting
- Epoch objects are separate from `IEpochStream`

---

## Core Components

### 1. IEpoch Interface

```csharp
/// <summary>
/// Represents an epoch instance with its own dependency injection scope.
/// Provides access to services scoped to this epoch's lifetime.
/// </summary>
public interface IEpoch : IAsyncDisposable
{
    /// <summary>
    /// The epoch vector identifying this epoch.
    /// </summary>
    EpochVector Vector { get; }
    
    /// <summary>
    /// Resolves a service from this epoch's DI scope.
    /// Multiple blocks accessing the same epoch will get the same instance.
    /// </summary>
    T GetService<T>() where T : notnull;
    
    /// <summary>
    /// Resolves a service from this epoch's DI scope, returning null if not registered.
    /// </summary>
    T? GetServiceOrNull<T>() where T : class;
    
    /// <summary>
    /// Gets the service provider for this epoch's scope.
    /// </summary>
    IServiceProvider ServiceProvider { get; }
}
```

**Key Characteristics**:
- Owns the DI scope for epoch-scoped services
- Independent object from `IEpochStream`
- Accessed via blocks, not via streams
- Lifecycle managed by `IEpochManager`

### 2. IEpochManager Interface

```csharp
/// <summary>
/// Manages the lifecycle of epoch objects and their DI scopes.
/// Handles epoch vector subsume operations and tracks which epochs are active.
/// </summary>
public interface IEpochManager : IAsyncDisposable
{
    /// <summary>
    /// Gets or creates an epoch object for the given epoch vector.
    /// If the epoch already exists, returns the existing instance.
    /// </summary>
    IEpoch GetOrCreateEpoch(EpochVector vector);
    
    /// <summary>
    /// Called when a block completes processing an epoch.
    /// Decrements the reference count and disposes the epoch if no longer needed.
    /// </summary>
    ValueTask NotifyEpochCompletedAsync(
        EpochVector vector, 
        IBlockContext block, 
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Called when epoch vectors are subsumed (e.g., vector A subsumed into vector B).
    /// Extends the lifetime of the epoch object to cover the subsumed vector space.
    /// </summary>
    void NotifyEpochSubsumed(EpochVector from, EpochVector to);
}
```

**Key Characteristics**:
- Centralized epoch registry (dictionary of active epochs)
- Manages creation, reuse, and disposal
- Handles reference counting
- Coordinates subsume operations

### 3. Block Integration

Blocks access epochs via `IBlockContext`:

```csharp
public interface IBlockContext
{
    string BlockId { get; }
    
    /// <summary>
    /// Gets the current epoch object for epoch-compatible blocks.
    /// Returns null if the block is not epoch-compatible.
    /// </summary>
    IEpoch? CurrentEpoch { get; }
}
```

Blocks opt-in via marker interface:

```csharp
/// <summary>
/// Marker interface for blocks that are epoch-compatible and want access
/// to the epoch object and its scoped services.
/// </summary>
public interface IEpochCompatibleBlock
{
    // Blocks implementing this interface will receive IEpoch in their execution context
}
```

---

## Lifecycle Flow

### 1. Epoch Creation

```
Source Block Processing Item
    │
    ├─ Determine epoch vector for item
    │
    ├─ Framework detects IEpochCompatibleBlock
    │
    └─ Framework calls IEpochManager.GetOrCreateEpoch(vector)
        │
        ├─ Check if epoch exists in dictionary
        │
        ├─ If NOT exists:
        │   ├─ Create IServiceScope from root provider
        │   ├─ Create Epoch object with scope
        │   ├─ Add to dictionary
        │   └─ Initialize reference count = 1
        │
        └─ If exists:
            ├─ Return existing epoch
            └─ Increment reference count
```

**Key Points**:
- Lazy creation on first access
- Dictionary lookup for reuse
- Reference counting starts at creation

### 2. Block Access

```
Block Processing IEpochStream
    │
    ├─ Framework provides IBlockContext with CurrentEpoch
    │
    ├─ Block: var epoch = context.CurrentEpoch
    │
    ├─ Block: var service = epoch.GetService<MyService>()
    │   │
    │   └─ Resolved from epoch's IServiceScope
    │       (Same instance for all blocks in same epoch)
    │
    └─ Block processes items
```

**Key Points**:
- Block doesn't interact with EpochManager directly
- Framework populates `CurrentEpoch` based on stream's vector
- Service resolution is straightforward for block developer

### 3. Epoch Completion

```
Block Completes Processing Epoch
    │
    └─ Framework calls IEpochManager.NotifyEpochCompletedAsync(vector, block, ct)
        │
        ├─ Lookup epoch in dictionary
        │
        ├─ Decrement reference count
        │
        └─ If reference count == 0:
            ├─ Dispose IServiceScope (disposes epoch-scoped services)
            ├─ Remove from dictionary
            └─ Return true (epoch disposed)
```

**Key Points**:
- Reference counting determines disposal
- Automatic cleanup when no blocks are using epoch
- Safe concurrent access via locking in Epoch class

### 4. Subsume Operations

```
Fan-In Block Merges Epoch Vectors
    │
    ├─ Vector A: {s1=5}
    ├─ Vector B: {s2=3}
    └─ Merged: {s1=5, s2=3}
        │
        └─ Framework calls IEpochManager.NotifyEpochSubsumed(from, to)
            │
            ├─ Lookup epoch for 'from' vector
            │
            ├─ Add epoch to dictionary under 'to' vector
            │
            ├─ Increment reference count (epoch now covers both vectors)
            │
            └─ Subsequent access to 'to' vector returns same epoch
```

**Key Points**:
- Epoch lifetime extends to cover subsumed vector space
- Same DI scope/services available under new vector
- Reference counting prevents premature disposal

---

## Reference Counting Strategy

### Problem

An epoch may be processed by multiple blocks at different stages:

```
Source → Block A → Block B → Block C → Sink
```

All blocks may need access to the same epoch-scoped services for a given epoch.

### Solution

Track references per block participation:

```
Epoch created:        refCount = 1 (initial reference)
Block A starts:       refCount++ (now 2)
Block B starts:       refCount++ (now 3)
Block C starts:       refCount++ (now 4)
Block A completes:    refCount-- (now 3)
Block B completes:    refCount-- (now 2)
Block C completes:    refCount-- (now 1)
Initial ref released: refCount-- (now 0) → Dispose
```

### Implementation

```csharp
internal sealed class Epoch : IEpoch
{
    private readonly IServiceScope _scope;
    private int _referenceCount;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    internal async ValueTask<bool> TryReleaseAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _referenceCount--;
            if (_referenceCount <= 0)
            {
                await DisposeAsync();
                return true; // Epoch was disposed
            }
            return false; // Still has references
        }
        finally
        {
            _lock.Release();
        }
    }
    
    internal void AddReference()
    {
        Interlocked.Increment(ref _referenceCount);
    }
}
```

**Key Characteristics**:
- Thread-safe via SemaphoreSlim
- Automatic disposal when count reaches zero
- Handles concurrent block access

---

## Strengths of Current Approach

### 1. Clear Separation of Concerns

- **IEpochStream**: Data + metadata (vector)
- **IEpoch**: DI scope + service resolution
- **IEpochManager**: Lifecycle + coordination

Each component has a single, well-defined responsibility.

### 2. Centralized Lifecycle Management

- Single source of truth (EpochManager dictionary)
- Explicit lifecycle events (creation, completion, disposal)
- Easier to reason about when epochs are created/destroyed

### 3. Reusability Across Blocks

- Dictionary lookup ensures same epoch instance
- Reference counting handles complex block topologies
- Works naturally with concurrent processing

### 4. Explicit Block Opt-In

- `IEpochCompatibleBlock` clearly signals intent
- Blocks that don't need epochs aren't affected
- Non-null `CurrentEpoch` guaranteed for compatible blocks

### 5. Subsume Handling

- Explicit `NotifyEpochSubsumed` method
- Can implement various merge strategies
- Clear extension point for complex subsume semantics

---

## Weaknesses of Current Approach

### 1. Indirection

- Epoch object is separate from epoch stream
- Blocks access via `IBlockContext.CurrentEpoch`
- Extra lookup to map stream vector → epoch object

### 2. Reference Counting Complexity

- Must track which blocks have accessed which epochs
- Requires coordination between framework and EpochManager
- Potential for leaks if reference counting logic has bugs

### 3. Lifecycle Coupling

- Epoch disposal tied to reference count reaching zero
- Not directly tied to stream consumption
- Must coordinate with global epoch alignment separately

### 4. Framework Responsibility

- Framework must:
  - Detect `IEpochCompatibleBlock`
  - Call `GetOrCreateEpoch` at right time
  - Call `NotifyEpochCompletedAsync` at right time
  - Handle subsume notifications
  - Populate `CurrentEpoch` in context

### 5. Subsume Semantics

- Must handle transfer of epoch to new vector
- Reference counting must account for vector aliasing
- Potential for confusion if multiple vectors map to same epoch

---

## Integration Example

### Service Registration

```csharp
services.AddEpochScoped<DemoDbContext>();
```

(Uses standard `AddScoped` since epoch creates its own `IServiceScope`)

### Block Implementation

```csharp
public sealed class WriteContextBlock : IEpochCompatibleBlock
{
    public async IAsyncEnumerable<IEpochStream<DataRecord>> ProcessAsync(
        IAsyncEnumerable<IEpochStream<DataRecord>> input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var epochStream in input.WithCancellation(cancellationToken))
        {
            yield return CreateEpochStream(
                epochStream.Epoch,
                ProcessEpochItems(epochStream, cancellationToken));
        }
    }

    private async IAsyncEnumerable<DataRecord> ProcessEpochItems(
        IEpochStream<DataRecord> epochStream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Framework-provided context
        var epoch = _context.CurrentEpoch 
            ?? throw new InvalidOperationException("Epoch not available");
        
        // Resolve epoch-scoped service
        var dbContext = epoch.GetService<DemoDbContext>();
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Process items...
        await foreach (var item in epochStream.Items.WithCancellation(cancellationToken))
        {
            // Use dbContext...
            yield return item;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
```

**Developer Experience**:
- Block developer doesn't see EpochManager
- Simple service resolution pattern
- Framework handles all lifecycle

---

## Summary

**Current Approach Philosophy**: 
> "Centralized, explicit, reference-counted epoch lifecycle management separate from streams"

**Key Trade-Off**:
- **Pro**: Clear separation, explicit lifecycle, easier centralized coordination
- **Con**: Indirection, reference counting complexity, framework orchestration burden

**Works Well For**:
- Complex block topologies
- Multiple blocks accessing same epoch concurrently
- Explicit lifecycle management requirements

**Challenges**:
- Reference counting correctness
- Framework integration complexity
- Indirection between streams and epochs
