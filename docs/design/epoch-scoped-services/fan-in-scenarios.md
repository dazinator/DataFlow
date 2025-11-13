# Fan-In Scenarios and Epoch Vector Merging

**Status**: Design Specification  
**Last Updated**: 2025-11-13  
**Addresses**: Feedback on BufferNode and fan-in epoch handling

---

## Overview

This document specifies how epoch-scoped services must behave in fan-in scenarios where multiple upstream `IEpochStream<T>` inputs merge into a single output stream. This is critical for blocks like `BufferNode` and any other block that performs fan-in operations.

---

## Problem Statement

When multiple epoch streams converge (fan-in), the resulting merged stream must correctly handle epoch vector merging and subsumption to ensure:

1. Downstream blocks access the correct epoch-scoped services
2. Epoch lifetimes extend appropriately as vectors merge
3. No premature disposal of epoch resources
4. No accidental creation of duplicate epoch instances

### Example Fan-In Scenario

```
Source A: [s1=5] ───┐
                    ├──> BufferNode ──> [s1=5, s2=3] ──> Downstream
Source B: [s2=3] ───┘
```

**Challenge**: BufferNode receives items from two different epochs and must emit a merged stream with the correct combined epoch vector `[s1=5, s2=3]`.

---

## Requirements for Fan-In Blocks

### 1. Element-Wise Max Merging

When merging epoch vectors, use element-wise maximum:

```csharp
// Merge {s1=5} and {s2=3}
var mergedVector = EpochVector.FromSources(new Dictionary<string, long>
{
    ["s1"] = 5,
    ["s2"] = 3
});

// Merge {s1=5} and {s1=3, s2=2} -> {s1=5, s2=2}
var vector1 = EpochVector.FromSingleSource("s1", 5);
var vector2 = EpochVector.FromSources(new() { ["s1"] = 3, ["s2"] = 2 });
var merged = vector1.Merge(vector2);
// Result: {s1=5, s2=2}
```

**Existing Support**: `EpochVector.Merge()` already implements element-wise max merging.

### 2. Subsume Notification on Vector Expansion

When an incoming item has a **strictly more specific** vector than the current merged vector, trigger subsume notification:

```csharp
// Current merged vector: {s1=5}
var currentVector = EpochVector.FromSingleSource("s1", 5);

// New item arrives with vector: {s1=5, s2=3}
var newVector = EpochVector.FromSources(new() { ["s1"] = 5, ["s2"] = 3 });

// Vector expanded - notify subsume
if (!currentVector.Equals(newVector))
{
    epochManager.NotifyEpochSubsumed(currentVector, newVector);
    currentVector = newVector;
}
```

**Key Point**: `NotifyEpochSubsumed` ensures the epoch object for `{s1=5}` extends its lifetime to also cover `{s1=5, s2=3}`.

### 3. Consistent Epoch Instance Lookup

Downstream blocks must resolve to the **same epoch instance** regardless of which vector key they use:

```csharp
// Before subsume: epoch for {s1=5}
var epoch1 = epochManager.GetOrCreateEpoch(
    EpochVector.FromSingleSource("s1", 5));
var service1 = epoch1.GetService<MyService>();

// After subsume: {s1=5} -> {s1=5, s2=3}
epochManager.NotifyEpochSubsumed(
    EpochVector.FromSingleSource("s1", 5),
    EpochVector.FromSources(new() { ["s1"] = 5, ["s2"] = 3 }));

// Downstream block uses merged vector
var epoch2 = epochManager.GetOrCreateEpoch(
    EpochVector.FromSources(new() { ["s1"] = 5, ["s2"] = 3 }));
var service2 = epoch2.GetService<MyService>();

// MUST be same epoch and same service instance
Assert.Same(epoch1, epoch2);
Assert.Same(service1, service2);
```

### 4. Reference Counting for All Vector Variants

The epoch must not dispose until **all vector variants** have completed:

