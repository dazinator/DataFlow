# Epoch Vector Subsume Semantics for Epoch-Scoped Services

**Status**: Draft  
**Last Updated**: 2025-11-13

---

## Overview

This document details how epoch-scoped services should behave when epoch vector subsume operations occur. Subsume operations are a critical part of the epoch tracking system, and proper handling is essential for correctness.

---

## What is Epoch Vector Subsume?

An **epoch vector subsume operation** occurs when one epoch vector is merged into another, typically in fan-in scenarios or when multiple sources align.

### Example Scenario

```
Source A produces: epoch [A=5]
Source B produces: epoch [B=3]

Fan-in merge point creates: epoch [A=5, B=3]

This is a subsume operation where:
- [A=5] is subsumed into [A=5, B=3]
- [B=3] is subsumed into [A=5, B=3]
```

### Why This Matters for Services

If blocks have resolved services from epoch `[A=5]`, and then epoch `[A=5]` is subsumed into `[A=5, B=3]`, those services need to remain accessible and valid.

**Key Requirement**: The DI scope for epoch `[A=5]` must extend its lifetime to cover the merged epoch `[A=5, B=3]`.

---

## Subsume Strategies

### Strategy 1: Epoch Reference Transfer

**Approach**: When epoch A is subsumed into epoch B, transfer the epoch object reference.

```csharp
public void NotifyEpochSubsumed(EpochVector from, EpochVector to)
{
    if (_activeEpochs.TryGetValue(from, out var epochFrom))
    {
        // Register the same epoch object under the new vector
        _activeEpochs.TryAdd(to, epochFrom);
        
        // Increment reference count for the new vector
        epochFrom.AddReference();
        
        // Original vector reference will be decremented when blocks complete
    }
}
```

**Pros**:
- ✅ Services resolved from `from` vector remain valid
- ✅ Services resolved from `to` vector get the same instances
- ✅ Simple implementation
- ✅ No service state migration needed

**Cons**:
- ⚠️ Multiple vector keys point to same epoch object
- ⚠️ Need careful reference counting

**When to Use**: This is the **recommended** approach for most scenarios.

### Strategy 2: Epoch Merging

**Approach**: Merge the DI scopes and service instances from both epochs.

```csharp
public void NotifyEpochSubsumed(EpochVector from, EpochVector to)
{
    var epochFrom = _activeEpochs.GetOrAdd(from, CreateEpoch);
    var epochTo = _activeEpochs.GetOrAdd(to, CreateEpoch);
    
    // Merge service instances from epochFrom into epochTo
    MergeServiceInstances(epochFrom, epochTo);
    
    // Mark epochFrom as subsumed
    epochFrom.MarkAsSubsumedInto(epochTo);
}
```

**Pros**:
- ✅ Can handle complex service state merging
- ✅ Distinct epoch objects for each vector

**Cons**:
- ❌ Complex implementation
- ❌ Service state migration is error-prone
- ❌ DI scopes are not designed to be merged
- ❌ Risk of losing service state

**When to Use**: Only if service state merging is absolutely required (rare).

**Verdict**: **Not recommended** - DI scopes are not designed for merging.

---

## Reference Counting with Subsume

### Challenge

```
Block A processes epoch [A=5]
  → Resolves service from epoch [A=5]
  → Epoch [A=5] is subsumed into [A=5, B=3]
  → Block A completes
  → Should epoch be disposed?

Answer: NO - epoch [A=5, B=3] may still be in use
```

### Solution: Vector-Based Reference Tracking

Track references per vector, transfer references on subsume:

