# EpochSegmenter Multi-Source Usage Patterns

## Overview

This document provides guidance on using `EpochSegmenterBlock` in multi-source scenarios with the decoupled epoch segmentation design.

## Key Principle

**With decoupled segmentation, epoch concerns are separated from sources**. This fundamentally changes how multi-source scenarios are handled compared to the old source-centric model.

## Multi-Source Patterns

### Pattern 1: Unified-Then-Segment (Recommended)

**When to use**: Most multi-source scenarios where per-source tracking is not required.

**Advantages**:
- Creates single-source epochs (no ancestry)
- Simple to understand and implement
- No lifecycle coordination needed
- Works with standard epoch-aware blocks

**Pattern**:
```csharp
// Step 1: Create plain sources
var source1 = new PlainSourceBlock<Order, OrderSource1>("source1", scopeFactory);
var source2 = new PlainSourceBlock<Order, OrderSource2>("source2", scopeFactory);

// Step 2: Unify sources into single stream
var unifiedStream = UnifyStreams(
    source1.ExecuteAsync(empty, ctx),
    source2.ExecuteAsync(empty, ctx));

// Step 3: Apply segmentation to unified stream
var segmenter = new EpochSegmenterBlock<Order>(
    "unified-segmenter",
    EpochSegmentationPolicy.ByCount(100, sourceId: "orders"));

var epochStreams = segmenter.ExecuteAsync(unifiedStream, ctx);

// Step 4: Process with epoch-aware blocks
var transformer = new EpochTransformerBlock<Order, OrderDto>("map", ...);
var batcher = new EpochBatchBlock<OrderDto>("batch", maxBatchSize: 50);
var processor = new EpochProcessorBlock<OrderDto[]>("save", ...);
```

**Result**: All epochs have sourceId "orders", no multi-source vectors.

**Epoch Vectors**:
```
Epoch 1: {orders=1}   [items 1-100 from source1 and source2 mixed]
Epoch 2: {orders=2}   [items 101-200 from source1 and source2 mixed]
Epoch 3: {orders=3}   ...
```

### Pattern 2: Segment-Then-Merge (Advanced)

**When to use**: When per-source progress tracking is required.

**Advantages**:
- Tracks each source independently
- Enables per-source watermarks

**Disadvantages**:
- Creates multi-source epochs at merge points
- Requires epoch vector subsumption tracking
- Needs lifecycle-aware blocks (`IEpochLifecycleParticipant`)
- More complex to implement correctly

**Pattern**:
```csharp
// Step 1: Create plain sources
var source1 = new PlainSourceBlock<Order, OrderSource1>("source1", scopeFactory);
var source2 = new PlainSourceBlock<Order, OrderSource2>("source2", scopeFactory);

// Step 2: Apply segmentation per source
var segmenter1 = new EpochSegmenterBlock<Order>(
    "seg1",
    EpochSegmentationPolicy.ByCount(100, sourceId: "source1"));

var segmenter2 = new EpochSegmenterBlock<Order>(
    "seg2",
    EpochSegmentationPolicy.ByCount(100, sourceId: "source2"));

var epochStream1 = segmenter1.ExecuteAsync(source1.ExecuteAsync(empty, ctx), ctx);
var epochStream2 = segmenter2.ExecuteAsync(source2.ExecuteAsync(empty, ctx), ctx);

// Step 3: Merge epoch streams (creates multi-source epochs)
var mergedStreams = MergeBlock.ExecuteAsync(epochStream1, epochStream2, ctx);

// Step 4: Process with lifecycle-aware blocks
var batcher = new TransactionalEpochBatchBlock<Order>("batch", ...); // Lifecycle-aware
var processor = new EpochProcessorBlock<Order[]>("save", ...);
```

**Result**: Epochs at merge points have multiple sourceIds.

**Epoch Vectors**:
```
From source1: {source1=1}       [100 items from source1]
From source2: {source2=1}       [100 items from source2]
At merge:     {source1=1, source2=1}  [merged - subsumes both parents]
```

**Important**: Standard epoch-aware blocks (from prototype) may create tiny batches at merge points. Use lifecycle-aware variants that batch until `OnGlobalEpochAlignedAsync` for proper transaction boundaries.

## Producer Groups

### Recommended: Group-Level Segmentation

For producer groups (multiple producers, limited concurrency), apply segmentation at the group level:

```csharp
// Producer group: 4 producers, max 2 running concurrently
var producerGroup = new ProducerGroupBlock<Order>(
    maxConcurrency: 2,
    producers: [producer1, producer2, producer3, producer4]);

// Single segmenter for entire group
var segmenter = new EpochSegmenterBlock<Order>(
    "group-segmenter",
    EpochSegmentationPolicy.ByCount(100, sourceId: "order-group"));

var epochStreams = segmenter.ExecuteAsync(
    producerGroup.ExecuteAsync(empty, ctx),
    ctx);
```

**Result**: All epochs single-source with "order-group" sourceId.

**Trade-off**: Loses ability to track individual producer progress, but dramatically simplifies the architecture.

## Decision Tree