```csharp
// Blocks processing {s1=5}
await epochManager.NotifyEpochCompletedAsync(
    EpochVector.FromSingleSource("s1", 5), blockA, ct);

// Epoch should NOT dispose yet - {s1=5, s2=3} variant still active

// Blocks processing {s1=5, s2=3}
await epochManager.NotifyEpochCompletedAsync(
    EpochVector.FromSources(new() { ["s1"] = 5, ["s2"] = 3 }), blockB, ct);

// Now epoch can be disposed - all variants complete
```

### 5. No Duplicate Epoch Creation

Fan-in must not accidentally create a new epoch instance just because vectors merged:

```csharp
// WRONG - creates duplicate epoch
var epoch1 = new Epoch(vector1, scope1);
var epoch2 = new Epoch(mergedVector, scope2);  // ❌ Duplicate!

// CORRECT - reuse and extend existing epoch
var epoch = epochManager.GetOrCreateEpoch(vector1);
epochManager.NotifyEpochSubsumed(vector1, mergedVector);
var sameEpoch = epochManager.GetOrCreateEpoch(mergedVector);
Assert.Same(epoch, sameEpoch);  // ✅ Same instance
```

---

## BufferNode Implementation Pattern

### Pattern for Epoch-Aware Fan-In

```csharp
public class BufferNode : IEpochCompatibleBlock
{
    private readonly IEpochManager _epochManager;
    private EpochVector? _currentMergedVector;
    
    public async IAsyncEnumerable<IEpochStream<T>> ProcessAsync(
        IAsyncEnumerable<IEpochStream<T>> input1,
        IAsyncEnumerable<IEpochStream<T>> input2,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Merge inputs
        var mergedInput = MergeStreams(input1, input2);
        
        await foreach (var epochStream in mergedInput.WithCancellation(ct))
        {
            // Check if vector expanded
            if (_currentMergedVector == null)
            {
                _currentMergedVector = epochStream.Epoch;
            }
            else if (!_currentMergedVector.Equals(epochStream.Epoch))
            {
                // Vector expanded - notify subsume
                var oldVector = _currentMergedVector;
                var newVector = _currentMergedVector.Merge(epochStream.Epoch);
                
                if (!oldVector.Equals(newVector))
                {
                    _epochManager.NotifyEpochSubsumed(oldVector, newVector);
                    _currentMergedVector = newVector;
                }
            }
            
            // Emit with merged vector
            yield return new EpochStreamWrapper(
                _currentMergedVector,
                ProcessEpochItems(epochStream.Items, ct));
        }
    }
}
```

---

## Test Requirements

### Test 1: Simple Two-Source Merge

```csharp
[Fact]
public async Task BufferNode_Should_MergeEpochVectorsCorrectly()
{
    // Arrange
    var source1Stream = CreateEpochStream("s1", 5, items1);
    var source2Stream = CreateEpochStream("s2", 3, items2);
    
    // Act
    var bufferNode = new BufferNode(epochManager);
    var output = bufferNode.ProcessAsync(source1Stream, source2Stream);
    
    // Assert
    await foreach (var epochStream in output)
    {
        var expectedVector = EpochVector.FromSources(new()
        {
            ["s1"] = 5,
            ["s2"] = 3
        });
        
        Assert.Equal(expectedVector, epochStream.Epoch);
    }
}
```

### Test 2: Subsume Notification on Merge

```csharp
[Fact]
public async Task BufferNode_Should_NotifySubsumeOnVectorExpansion()
{
    // Arrange
    var mockManager = new Mock<IEpochManager>();
    var bufferNode = new BufferNode(mockManager.Object);
    
    var stream1 = CreateEpochStream("s1", 5, items1);
    var stream2 = CreateEpochStream("s2", 3, items2);
    
    // Act
    await ConsumeStream(bufferNode.ProcessAsync(stream1, stream2));
    
    // Assert
    mockManager.Verify(m => m.NotifyEpochSubsumed(
        EpochVector.FromSingleSource("s1", 5),
        It.Is<EpochVector>(v => 
            v.GetSequence("s1") == 5 && 
            v.GetSequence("s2") == 3)),
        Times.Once);
}
```

### Test 3: Downstream Service Consistency

