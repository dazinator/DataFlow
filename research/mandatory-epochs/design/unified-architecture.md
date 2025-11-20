# Design: Unified Epoch-Based Architecture

**Created**: 2025-11-20  
**Status**: Research Prototype

---

## Executive Summary

This design proposes consolidating DataFlow's block architecture by making epochs mandatory for all blocks, treating plain sources as single-epoch sequences, and eliminating duplicate block implementations.

**Key Benefits**:
- 50% reduction in block types (from 12 to ~6)
- Single mental model (everything is epoch-based)
- ~120 lines of duplicated code eliminated
- Simplified testing and maintenance
- Near-zero performance overhead (<1%)

---

## Problem Statement

### Current Architecture: Parallel Block Variants

We maintain two parallel implementations for most block types:

| Concept | Plain Variant | Epoch Variant |
|---------|---------------|---------------|
| Transformation | `ActorBlock<TIn, TOut>` | `EpochActorBlock<TIn, TOut>` |
| Batching | `BatchBlock<T>` | `EpochBatchBlock<T>` |
| Source | `PlainSourceBlock<T>` | `EpochSourceBlock<T>` |
| Producer | `ProducerBlock<T>` | (none - uses EpochSegmenterBlock) |

**Maintenance Issues**:
1. Bug fixes must be applied to both variants
2. Features must be implemented twice
3. Tests must cover both paths
4. Documentation must explain both approaches
5. Users must understand when to use each variant

**Conceptual Complexity**:
- Users must understand plain vs epoch streams
- Must know how to convert between them
- Must choose the right block variant
- Creates cognitive overhead

---

## Solution: Mandatory Epochs

### Core Insight

**A stream with no breaks or epochs is semantically equivalent to one long epoch.**

Therefore:
1. Treat plain sources as single-epoch sequences
2. Keep only epoch-aware block implementations
3. Provide automatic wrapping for plain sources
4. Remove duplicate implementations

### Architectural Principles

1. **Single Mental Model**: Everything is epoch-based internally
2. **Zero Overhead**: Single-epoch case has negligible overhead (<1%)
3. **Backward Compatible**: Plain sources work via automatic wrapping
4. **Explicit Segmentation**: Multi-epoch requires explicit EpochSegmenterBlock
5. **Simplified API**: One block type per concept

---

## Architecture

### Unified Block Types

After unification:

| Concept | Unified Type | Input | Output |
|---------|--------------|-------|--------|
| Transformation | `ActorBlock<TIn, TOut>` | `IEpochStream<TIn>` | `IEpochStream<TOut>` |
| Batching | `BatchBlock<T>` | `IEpochStream<T>` | `IEpochStream<T[]>` |
| Source | `SourceBlock<T>` | - | `IEpochStream<T>` |
| Segmentation | `SegmenterBlock<T>` | `T` | `IEpochStream<T>` |

**Note**: "Epoch" prefix dropped - it's redundant when all blocks are epoch-based.

### Stream Type Hierarchy

```
Plain Stream
    ↓ (automatic wrapping)
Single Epoch Stream
    ↓ (optional segmentation)
Multi-Epoch Stream
```

All blocks operate on epoch streams internally.

---

## Single-Epoch Pattern

### Concept

```csharp
// Plain source produces items
IAsyncEnumerable<int> plainItems = GetItems();

// Wrapped in single epoch
IAsyncEnumerable<IEpochStream<int>> singleEpoch = 
    plainItems.WrapInSingleEpoch("source-name");

// Result: One epoch stream containing all items
// Epoch Vector: {source-name: 1}
```

### Implementation

```csharp
public static async IAsyncEnumerable<IEpochStream<T>> WrapInSingleEpoch<T>(
    this IAsyncEnumerable<T> source,
    string sourceName,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    var epochVector = new EpochVector(
        new Dictionary<string, int> { [sourceName] = 1 });
    
    yield return new EpochStream<T>(epochVector, source);
}
```

**Characteristics**:
- Single iteration of outer loop (one epoch)
- Inner loop iterates all items
- Epoch metadata wraps the stream
- Near-zero allocation overhead

### Performance Analysis

```csharp
// Plain stream (current)
await foreach (var item in plainStream)
{
    Process(item);
}

// Single epoch stream (proposed)
await foreach (var epochStream in singleEpochStream)  // Executes once
{
    await foreach (var item in epochStream.Items)
    {
        Process(item);
    }
}
```

**Overhead**:
- One additional object allocation: `EpochStream<T>` wrapper
- One additional iterator: outer epoch loop (executes once)
- Epoch metadata storage: `EpochVector` (dictionary + metadata)

**Estimated Impact**: <1% for single-epoch case

---

## Block Unification Strategy

### Phase 1: Unify ActorBlock

**Current**:
```csharp
// Plain variant
public class ActorBlock<TIn, TOut, TActor> 
    : BlockBase<TIn, TOut> { ... }

// Epoch variant
public class EpochActorBlock<TIn, TOut, TActor> 
    : BlockBase<IEpochStream<TIn>, IEpochStream<TOut>> { ... }
```