```
Do you need to track progress per source independently?
│
├─ NO → Use Unified-Then-Segment pattern (Pattern 1)
│       ✓ Simpler
│       ✓ Works with standard epoch-aware blocks
│       ✓ No ancestry tracking
│
└─ YES → Use Segment-Then-Merge pattern (Pattern 2)
         ⚠️ More complex
         ⚠️ Requires lifecycle-aware blocks
         ⚠️ Need to handle epoch vector subsumption
```

## Epoch Streams vs Epochs (Transaction Boundaries)

**Important Distinction**:

- **Epoch Stream**: `IEpochStream<T>` - data carrier with `EpochVector` metadata
  - In Pattern 1 (single-source): Epoch stream boundary = transaction boundary
  - In Pattern 2 (multi-source): Epoch stream boundaries ≠ transaction boundaries (due to merges)

- **Epoch (Transaction)**: Lifecycle span from `OnEpochCreatedAsync` → `OnGlobalEpochAlignedAsync`
  - Safe to commit transactions only at `OnGlobalEpochAlignedAsync`
  - In Pattern 2, epochs expand to subsume ancestors before completion

**Implication for Batching**:
- Pattern 1: Batching within epoch streams = batching within transactions ✓
- Pattern 2: Batching within epoch streams may create sub-transaction batches (use lifecycle-aware batcher)

## Type Requirements

**Pattern 1 (Unified-Then-Segment)**:
- All sources must produce (or be transformable to) the same type `T`
- Required for unification: `IAsyncEnumerable<T>` from each source

**Pattern 2 (Segment-Then-Merge)**:
- Sources can produce different types initially
- Must converge to same type before merge
- Merge point: `IAsyncEnumerable<IEpochStream<T>>`

## Examples

### Example 1: Two Order Sources

```csharp
// Pattern 1: Unified-Then-Segment
var normalOrders = new PlainSourceBlock<Order, NormalOrderSource>("normal", sf);
var priorityOrders = new PlainSourceBlock<Order, PriorityOrderSource>("priority", sf);

var unified = UnifyStreams(
    normalOrders.ExecuteAsync(empty, ctx),
    priorityOrders.ExecuteAsync(empty, ctx));

var segmenter = new EpochSegmenterBlock<Order>(
    "orders",
    EpochSegmentationPolicy.ByCount(1000, sourceId: "all-orders"));

// Result: Single-source epochs {all-orders=1}, {all-orders=2}, ...
```

### Example 2: Different Initial Types

```csharp
// Pattern 1 with transformation
var csvSource = new PlainSourceBlock<CsvRow, CsvSource>("csv", sf);
var apiSource = new PlainSourceBlock<ApiResponse, ApiSource>("api", sf);

// Transform to common type
var csvOrders = transformer1.ExecuteAsync(csvSource.ExecuteAsync(empty, ctx), ctx);
var apiOrders = transformer2.ExecuteAsync(apiSource.ExecuteAsync(empty, ctx), ctx);

// Then unify and segment
var unified = UnifyStreams(csvOrders, apiOrders);
var segmenter = new EpochSegmenterBlock<Order>("seg", 
    EpochSegmentationPolicy.ByCount(500, sourceId: "orders"));
```

## Performance Considerations

**Pattern 1**:
- Minimal overhead (single sourceId)
- No epoch vector merging
- Standard epoch-aware blocks work efficiently

**Pattern 2**:
- Overhead from epoch vector operations (Merge, Subsumes, FindMostSpecificAncestor)
- Context promotion at merge points (if using DbContext tracking)
- Lifecycle coordination overhead

**Recommendation**: Use Pattern 1 unless per-source tracking is a hard requirement.

## Testing Multi-Source Patterns

See experimental validation tests in:
`/research/flow-composability-unification/handover/prototype/MultiSourceSegmentationExperiments.cs`

These tests validate:
1. Unified-then-segment creates single-source epochs ✓
2. Segment-then-merge creates multi-source epochs with ancestry
3. Epoch stream boundaries align with transactions in single-source
4. Producer groups work with single segmenter ✓

## Future Considerations

### Graph Validation

Currently there are no hard constraints preventing "unsupported" compositions like:
- Multiple segmenters on the same stream
- Mixing plain and epoch streams incorrectly

**Recommendation**: Wait until POC features stabilize, then add graph validation to detect:
- Multiple segmenters (except when intentional for pattern 2)
- Type mismatches (plain block receiving epoch stream, etc.)
- Invalid merge configurations

### Lifecycle-Aware Block Variants

The prototype demonstrates epoch-aware blocks for Pattern 1 (single-source). For Pattern 2, implement lifecycle-aware variants:

```csharp
public class TransactionalEpochBatchBlock<T> : 
    BlockBase<IEpochStream<T>, IEpochStream<T[]>>,
    IEpochLifecycleParticipant
{
    // Batches until OnGlobalEpochAlignedAsync
    // Respects actual transaction boundaries, not just epoch stream boundaries
}
```

## Summary

**Recommended for Most Cases**: Pattern 1 (Unified-Then-Segment)
- Unify sources → Single segmenter → Standard epoch-aware blocks
- Simple, efficient, no ancestry complexity

**Use Pattern 2 Only When**: Per-source progress tracking is required
- Segment per source → Merge → Lifecycle-aware blocks
- Complex, requires careful handling of epoch vector evolution

The decoupled segmentation design makes Pattern 1 the natural choice, eliminating much of the complexity that existed in the old source-centric model.
