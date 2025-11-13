# API Refinements and Implementation Clarifications

**Status**: Design Refinement  
**Last Updated**: 2025-11-13  
**Addresses**: Architect feedback on design details

---

## Overview

This document addresses specific refinements and clarifications based on architectural review feedback. These refinements tighten the design without changing core concepts.

---

## 1. CurrentEpoch Null Semantics

### Specification

**For `IEpochCompatibleBlock` implementations:**
- `IBlockContext.CurrentEpoch` is **guaranteed non-null** whenever epoch streams are being processed
- Framework ensures epoch is available before calling block methods
- Blocks can safely assume non-null without defensive checks

**For non-epoch-compatible blocks:**
- `IBlockContext.CurrentEpoch` is **always null**
- Blocks that don't implement `IEpochCompatibleBlock` never receive epoch context
- Attempting to access will return null (not throw)

### Updated Interface

```csharp
public interface IBlockContext
{
    string BlockId { get; }
    
    /// <summary>
    /// Gets the current epoch object for epoch-compatible blocks.
    /// 
    /// Guaranteed non-null for blocks implementing IEpochCompatibleBlock
    /// when processing epoch streams.
    /// 
    /// Always null for blocks not implementing IEpochCompatibleBlock.
    /// </summary>
    IEpoch? CurrentEpoch { get; }
}
```

### Usage Patterns

**For epoch-compatible blocks (recommended):**
```csharp
public class MyEpochBlock : IEpochCompatibleBlock
{
    private readonly IBlockContext _context;
    
    public async Task ProcessAsync(DataRecord item)
    {
        // Safe - guaranteed non-null for IEpochCompatibleBlock
        var epoch = _context.CurrentEpoch!;
        var dbContext = epoch.GetService<DbContext>();
    }
}
```

**For defensive blocks (optional):**
```csharp
public async Task ProcessAsync(DataRecord item)
{
    if (_context.CurrentEpoch is { } epoch)
    {
        var dbContext = epoch.GetService<DbContext>();
    }
    else
    {
        // Fallback behavior
    }
}
```

### Framework Responsibility

The framework must:
1. Check if block implements `IEpochCompatibleBlock`
2. If yes, populate `CurrentEpoch` before calling block methods
3. If no, leave `CurrentEpoch` as null

```csharp
// Framework code
public async Task ExecuteBlockAsync(IBlock block, IEpochStream<T> epochStream)
{
    IBlockContext context;
    
    if (block is IEpochCompatibleBlock)
    {
        var epoch = _epochManager.GetOrCreateEpoch(epochStream.Epoch);
        context = new BlockContext(blockId, epoch);  // Non-null epoch
    }
    else
    {
        context = new BlockContext(blockId, null);  // Null epoch
    }
    
    await block.ProcessAsync(context, item);
}
```

---

## 2. Service Resolution API

### Complete IEpoch Interface

```csharp
public interface IEpoch : IAsyncDisposable
{
    /// <summary>
    /// The epoch vector identifying this epoch.
    /// </summary>
    EpochVector Vector { get; }
    
    /// <summary>
    /// Gets the service provider for this epoch's scope.
    /// </summary>
    IServiceProvider ServiceProvider { get; }
    
    /// <summary>
    /// Resolves a required service from this epoch's DI scope.
    /// Throws if service is not registered.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Service of type T is not registered.
    /// </exception>
    T GetService<T>() where T : notnull;
    
    /// <summary>
    /// Resolves a service from this epoch's DI scope.
    /// Returns null if service is not registered.
    /// Use for optional dependencies.
    /// </summary>
    T? GetService<T>() where T : class;
}
```

### Usage Examples

**Required service:**
```csharp
// Throws if DbContext not registered
var dbContext = epoch.GetService<DbContext>();
```

**Optional service:**
```csharp
// Returns null if not registered
var metrics = epoch.GetService<IMetricsCollector>();
if (metrics != null)
{
    await metrics.RecordAsync("processed", 1);
}
```

**Multiple services:**
```csharp
var dbContext = epoch.GetService<DbContext>();
var cache = epoch.GetService<IEpochCache>();
var metrics = epoch.GetService<IMetricsCollector>();
```

---

## 3. Participation Tracking

### Chosen Strategy: Reactive Tracking

**Decision**: Use **reactive tracking** where blocks are counted as participants when they first request an epoch.