**After Unification**:
```csharp
// Single unified implementation
public class ActorBlock<TIn, TOut, TActor> 
    : BlockBase<IEpochStream<TIn>, IEpochStream<TOut>>
{
    // Same logic as current EpochActorBlock
    public override async IAsyncEnumerable<IEpochStream<TOut>> ExecuteAsync(
        IAsyncEnumerable<IEpochStream<TIn>> input,
        IExecutionContext context)
    {
        await foreach (var epochStream in input)
        {
            yield return new EpochStream<TOut>(
                epochStream.Epoch,
                ProcessEpochItems(epochStream, context));
        }
    }
    
    // ... (rest same as EpochActorBlock)
}

// Old plain variant marked obsolete
[Obsolete("Use ActorBlock - all blocks are now epoch-aware. " +
          "For plain sources, use PlainSourceAdapter or WrapInSingleEpoch.")]
public class ActorBlock<TIn, TOut, TActor> 
    : BlockBase<TIn, TOut> { ... }
```

### Phase 2: Unify BatchBlock

Same pattern as ActorBlock:
1. Keep epoch-aware implementation
2. Rename to remove "Epoch" prefix
3. Deprecate plain variant

### Phase 3: Unify SourceBlock

**Epoch-Aware Sources**:
```csharp
public interface ISourceActor<T>
{
    IAsyncEnumerable<IEpochStream<T>> ProduceEpochsAsync(
        IActorExecutionContext context);
}
```

**Plain Sources (Adapter)**:
```csharp
public interface IPlainSourceActor<T>
{
    IAsyncEnumerable<T> ProduceAsync(IActorExecutionContext context);
}

// Automatically wrapped via PlainSourceAdapter
public class PlainSourceAdapter<T, TActor> 
    : BlockBase<object, IEpochStream<T>>
{
    public override async IAsyncEnumerable<IEpochStream<T>> ExecuteAsync(...)
    {
        var plainStream = actor.ProduceAsync(context);
        await foreach (var epoch in plainStream.WrapInSingleEpoch(sourceName))
        {
            yield return epoch;
        }
    }
}
```

---

## Developer Experience

### Before: Choose Between Variants

```csharp
// User must decide: plain or epoch?
builder.AddActorBlock<int, string, MyActor>("transform");
// OR
builder.AddEpochActorBlock<int, string, MyActor>("transform");

// Confusion: When to use which?
// What if I change my mind later?
```

### After: Single API

```csharp
// Single method - always epoch-based
builder.AddActorBlock<int, string, MyActor>("transform");

// Plain sources automatically wrapped
builder.AddPlainSource<int, MySource>("source");
// Internally: wraps in single epoch

// Explicit segmentation when needed
builder.AddSegmenter<int>("segmenter", 
    policy: EpochSegmentationPolicy.BySize(100));
```

**Benefits**:
- No decision fatigue
- Clear migration path
- Explicit control over segmentation

---

## Graph Builder Integration

### Automatic Epoch Setup

```csharp
public class DataFlowGraphBuilder
{
    /// <summary>
    /// Adds a plain source block with automatic single-epoch wrapping.
    /// </summary>
    public DataFlowGraphBuilder AddPlainSource<T, TActor>(
        string name,
        string? sourceName = null)
        where TActor : IPlainSourceActor<T>
    {
        sourceName ??= name;
        
        var adapter = new PlainSourceAdapter<T, TActor>(
            CreateBlockContext(name),
            _scopeFactory,
            sourceName);
        
        AddBlock(name, adapter);
        return this;
    }

    /// <summary>
    /// Adds an epoch-aware source block.
    /// </summary>
    public DataFlowGraphBuilder AddEpochSource<T, TActor>(
        string name)
        where TActor : ISourceActor<T>
    {
        var source = new EpochSourceBlock<T, TActor>(
            CreateBlockContext(name),
            _scopeFactory);
        
        AddBlock(name, source);
        return this;
    }

    /// <summary>
    /// Adds a segmenter to convert single epochs to multiple epochs.
    /// </summary>
    public DataFlowGraphBuilder AddSegmenter<T>(
        string name,
        EpochSegmentationPolicy policy)
    {
        var segmenter = new EpochSegmenterBlock<T>(
            CreateBlockContext(name),
            policy);
        
        AddBlock(name, segmenter);
        return this;
    }
}
```

---

## Migration Guide

### For Existing Plain Streams

**Before**:
```csharp
builder
    .AddActorBlock<int, string, MyActor>("transform")
    .ReceiveFrom("plain-source");
```

**After (Option 1: Automatic Wrapping)**:
```csharp
builder
    .AddPlainSource<int, MySource>("plain-source")  // Auto-wraps
    .AddActorBlock<int, string, MyActor>("transform")  // Now epoch-aware
    .ReceiveFrom("plain-source");
```

**After (Option 2: Make Source Epoch-Aware)**:
```csharp
// Convert source to epoch-aware
public class MySource : ISourceActor<int>
{
    public async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(...)
    {
        var items = GetItems();
        yield return items.WrapInSingleEpoch("my-source");
    }
}

builder
    .AddEpochSource<int, MySource>("source")
    .AddActorBlock<int, string, MyActor>("transform")
    .ReceiveFrom("source");
```