```csharp
internal sealed class EpochManager : IEpochManager
{
    private readonly ConcurrentDictionary<EpochVector, Epoch> _activeEpochs = new();
    
    public IEpoch GetOrCreateEpoch(EpochVector vector)
    {
        return _activeEpochs.GetOrAdd(vector, v =>
        {
            var scope = _rootServiceProvider.CreateScope();
            var epoch = new Epoch(v, scope);
            
            // Initial reference for this vector
            epoch.AddVectorReference(v);
            
            return epoch;
        });
    }
    
    public void NotifyEpochSubsumed(EpochVector from, EpochVector to)
    {
        if (_activeEpochs.TryGetValue(from, out var epochFrom))
        {
            // Register under new vector
            _activeEpochs.TryAdd(to, epochFrom);
            
            // Add reference for the new vector
            epochFrom.AddVectorReference(to);
            
            // Note: 'from' vector reference will be released when blocks complete
        }
    }
    
    public async ValueTask NotifyEpochCompletedAsync(
        EpochVector vector, 
        IBlockContext block, 
        CancellationToken cancellationToken)
    {
        if (_activeEpochs.TryGetValue(vector, out var epoch))
        {
            // Release reference for this specific vector
            var shouldDispose = await epoch.TryReleaseVectorAsync(vector);
            
            if (shouldDispose)
            {
                // Remove ALL vector entries pointing to this epoch
                foreach (var kvp in _activeEpochs)
                {
                    if (ReferenceEquals(kvp.Value, epoch))
                    {
                        _activeEpochs.TryRemove(kvp.Key, out _);
                    }
                }
            }
        }
    }
}
```

Updated `Epoch` class:

```csharp
internal sealed class Epoch : IEpoch
{
    private readonly IServiceScope _scope;
    private readonly ConcurrentDictionary<EpochVector, int> _vectorReferences = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    // ... other members
    
    internal void AddVectorReference(EpochVector vector)
    {
        _vectorReferences.AddOrUpdate(
            vector, 
            1, 
            (_, count) => count + 1);
    }
    
    internal async ValueTask<bool> TryReleaseVectorAsync(EpochVector vector)
    {
        await _lock.WaitAsync();
        try
        {
            if (_vectorReferences.TryGetValue(vector, out var count))
            {
                count--;
                if (count <= 0)
                {
                    _vectorReferences.TryRemove(vector, out _);
                }
                else
                {
                    _vectorReferences[vector] = count;
                }
            }
            
            // Dispose only when ALL vectors have released
            if (_vectorReferences.IsEmpty)
            {
                await DisposeAsync();
                return true;
            }
            
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }
}
```

---

## Test Scenarios

### Scenario 1: Simple Subsume

```csharp
[Fact]
public async Task Subsume_ShouldPreserveServiceInstances()
{
    // Arrange
    var manager = new EpochManager(serviceProvider);
    var vectorA = EpochVector.FromSingleSource("source-a", 5);
    var vectorAB = EpochVector.FromSources(new() { ["source-a"] = 5, ["source-b"] = 3 });
    
    // Act
    var epochA = manager.GetOrCreateEpoch(vectorA);
    var serviceFromA = epochA.GetService<MyService>();
    
    manager.NotifyEpochSubsumed(vectorA, vectorAB);
    
    var epochAB = manager.GetOrCreateEpoch(vectorAB);
    var serviceFromAB = epochAB.GetService<MyService>();
    
    // Assert
    Assert.Same(epochA, epochAB); // Same epoch object
    Assert.Same(serviceFromA, serviceFromAB); // Same service instance
}
```

### Scenario 2: Multiple Subsumes

```csharp
[Fact]
public async Task MultipleSubsumes_ShouldMaintainCorrectReferences()
{
    // Arrange
    var manager = new EpochManager(serviceProvider);
    var vectorA = EpochVector.FromSingleSource("a", 1);
    var vectorB = EpochVector.FromSingleSource("b", 1);
    var vectorAB = EpochVector.FromSources(new() { ["a"] = 1, ["b"] = 1 });
    
    // Act - Create epochs
    var epochA = manager.GetOrCreateEpoch(vectorA);
    var epochB = manager.GetOrCreateEpoch(vectorB);
    
    // Subsume both into merged epoch
    manager.NotifyEpochSubsumed(vectorA, vectorAB);
    manager.NotifyEpochSubsumed(vectorB, vectorAB);
    
    // Complete original vectors
    await manager.NotifyEpochCompletedAsync(vectorA, mockBlockContext, CancellationToken.None);
    
    // Epoch should NOT be disposed yet - vectorB reference still active
    var epochAB = manager.GetOrCreateEpoch(vectorAB);
    Assert.NotNull(epochAB);
    
    await manager.NotifyEpochCompletedAsync(vectorB, mockBlockContext, CancellationToken.None);
    
    // Now epoch may be disposed if vectorAB is also complete
}
```

### Scenario 3: Subsume Chain