**Rationale**:
- Simpler than pre-registration
- Automatically handles routing (blocks not receiving epochs don't participate)
- Works naturally with lazy evaluation
- Consistent with DI scope semantics

### Implementation Pattern

```csharp
public class EpochManager : IEpochManager
{
    private readonly ConcurrentDictionary<EpochVector, Epoch> _activeEpochs = new();
    private readonly ConcurrentDictionary<(EpochVector, string), bool> _blockParticipation = new();
    
    public IEpoch GetOrCreateEpoch(EpochVector vector)
    {
        var epoch = _activeEpochs.GetOrAdd(vector, v =>
        {
            var scope = _rootServiceProvider.CreateScope();
            return new Epoch(v, scope);
        });
        
        // Track participation when block requests epoch
        var blockId = _currentBlockContext?.BlockId 
            ?? throw new InvalidOperationException("No block context");
        
        _blockParticipation.TryAdd((vector, blockId), true);
        
        return epoch;
    }
    
    public async ValueTask NotifyEpochCompletedAsync(
        EpochVector vector,
        IBlockContext block,
        CancellationToken ct)
    {
        // Mark this block as complete for this vector
        _blockParticipation.TryRemove((vector, block.BlockId), out _);
        
        // Check if any blocks still participating
        var stillActive = _blockParticipation.Keys
            .Any(k => k.Item1.Equals(vector));
        
        if (!stillActive && _activeEpochs.TryGetValue(vector, out var epoch))
        {
            await epoch.TryReleaseVectorAsync(vector);
            
            // If epoch has no more vector references, remove it
            if (await epoch.IsFullyDisposedAsync())
            {
                _activeEpochs.TryRemove(vector, out _);
            }
        }
    }
}
```

### Participation Lifecycle

```
1. Block requests epoch via GetOrCreateEpoch()
   → Participation tracked: (vector, blockId) -> true

2. Block completes epoch via NotifyEpochCompletedAsync()
   → Participation removed: (vector, blockId) -> removed

3. When no more participants for vector:
   → Epoch can release that vector reference

4. When epoch has no more vector references:
   → Epoch is disposed
```

### Edge Cases

**Block requests epoch multiple times:**
- Only count as one participant (idempotent)
- TryAdd ensures no duplicate tracking

**Block never completes:**
- Timeout mechanism (future work)
- Monitoring/alerting for stuck epochs

**Block crashes:**
- Exception handling ensures cleanup
- OnDisposed event for emergency cleanup

---

## 4. Subsume API Guarantees

### Vector Immutability Contract

**Requirement**: `EpochVector` must be immutable value objects.

**Current State**: ✅ Already implemented as `record` type with `ImmutableDictionary`

```csharp
public sealed record EpochVector
{
    public ImmutableDictionary<string, long> Sequences { get; }
    
    // Immutable - creates new instances
    public EpochVector Merge(EpochVector other) { ... }
    public EpochVector IncrementSource(string sourceId) { ... }
}
```

### Subsume API Contract

```csharp
/// <summary>
/// Notifies the epoch manager that one epoch vector has been subsumed into another.
/// This extends the lifetime of the epoch to cover both vectors.
/// 
/// REQUIREMENTS:
/// - Vectors must be the exact instances flowing in streams (not reconstructed)
/// - Vectors must be immutable (already enforced by EpochVector type)
/// - 'from' vector must already exist in the manager
/// - 'to' vector will be registered to the same epoch instance as 'from'
/// 
/// GUARANTEES:
/// - After subsume, GetOrCreateEpoch(to) returns same instance as GetOrCreateEpoch(from)
/// - Service state from 'from' epoch is preserved
/// - Epoch does not dispose until both vectors complete
/// </summary>
void NotifyEpochSubsumed(EpochVector from, EpochVector to);
```

### Dictionary Lookup Guarantees

**Problem**: Dictionary lookups require exact reference equality or proper `Equals`/`GetHashCode`.

**Solution**: `EpochVector` as `record` type provides structural equality:

```csharp
var vector1 = EpochVector.FromSingleSource("s1", 5);
var vector2 = EpochVector.FromSingleSource("s1", 5);

// Structural equality (not reference equality)
Assert.Equal(vector1, vector2);  // ✅ True
Assert.True(vector1.Equals(vector2));  // ✅ True
Assert.Equal(vector1.GetHashCode(), vector2.GetHashCode());  // ✅ True

// Dictionary lookup works
var dict = new Dictionary<EpochVector, Epoch>();
dict[vector1] = epoch;
var retrieved = dict[vector2];  // ✅ Same epoch
```

### Best Practices for Callers

```csharp
// ✅ GOOD - Use exact vector from stream
await foreach (var epochStream in input)
{
    epochManager.NotifyEpochSubsumed(
        currentVector,
        epochStream.Epoch);  // Exact vector from stream
}

// ❌ BAD - Reconstructing vector
var reconstructed = EpochVector.FromSources(new()
{
    ["s1"] = 5  // Might be equivalent but not exact
});
epochManager.NotifyEpochSubsumed(currentVector, reconstructed);  // Risk
```

---

## 5. Error Handling Semantics

### Failure Modes

#### Mode 1: Block Failure During Epoch Processing

**Scenario**: Block throws exception while processing epoch.

**Behavior**:
1. Exception propagates to caller
2. Epoch resources remain valid for other blocks
3. Failed block's participation is marked complete
4. Other blocks can finish gracefully

```csharp
public async Task ExecuteBlockAsync(IBlock block, IEpoch epoch)
{
    try
    {
        await block.ProcessAsync(epoch, item);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Block {Block} failed", block);
        
        // Mark participation complete even on failure
        await _epochManager.NotifyEpochCompletedAsync(
            epoch.Vector, 
            block, 
            CancellationToken.None);
        
        throw;  // Propagate failure
    }
}
```

#### Mode 2: Epoch Service Failure

**Scenario**: Epoch-scoped service throws during operation.

**Behavior**:
1. Exception propagates to block
2. Service remains in scope (DI doesn't dispose on exception)
3. Other blocks see the same (potentially invalid) service
4. Epoch disposal cleans up at completion

**Recommendation**: Services should be resilient or blocks should handle errors.

#### Mode 3: Global Alignment Failure

**Scenario**: Some blocks complete successfully, others fail.

**Behavior**:
1. Failed blocks mark participation complete
2. Successful blocks mark participation complete
3. Epoch disposes when all participate (success or failure)
4. Global alignment mechanism unaffected

### Transaction Safety

**For epoch-scoped transactions:**

```csharp
public class TransactionalBlock : IEpochCompatibleBlock
{
    public async Task ProcessAsync(IEpoch epoch, DataRecord item)
    {
        var db = epoch.GetService<DbContext>();
        
        try
        {
            // Process item
            db.Records.Add(item);
            
            // Commit at epoch completion (via lifecycle hook)
        }
        catch
        {
            // DO NOT commit transaction
            // Epoch disposal will rollback uncommitted transaction
            throw;
        }
    }
}

public class TransactionCoordinator : IEpochLifecycleParticipant
{
    public async ValueTask OnEpochCompletedAsync(
        EpochVector epoch,
        IBlockContext block,
        CancellationToken ct)
    {
        var db = block.CurrentEpoch.GetService<DbContext>();
        
        // Only commit if no errors
        if (!_hasErrors)
        {
            await db.SaveChangesAsync(ct);
        }
        // Otherwise, disposal will rollback
    }
}
```

### Disposal Safety

**Guarantee**: Epoch disposal is safe even if services threw exceptions.

```csharp
public async ValueTask DisposeAsync()
{
    try
    {
        await _scope.DisposeAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error disposing epoch {Vector}", Vector);
        // Swallow - disposal must complete
    }
    finally
    {
        _lock.Dispose();
    }
}
```

---

## 6. Reference Counting and Alignment

### Clarification: Two Separate Mechanisms

**1. Epoch Reference Counting (New)**
- **Purpose**: Track when to dispose epoch DI scope
- **Scope**: Per-epoch object lifetime
- **Managed By**: `EpochManager`
- **Triggers**: 
  - Increment: Block requests epoch
  - Decrement: Block completes epoch

**2. Global Epoch Alignment (Existing)**
- **Purpose**: Coordinate checkpoint boundaries
- **Scope**: Cross-block synchronization
- **Managed By**: `CompletionBasedEpochProgress`
- **Triggers**:
  - Track completion watermarks
  - Fire `OnGlobalEpochAlignedAsync` events

### How They Interact

```
┌─────────────────────────────────────────────────────┐
│         Epoch Lifecycle (New System)                │
│                                                     │
│  Block A requests epoch [s1=5]                     │
│    → Participation tracked                         │
│    → Epoch scope created (if new)                  │
│                                                     │
│  Block A completes epoch [s1=5]                    │
│    → Participation removed                         │
│    → If no more participants:                      │
│      → Release vector reference                    │
│      → If no more vectors:                         │
│        → Dispose epoch scope                       │
│                                                     │
└─────────────────────────────────────────────────────┘
                        │
                        │ Fires event
                        ▼
┌─────────────────────────────────────────────────────┐
│    Global Alignment (Existing System)              │
│                                                     │
│  OnEpochCompletedAsync(vector, block)              │
│    → Update completion watermarks                  │
│    → Check if all blocks aligned                   │
│    → If aligned:                                   │
│      → Fire OnGlobalEpochAlignedAsync             │
│      → Potential checkpoint boundary               │
│                                                     │
└─────────────────────────────────────────────────────┘
```

### Integration Pattern

```csharp
public async ValueTask NotifyEpochCompletedAsync(
    EpochVector vector,
    IBlockContext block,
    CancellationToken ct)
{
    // 1. Reference counting (new system)
    await ReleaseParticipation(vector, block);
    
    // 2. Global alignment (existing system)
    await _lifecycleCoordinator.OnEpochCompletedAsync(vector, block, ct);
    
    // Systems are independent but both triggered by same event
}
```

### Key Principle

**Independence**: Reference counting and global alignment are independent mechanisms:
- Epoch can dispose before global alignment (if blocks finish)
- Global alignment can trigger before epoch disposal (if blocks slow)
- Both are correct and serve different purposes

---

## 7. Performance Targets and Validation

### Benchmark Requirements

**Target**: < 5% overhead vs manual approach

**What to Measure**:
1. **Scope creation time**: `IServiceProvider.CreateScope()`
2. **Service resolution time**: `GetService<T>()`
3. **Reference tracking overhead**: Dictionary operations
4. **Subsume operation cost**: Vector mapping updates

### Benchmark Pattern

```csharp
[Benchmark]
public async Task Manual_EpochScoping()
{
    await using var scope = serviceProvider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DbContext>();
    await ProcessItemsAsync(db);
}

[Benchmark]
public async Task EpochManager_Scoping()
{
    var epoch = epochManager.GetOrCreateEpoch(vector);
    var db = epoch.GetService<DbContext>();
    await ProcessItemsAsync(db);
    await epochManager.NotifyEpochCompletedAsync(vector, block, ct);
}
```

### Acceptance Criteria

| Operation | Target | Max Acceptable |
|-----------|--------|----------------|
| GetOrCreateEpoch | < 10 μs | 50 μs |
| GetService (cached) | < 1 μs | 5 μs |
| NotifyEpochSubsumed | < 5 μs | 20 μs |
| NotifyEpochCompleted | < 10 μs | 50 μs |
| Total overhead per epoch | < 100 μs | 500 μs |

**For epoch with 1000 items**: 
- Per-item overhead: < 0.1 μs
- Percentage overhead: < 0.01% for typical processing

---

## 8. Concurrent Access Safety

### Thread-Safety Guarantees

**EpochManager**:
- `ConcurrentDictionary` for epoch storage → Thread-safe
- Atomic operations for participation tracking → Thread-safe
- No mutable shared state → Thread-safe

**Epoch**:
- Immutable `EpochVector` → Thread-safe
- `IServiceScope` is NOT thread-safe for disposal
- Use `SemaphoreSlim` for disposal coordination → Thread-safe

**Services Resolved from Epoch**:
- Framework provides NO thread-safety guarantees
- Services must be thread-safe if accessed concurrently
- DbContext is NOT thread-safe → Use carefully

### Recommended Pattern for Concurrent Access

```csharp
// ✅ SAFE - Each block gets its own service instances
public async Task ProcessAsync(IEpoch epoch, DataRecord item)
{
    var db = epoch.GetService<DbContext>();  // Same DbContext per epoch
    
    // But blocks should not access concurrently
    // Lock if necessary
    lock (_epochLock)
    {
        db.Records.Add(item);
    }
}

// ✅ SAFER - Use thread-safe services
services.AddScoped<ConcurrentMetricsCollector>();  // Thread-safe by design

public async Task ProcessAsync(IEpoch epoch, DataRecord item)
{
    var metrics = epoch.GetService<ConcurrentMetricsCollector>();
    await metrics.RecordAsync(item);  // Thread-safe internally
}
```

---

## Summary of Refinements

| Area | Refinement | Impact |
|------|-----------|--------|
| **Null Semantics** | CurrentEpoch guaranteed non-null for IEpochCompatibleBlock | Clearer API contract |
| **Service Resolution** | Expose `GetService<T>()` for optional services | Better DX |
| **Participation** | Reactive tracking when blocks request epochs | Simpler implementation |
| **Subsume API** | Document vector immutability requirements | Clearer contract |
| **Error Handling** | Epoch persists on block failure, other blocks continue | Better resilience |
| **Ref Counting** | Independent from global alignment | Clearer separation |
| **Performance** | Specific benchmark targets | Measurable success |
| **Concurrency** | Document thread-safety boundaries | Safer usage |

---

**Status**: ✅ Refinements Complete  
**Next**: Incorporate into Phase 1-6 implementation plans  
**Priority**: High - These clarifications prevent implementation issues