```csharp
[Fact]
public async Task BufferNode_Merge_Should_PreserveEpochScopedServices()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddScoped<SharedState>();
    var serviceProvider = services.BuildServiceProvider();
    var epochManager = new EpochManager(serviceProvider);
    
    // Create epoch with first vector
    var vector1 = EpochVector.FromSingleSource("s1", 5);
    var epoch1 = epochManager.GetOrCreateEpoch(vector1);
    var state1 = epoch1.GetService<SharedState>();
    state1.Value = 42;
    
    // Simulate subsume (as BufferNode would do)
    var mergedVector = EpochVector.FromSources(new() 
    { 
        ["s1"] = 5, 
        ["s2"] = 3 
    });
    epochManager.NotifyEpochSubsumed(vector1, mergedVector);
    
    // Downstream block accesses with merged vector
    var epoch2 = epochManager.GetOrCreateEpoch(mergedVector);
    var state2 = epoch2.GetService<SharedState>();
    
    // Assert - same epoch, same service, state preserved
    Assert.Same(epoch1, epoch2);
    Assert.Same(state1, state2);
    Assert.Equal(42, state2.Value);
}
```

### Test 4: Reference Counting Across Variants

```csharp
[Fact]
public async Task BufferNode_Merge_Should_NotDisposeUntilAllVariantsComplete()
{
    // Arrange
    var epochManager = new EpochManager(serviceProvider);
    var vector1 = EpochVector.FromSingleSource("s1", 5);
    var mergedVector = EpochVector.FromSources(new() 
    { 
        ["s1"] = 5, 
        ["s2"] = 3 
    });
    
    var epoch = epochManager.GetOrCreateEpoch(vector1);
    epochManager.NotifyEpochSubsumed(vector1, mergedVector);
    
    // Act - complete original vector
    await epochManager.NotifyEpochCompletedAsync(
        vector1, mockBlockContext, CancellationToken.None);
    
    // Assert - epoch still accessible via merged vector
    var epochStillAlive = epochManager.GetOrCreateEpoch(mergedVector);
    Assert.NotNull(epochStillAlive);
    Assert.Same(epoch, epochStillAlive);
    
    // Complete merged vector
    await epochManager.NotifyEpochCompletedAsync(
        mergedVector, mockBlockContext, CancellationToken.None);
    
    // Now epoch should be disposed (verify in implementation)
}
```

### Test 5: Incremental Lineage Growth

```csharp
[Fact]
public async Task BufferNode_Should_HandleIncrementalVectorGrowth()
{
    // Simulate: {s1=5} -> {s1=5, s2=3} -> {s1=5, s2=3, s3=7}
    
    var vector1 = EpochVector.FromSingleSource("s1", 5);
    var vector2 = EpochVector.FromSources(new() { ["s1"] = 5, ["s2"] = 3 });
    var vector3 = EpochVector.FromSources(new() 
    { 
        ["s1"] = 5, 
        ["s2"] = 3, 
        ["s3"] = 7 
    });
    
    var epoch = epochManager.GetOrCreateEpoch(vector1);
    
    // First merge
    epochManager.NotifyEpochSubsumed(vector1, vector2);
    var epoch2 = epochManager.GetOrCreateEpoch(vector2);
    
    // Second merge
    epochManager.NotifyEpochSubsumed(vector2, vector3);
    var epoch3 = epochManager.GetOrCreateEpoch(vector3);
    
    // All should be the same epoch instance
    Assert.Same(epoch, epoch2);
    Assert.Same(epoch, epoch3);
}
```

### Test 6: Multi-Source Fan-In (3+ Sources)

```csharp
[Fact]
public async Task BufferNode_Should_HandleThreeSourceMerge()
{
    // Three sources merging: {s1=5}, {s2=3}, {s3=7}
    
    var stream1 = CreateEpochStream("s1", 5, items1);
    var stream2 = CreateEpochStream("s2", 3, items2);
    var stream3 = CreateEpochStream("s3", 7, items3);
    
    var bufferNode = new BufferNode(epochManager);
    var merged = bufferNode.MergeManyAsync(stream1, stream2, stream3);
    
    await foreach (var epochStream in merged)
    {
        var expectedVector = EpochVector.FromSources(new()
        {
            ["s1"] = 5,
            ["s2"] = 3,
            ["s3"] = 7
        });
        
        Assert.Equal(expectedVector, epochStream.Epoch);
        
        // Verify same epoch-scoped services accessible
        var epoch = epochManager.GetOrCreateEpoch(epochStream.Epoch);
        var service = epoch.GetService<SharedState>();
        Assert.NotNull(service);
    }
}
```