```csharp
[Fact]
public async Task SubsumeChain_ShouldExtendLifetime()
{
    // A -> AB -> ABC (chain of subsumes)
    var vectorA = EpochVector.FromSingleSource("a", 1);
    var vectorAB = EpochVector.FromSources(new() { ["a"] = 1, ["b"] = 1 });
    var vectorABC = EpochVector.FromSources(new() { ["a"] = 1, ["b"] = 1, ["c"] = 1 });
    
    var epochA = manager.GetOrCreateEpoch(vectorA);
    var service = epochA.GetService<MyService>();
    
    manager.NotifyEpochSubsumed(vectorA, vectorAB);
    manager.NotifyEpochSubsumed(vectorAB, vectorABC);
    
    // Service should still be accessible through the chain
    var epochABC = manager.GetOrCreateEpoch(vectorABC);
    var serviceFromABC = epochABC.GetService<MyService>();
    
    Assert.Same(service, serviceFromABC);
}
```

---

## Edge Cases

### Case 1: Subsume Before First Access

**Scenario**: Epoch is subsumed before any block accesses it.

```csharp
var vectorA = EpochVector.FromSingleSource("a", 1);
var vectorAB = EpochVector.FromSources(new() { ["a"] = 1, ["b"] = 1 });

// Subsume BEFORE accessing epochA
manager.NotifyEpochSubsumed(vectorA, vectorAB);

// First access via vectorAB
var epoch = manager.GetOrCreateEpoch(vectorAB);
```

**Expected Behavior**: 
- No epoch exists for vectorA yet
- Create new epoch for vectorAB
- No special handling needed

**Implementation**: GetOrAdd handles this naturally.

### Case 2: Concurrent Subsume and Completion

**Scenario**: Block completes epoch while subsume is happening.

```csharp
// Thread 1: Block completes epoch [A=5]
await manager.NotifyEpochCompletedAsync(vectorA, block, ct);

// Thread 2: Subsume [A=5] into [A=5, B=3] (concurrent)
manager.NotifyEpochSubsumed(vectorA, vectorAB);
```

**Expected Behavior**:
- Thread-safe operations
- Epoch not disposed if subsume added new reference
- Correct final reference count

**Implementation**: Use locking in `Epoch.TryReleaseVectorAsync` and `EpochManager.NotifyEpochSubsumed`.

### Case 3: Subsume with Different Service States

**Scenario**: Services have mutable state, subsume occurs.

```csharp
var epochA = manager.GetOrCreateEpoch(vectorA);
var tracker = epochA.GetService<StateTracker>();
tracker.RecordEvent("event1");

manager.NotifyEpochSubsumed(vectorA, vectorAB);

var epochAB = manager.GetOrCreateEpoch(vectorAB);
var trackerAB = epochAB.GetService<StateTracker>();

// trackerAB.Events should contain "event1"
Assert.Same(tracker, trackerAB);
```

**Expected Behavior**: Service state preserved (same instance).

**Implementation**: Reference transfer strategy naturally preserves state.

---

## Design Decision: Reference Transfer (Recommended)

**Decision**: Use the **Reference Transfer** strategy for subsume operations.

**Rationale**:
1. DI scopes are not designed to be merged
2. Service instances may have mutable state
3. Reference transfer is simpler and more predictable
4. Performance is better (no service migration overhead)
5. Correctness is easier to reason about

**Implementation**: See code examples above.

**Trade-offs Accepted**:
- Multiple vector keys may point to the same epoch object
- Need careful tracking of vector-specific references
- Slightly more complex reference counting logic

**Benefits Gained**:
- Service state is preserved automatically
- No risk of state loss during merge
- Simpler mental model for developers
- Better performance

---

## Open Questions

1. **Max Vector References**: Should we limit how many vectors can reference one epoch? (Answer: Probably not needed - natural cleanup)

2. **Subsume Notification Order**: Does order matter if multiple subsumes happen? (Answer: Should be commutative)

3. **Circular Subsumes**: Can we have A→B→A? (Answer: No - epoch vectors are monotonic)

4. **Performance**: What's the overhead of vector tracking? (Answer: TBD - benchmark in implementation)

---

## Next Steps

1. Implement reference transfer strategy in Phase 3
2. Create comprehensive tests for all scenarios
3. Benchmark performance overhead
4. Document any discovered edge cases
5. Update main design doc with findings
