# Block Pair Analysis: Plain vs Epoch Variants

**Date**: 2025-11-20  
**Purpose**: Analyze differences between plain and epoch block variants

---

## Overview

Currently, we maintain parallel implementations for blocks that work with either:
- **Plain streams**: `IAsyncEnumerable<T>`
- **Epoch streams**: `IAsyncEnumerable<IEpochStream<T>>`

---

## Block Pairs

### 1. ActorBlock vs EpochActorBlock

**ActorBlock<TIn, TOut, TActor>**
- Input: `IAsyncEnumerable<TIn>`
- Output: `IAsyncEnumerable<TOut>`
- Processes continuous stream of items
- Actor can request scope rotation at any time
- ~80 lines of code

**EpochActorBlock<TIn, TOut, TActor>**
- Input: `IAsyncEnumerable<IEpochStream<TIn>>`
- Output: `IAsyncEnumerable<IEpochStream<TOut>>`
- Processes items within epoch boundaries
- Actor can request scope rotation at any time
- Preserves epoch metadata
- ~110 lines of code

**Key Differences**:
1. EpochActorBlock wraps output in `EpochStream<TOut>` with epoch metadata
2. EpochActorBlock processes items per epoch (outer loop over epochs)
3. Both share identical actor rotation logic
4. Both use same `IStreamActor<TIn, TOut>` interface

**Code Similarity**: ~90% identical logic

---

### 2. BatchBlock vs EpochBatchBlock

**BatchBlock<T>**
- Input: `IAsyncEnumerable<T>`
- Output: `IAsyncEnumerable<T[]>`
- Accumulates items into batches
- Batches can span indefinitely
- Uses timer for window periods
- ~120 lines of code

**EpochBatchBlock<T>**
- Input: `IAsyncEnumerable<IEpochStream<T>>`
- Output: `IAsyncEnumerable<IEpochStream<T[]>>`
- Batches items within epoch boundaries
- Batches CANNOT cross epochs
- Each epoch is batched independently
- ~140 lines of code

**Key Differences**:
1. EpochBatchBlock enforces epoch boundaries (no cross-epoch batches)
2. EpochBatchBlock processes each epoch independently
3. Both use same batching logic (size + time window)
4. Timer and batch accumulation identical

**Code Similarity**: ~85% identical logic

---

### 3. ProducerBlock vs EpochSourceBlock

**ProducerBlock<T>**
- Input: `IAsyncEnumerable<object>` (ignored)
- Output: `IAsyncEnumerable<T>`
- Simple function-based producer
- No epoch knowledge
- ~35 lines of code

**EpochSourceBlock<T, TActor>**
- Input: `IAsyncEnumerable<object>` (ignored)
- Output: `IAsyncEnumerable<IEpochStream<T>>`
- Actor-based producer with DI scope
- Produces epoch streams
- Uses `ISourceActor<T>`
- ~55 lines of code

**Key Differences**:
1. ProducerBlock uses simple function, EpochSourceBlock uses actor
2. EpochSourceBlock has DI scope management
3. Different abstraction levels (function vs actor)

**Code Similarity**: ~30% (different abstraction approaches)

---

### 4. PlainSourceBlock vs EpochSourceBlock

**PlainSourceBlock<T, TActor>**
- Input: `IAsyncEnumerable<object>` (ignored)
- Output: `IAsyncEnumerable<T>`
- Actor produces continuous stream
- Uses `IPlainSourceActor<T>`
- ~50 lines of code

**EpochSourceBlock<T, TActor>**
- Input: `IAsyncEnumerable<object>` (ignored)
- Output: `IAsyncEnumerable<IEpochStream<T>>`
- Actor produces epoch streams
- Uses `ISourceActor<T>`
- ~55 lines of code

**Key Differences**:
1. Different actor interfaces (`IPlainSourceActor` vs `ISourceActor`)
2. EpochSourceBlock yields epoch streams
3. Both have identical DI scope management

**Code Similarity**: ~95% identical

---

## Conversion Strategy: Plain → Epoch

### Strategy 1: Automatic Single-Epoch Wrapper

Wrap plain streams in a single epoch automatically:

```csharp
// Plain source producing IAsyncEnumerable<T>
var plainSource = GetPlainItems();

// Wrap in single epoch
var singleEpochStream = WrapInSingleEpoch(plainSource, "source-name");
// Output: IAsyncEnumerable<IEpochStream<T>> with one epoch containing all items
```