---

## General Fan-In Guidelines

These requirements apply to **any block** that performs fan-in, not just `BufferNode`:

### When to Merge Vectors

```csharp
// Merge when:
// 1. Multiple upstream IEpochStream<T> converge
// 2. Items from different epochs mix in output

// Do NOT merge when:
// 1. Processing single epoch stream
// 2. Simple transforms that preserve epoch boundaries
// 3. Routing/filtering that doesn't combine epochs
```

### Pattern for Any Fan-In Block

```csharp
public abstract class FanInBlockBase<TIn, TOut>
{
    protected readonly IEpochManager _epochManager;
    private readonly Dictionary<string, long> _activeVectorState = new();
    
    protected EpochVector UpdateMergedVector(EpochVector incoming)
    {
        bool changed = false;
        
        foreach (var (sourceId, sequence) in incoming.Sequences)
        {
            if (!_activeVectorState.TryGetValue(sourceId, out var current) 
                || sequence > current)
            {
                _activeVectorState[sourceId] = sequence;
                changed = true;
            }
        }
        
        if (changed)
        {
            var oldVector = EpochVector.FromSources(
                new Dictionary<string, long>(_activeVectorState));
            var newVector = incoming;
            
            if (!oldVector.Equals(newVector))
            {
                _epochManager.NotifyEpochSubsumed(oldVector, newVector);
            }
        }
        
        return EpochVector.FromSources(
            new Dictionary<string, long>(_activeVectorState));
    }
}
```

---

## Implementation Checklist

### For EpochManager

- [x] `NotifyEpochSubsumed` creates vector reference mapping
- [x] `GetOrCreateEpoch` returns same instance for subsumed vectors
- [x] Reference counting tracks all vector variants
- [x] Disposal waits for all variants to complete
- [ ] Add fan-in specific unit tests

### For BufferNode (Example Implementation)

- [ ] Implement vector merging logic
- [ ] Call `NotifyEpochSubsumed` on vector expansion
- [ ] Emit merged epoch vector in output stream
- [ ] Add all 6 test scenarios above
- [ ] Stress test with many sources and high concurrency

### For Other Fan-In Blocks

- [ ] Identify all blocks that perform fan-in
- [ ] Implement vector merging pattern
- [ ] Add subsume notifications
- [ ] Add test coverage per block

---

## Performance Considerations

### Merge Operation Overhead

- **Vector merging**: O(n) where n = number of sources
- **Subsume notification**: Dictionary operation (O(1))
- **Expected overhead**: < 1% for typical fan-in scenarios (2-10 sources)

### Optimization Strategies

1. **Cache merged vectors**: Don't recompute if inputs haven't changed
2. **Lazy subsume**: Batch subsume notifications if many occur rapidly
3. **Vector interning**: Reuse immutable vector instances

---

## Open Questions

1. **Maximum fan-in sources**: Should we limit how many sources can merge? (Probably not - let it scale naturally)

2. **Concurrent merges**: What if two threads merge different vectors simultaneously? (Use locking in `UpdateMergedVector`)

3. **Partial completion**: What if some sources complete but others continue? (Each vector completes independently, epoch persists until all complete)

---

## Related Documentation

- **Main Design**: [README.md](README.md)
- **Subsume Semantics**: [subsume-semantics.md](subsume-semantics.md)
- **EpochVector**: `/poc/DataFlow.POC/Core/EpochVector.cs`

---

**Status**: ✅ Specification Complete  
**Next**: Implement tests during Phase 3 (Epoch Vector Subsume Support)  
**Priority**: High - Critical for correctness in multi-source scenarios