### For Existing Epoch Streams

No changes needed - already using epochs.

---

## Testing Strategy

### Unit Tests

1. **Single-Epoch Wrapper Tests**:
   - Verify plain stream wraps correctly
   - Verify epoch vector has correct source name
   - Verify items are preserved
   - Verify disposal behavior

2. **Adapter Tests**:
   - Verify PlainSourceAdapter wraps correctly
   - Verify DI scope management
   - Verify cancellation handling

3. **Unified Block Tests**:
   - Test blocks with single-epoch input
   - Test blocks with multi-epoch input
   - Verify behavior identical to current EpochBlocks

### Integration Tests

1. **Single-Epoch Pipeline**:
   ```csharp
   // Plain source → transform → batch → process
   // All using single epoch internally
   ```

2. **Multi-Epoch Pipeline**:
   ```csharp
   // Plain source → segmenter → transform → batch → process
   // Explicit segmentation creates multiple epochs
   ```

### Performance Tests

1. **Benchmark**: Single-epoch vs plain stream overhead
2. **Benchmark**: Unified block vs current EpochBlock performance
3. **Memory**: Allocation overhead of wrapping

**Target**: <5% performance regression for single-epoch case

---

## Backward Compatibility

### Deprecation Strategy

**Phase 1: Deprecate Plain Variants** (v2.0)
```csharp
[Obsolete("Use ActorBlock - all blocks are now epoch-aware.")]
public class ActorBlock<TIn, TOut, TActor> : BlockBase<TIn, TOut> { ... }
```

**Phase 2: Remove Plain Variants** (v3.0)
- Remove deprecated plain block types
- Remove deprecation warnings from unified types
- Update documentation

**Timeline**:
- v2.0: Introduce unified blocks, deprecate plain variants
- v2.1-2.x: Support both, encourage migration
- v3.0: Remove plain variants

---

## Success Criteria

### Quantitative
- ✅ Performance overhead <5% for single-epoch case
- ✅ Code reduction: Eliminate 4+ duplicate block implementations (~120 lines)
- ✅ API surface reduced by ~50%
- ✅ All existing tests pass with unified blocks

### Qualitative
- ✅ Single mental model (everything is epoch-based)
- ✅ Simplified developer experience
- ✅ Clear migration path
- ✅ Improved maintainability

---

## Risks and Mitigation

### Risk 1: Performance Overhead

**Risk**: Single-epoch wrapping adds unacceptable overhead.

**Mitigation**:
- Benchmark early and often
- Optimize wrapper implementation
- Target <1% overhead (actual expected)

**Threshold**: If >5%, reconsider approach.

### Risk 2: Breaking Changes

**Risk**: Existing code breaks with unified architecture.

**Mitigation**:
- Deprecation period (full major version)
- Maintain both APIs during transition
- Provide migration tools and documentation
- Clear communication of timeline

### Risk 3: API Confusion

**Risk**: Users confused by automatic wrapping.

**Mitigation**:
- Clear documentation
- Examples showing both plain and epoch sources
- Builder methods with explicit names
- Compiler warnings for deprecated APIs

---

## Future Enhancements

### 1. Automatic Segmentation Hints

```csharp
builder.AddSource<int, MySource>("source")
    .WithEpochSegmentation(policy: BySize(100));
// Automatically inserts segmenter
```

### 2. Performance Optimizations

- Specialized single-epoch fast path
- Reduced allocations for common cases
- Pooling for epoch metadata

### 3. Enhanced Diagnostics

- Epoch flow visualization
- Performance metrics per epoch
- Debugging tools for epoch boundaries

---

## References

- Block implementations: `/poc/DataFlow.POC/Blocks/`
- Epoch research: `/research/epoch-stream-separation/`
- Epoch coordination: `/research/epoch-source-coordination/`
- Current architecture: `/poc/README.md`

---

## Appendix: Code Comparison

### ActorBlock: Plain vs Unified

**Plain (Current - 81 lines)**:
```csharp
public sealed class ActorBlock<TIn, TOut, TActor> : BlockBase<TIn, TOut>
{
    public override async IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
        IExecutionContext context)
    {
        // ... process plain stream
    }
}
```

**Unified (Proposed - 110 lines, replaces 191 total)**:
```csharp
public sealed class ActorBlock<TIn, TOut, TActor> 
    : BlockBase<IEpochStream<TIn>, IEpochStream<TOut>>
{
    public override async IAsyncEnumerable<IEpochStream<TOut>> ExecuteAsync(
        IAsyncEnumerable<IEpochStream<TIn>> input,
        IExecutionContext context)
    {
        await foreach (var epochStream in input)
        {
            yield return new EpochStream<TOut>(
                epochStream.Epoch,
                ProcessEpochItems(epochStream, context));
        }
    }
}
```

**Net Result**: 191 lines → 110 lines (81 lines eliminated)