**Implementation**:
```csharp
public static async IAsyncEnumerable<IEpochStream<T>> WrapInSingleEpoch<T>(
    IAsyncEnumerable<T> source,
    string sourceName,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    var epoch = new Epoch(new Dictionary<string, int> { [sourceName] = 1 });
    yield return new EpochStream<T>(epoch, source);
}
```

**Benefits**:
- Zero overhead (just wrapping metadata)
- Semantically correct (one continuous epoch)
- Backward compatible (can still have plain sources)

---

### Strategy 2: Graph Builder Convenience

```csharp
// Old API: user chooses plain vs epoch
builder.AddActorBlock<int, string, MyActor>("transform");
builder.AddEpochActorBlock<int, string, MyActor>("transform");

// New API: single method, automatic epoch handling
builder.AddActorBlock<int, string, MyActor>("transform");
// Always epoch-based internally, plain sources auto-wrapped
```

---

## Code Duplication Analysis

### Duplicated Logic

1. **Actor Rotation Pattern**:
   - Both ActorBlock and EpochActorBlock have identical rotation logic
   - ~40 lines duplicated

2. **Batching Logic**:
   - Both BatchBlock and EpochBatchBlock have identical batching
   - ~60 lines duplicated

3. **DI Scope Management**:
   - Source blocks share identical scope patterns
   - ~20 lines duplicated

**Total Duplication**: ~120 lines across 4 block pairs

### Maintenance Burden

- Any bug fix must be applied to both variants
- Any feature addition must be implemented twice
- Tests must cover both paths
- Documentation must explain both approaches

---

## Conceptual Complexity

### Current Model: Two Parallel Worlds

Users must understand:
1. Plain streams vs Epoch streams
2. When to use each variant
3. How to convert between them (EpochSegmenterBlock)
4. Different actor interfaces (IStreamActor vs ISourceActor vs IPlainSourceActor)

### Proposed Model: Single Epoch World

Users understand:
1. Everything is epoch-based
2. Plain sources automatically get single epoch
3. Segmentation is explicit choice (EpochSegmenterBlock for multi-epoch)
4. One actor interface per concept

**Cognitive Load Reduction**: ~50%

---

## Performance Implications

### Single Epoch Overhead

```csharp
// Plain stream (current)
foreach (var item in stream) { ... }

// Single epoch stream (proposed)
foreach (var epochStream in stream) {  // One iteration
    foreach (var item in epochStream.Items) { ... }
}
```

**Expected Overhead**:
- One additional object allocation (EpochStream wrapper)
- One additional iteration level (outer loop executes once)
- Epoch metadata storage (negligible)

**Estimated Impact**: <1% for single-epoch case

### Multi-Epoch Case

No change - already using epochs.

---

## Migration Path

### Phase 1: Deprecate (Non-Breaking)

```csharp
[Obsolete("Use ActorBlock - all blocks are now epoch-aware")]
public sealed class ActorBlock<TIn, TOut, TActor> { ... }

// Recommend EpochActorBlock
public sealed class EpochActorBlock<TIn, TOut, TActor> { ... }
```

### Phase 2: Rename (Breaking)

```csharp
// Remove "Epoch" prefix - it's redundant when all blocks are epoch-based
public sealed class ActorBlock<TIn, TOut, TActor> 
    : BlockBase<IEpochStream<TIn>, IEpochStream<TOut>> { ... }
```

### Phase 3: Automatic Wrapping

```csharp
// Builder auto-wraps plain sources
builder.AddSource("plain-source", () => GetPlainItems());
// Internally wraps in single epoch automatically
```

---

## Recommendations

### ✅ DO: Unify on Epochs

1. **Remove plain variants**: Keep only epoch-aware blocks
2. **Auto-wrap plain sources**: Single-epoch wrapper for non-segmented sources
3. **Rename blocks**: Drop "Epoch" prefix once it's the only option
4. **Simplify interfaces**: One actor interface per concept

**Benefits**:
- ~120 lines of code eliminated
- 50% reduction in API surface
- Single mental model
- Easier testing and maintenance

### ✅ DO: Provide Migration Tools

1. **Deprecation warnings**: Guide users to new API
2. **Auto-conversion utilities**: Help migrate existing code
3. **Documentation**: Clear migration guide

### ❌ DON'T: Rush Breaking Changes

1. Keep deprecated APIs for one major version
2. Provide clear migration timeline
3. Ensure backward compatibility where possible

---

## Next Steps

1. Prototype single-epoch wrapper
2. Convert ActorBlock to epoch-only
3. Benchmark performance impact
4. Validate with existing tests
5. Document unified architecture
