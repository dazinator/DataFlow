# Epoch Vector and Stream Segmentation Design

## Overview

This document describes the Phase 3 epoch management system that fixes the premature alignment bug discovered in Phase 2. The new design uses a **stream-per-epoch model** where each epoch is represented as a substream that naturally completes when all data for that epoch has been processed.

## Problem Statement

### The Premature Alignment Bug

Phase 2's epoch control plane had a critical issue: epochs were acknowledged based on **broadcast timing** rather than **data completion**. This led to:

```
1. Source emits 1000 data items for Epoch 1
2. Data items are queued in channels (still unprocessed)
3. Source broadcasts "Epoch 1 complete" event
4. Downstream blocks receive broadcast and update progress immediately
5. Alignment check passes → "Epoch 1 aligned" ✓
6. BUT: 900 data items are still in channels being processed!
```

**Impact:**
- Checkpoints acknowledged before all in-flight data is processed
- False completion signals when the pipeline still has work in progress
- Potential data loss in recovery scenarios if state is persisted too early

## Solution: Stream-Per-Epoch Model

### Core Concept

Instead of one continuous stream with out-of-band epoch events, we segment the stream into **epoch substreams**:

```csharp
IAsyncEnumerable<T>                    // Old: continuous stream
    ↓
IAsyncEnumerable<IEpochStream<T>>      // New: stream of epoch streams
```

Each `IEpochStream<T>`:
- Carries its own `EpochVector` metadata
- Yields only items belonging to that epoch
- Completes naturally when all epoch data is drained
- Can be processed before the next epoch starts (Sequential policy)
- Can overlap with other epochs (Overlapped policy)

### Key Advantages

✅ **Correct Alignment** - Epochs marked complete only after stream finishes
✅ **Natural Boundaries** - Stream completion = epoch completion
✅ **No Race Conditions** - Data and control are synchronized
✅ **Flexible Concurrency** - Sequential or overlapped execution
✅ **Type Safe** - Strongly typed epoch streams

## Core Abstractions

### 1. EpochVector

Multi-source epoch tracking with vector operations:

```csharp
public sealed record EpochVector
{
    public ImmutableDictionary<string, long> Sequences { get; }
    
    // Creation
    static EpochVector FromSingleSource(string sourceId, long sequence);
    static EpochVector FromSources(Dictionary<string, long> sequences);
    
    // Operations
    EpochVector Merge(EpochVector other);              // Fan-in: element-wise max
    bool IsLessThanOrEqual(EpochVector other);         // Comparison
    EpochVector IncrementSource(string sourceId);      // Advance epoch
}
```

**Vector Composition Rules:**

| Operation | Rule | Use Case |
|-----------|------|----------|
| Single source | Monotonic increment | Linear pipeline |
| Fan-in (merge) | Element-wise max | Multiple sources to one block |
| Fan-out | Inherit parent vector | One source to multiple blocks |
| Transform | Propagate or map | Processing blocks |

**Examples:**

```csharp
// Single source
var epoch = EpochVector.FromSingleSource("source1", 5);
// EpochVector[source1=5]

// Fan-in: merge two sources
var v1 = EpochVector.FromSources(new Dictionary<string, long> 
{
    ["source1"] = 10,
    ["source2"] = 5
});

var v2 = EpochVector.FromSources(new Dictionary<string, long>
{
    ["source1"] = 8,
    ["source2"] = 12
});

var merged = v1.Merge(v2);
// EpochVector[source1=10, source2=12]  (max of each)

// Comparison
v1.IsLessThanOrEqual(merged);  // true
```

### 2. IEpochStream<T>

Represents a stream of items belonging to a specific epoch:

```csharp
public interface IEpochStream<out T>
{
    EpochVector Epoch { get; }           // Epoch identity
    IAsyncEnumerable<T> Items { get; }   // Data for this epoch
}
```

**Key Properties:**
- Immutable epoch metadata
- Lazy item enumeration
- Natural completion boundary
- Composable and chainable

### 3. EpochSegmenter

Converts continuous streams into epoch-scoped substreams:

```csharp
public static class EpochSegmenter
{
    // Clock-based segmentation
    static IAsyncEnumerable<IEpochStream<T>> SegmentByEpoch<T>(
        IAsyncEnumerable<T> input,
        IEpochClock clock,
        EpochSegmenterConfig? config = null,
        CancellationToken cancellationToken = default);
    
    // Key-based segmentation
    static IAsyncEnumerable<IEpochStream<T>> SegmentByKey<T, TKey>(
        IAsyncEnumerable<T> input,
        Func<T, TKey> epochKeySelector,
        string sourceId,
        EpochSegmenterConfig? config = null,
        CancellationToken cancellationToken = default);
}
```

**Configuration:**

```csharp
public sealed class EpochSegmenterConfig
{
    public int BufferCapacity { get; init; } = 256;
    public int MaxConcurrentEpochs { get; init; } = 4;
    public EpochExecutionPolicy ExecutionPolicy { get; init; } 
        = EpochExecutionPolicy.Sequential;
}

public enum EpochExecutionPolicy
{
    Sequential,  // One epoch at a time
    Overlapped   // Multiple concurrent epochs (bounded)
}
```

### 4. CompletionBasedEpochProgress

Tracks epoch completion based on actual data drain:

```csharp
public sealed class CompletionBasedEpochProgress
{
    void RegisterEpochStarted(EpochVector epoch);
    void RegisterEpochCompleted(EpochVector epoch);
    
    bool IsEpochCompleted(EpochVector epoch);
    IReadOnlyList<EpochVector> GetCompletedEpochs();
    EpochVector GetHighestCompletedEpoch();
    
    int ClearCompletedEpochsUpTo(EpochVector maxEpoch);
    EpochProgressStatistics GetStatistics();
}
```

**Usage Pattern:**

```csharp
var progress = new CompletionBasedEpochProgress();

await foreach (var epochStream in EpochSegmenter.SegmentByKey(...))
{
    progress.RegisterEpochStarted(epochStream.Epoch);
    
    // Process all items in epoch
    await foreach (var item in epochStream.Items)
    {
        await ProcessItemAsync(item);
    }
    
    // Only mark complete after stream is fully drained
    progress.RegisterEpochCompleted(epochStream.Epoch);
}
```

### 5. GlobalEpochAlignment

Coordinates alignment across multiple blocks:

```csharp
public sealed class GlobalEpochAlignment
{
    CompletionBasedEpochProgress GetOrCreateBlockProgress(string blockName);
    
    bool IsGloballyAligned(EpochVector epoch);
    EpochVector GetGlobalCompletionWatermark();
    
    GlobalAlignmentStatistics GetStatistics();
}
```

**Global Watermark Calculation:**

The safe checkpoint boundary is the **minimum epoch** completed by **all blocks** for each source:

```csharp
var alignment = new GlobalEpochAlignment();
var block1Progress = alignment.GetOrCreateBlockProgress("block1");
var block2Progress = alignment.GetOrCreateBlockProgress("block2");

// block1 completed epochs 1, 2, 3
block1Progress.RegisterEpochCompleted(EpochVector.FromSingleSource("s1", 1));
block1Progress.RegisterEpochCompleted(EpochVector.FromSingleSource("s1", 2));
block1Progress.RegisterEpochCompleted(EpochVector.FromSingleSource("s1", 3));

// block2 only completed epochs 1, 2
block2Progress.RegisterEpochCompleted(EpochVector.FromSingleSource("s1", 1));
block2Progress.RegisterEpochCompleted(EpochVector.FromSingleSource("s1", 2));

// Global watermark is epoch 2 (slowest block)
var watermark = alignment.GetGlobalCompletionWatermark();
// EpochVector[s1=2]
```

## Usage Examples

### Example 1: Simple Key-Based Segmentation

```csharp
var data = ProduceData(); // IAsyncEnumerable<Order>

// Segment by day
var epochs = EpochSegmenter.SegmentByKey(
    data,
    order => order.Date.Date,  // Key selector
    "order-source");

await foreach (var epochStream in epochs)
{
    Console.WriteLine($"Processing epoch: {epochStream.Epoch}");
    
    var itemCount = 0;
    await foreach (var order in epochStream.Items)
    {
        await ProcessOrderAsync(order);
        itemCount++;
    }
    
    Console.WriteLine($"Completed epoch with {itemCount} orders");
}
```

### Example 2: Clock-Based Segmentation with Progress Tracking

```csharp
var clock = new ManualEpochClock();
var progress = new CompletionBasedEpochProgress();

clock.SetEpoch(EpochVector.FromSingleSource("stream1", 1));

var epochs = EpochSegmenter.SegmentByEpoch(
    dataStream,
    clock,
    new EpochSegmenterConfig 
    { 
        ExecutionPolicy = EpochExecutionPolicy.Sequential,
        BufferCapacity = 1000
    });

await foreach (var epochStream in epochs)
{
    progress.RegisterEpochStarted(epochStream.Epoch);
    
    try
    {
        await ProcessEpochAsync(epochStream.Items);
        progress.RegisterEpochCompleted(epochStream.Epoch);
        
        // Safe to checkpoint here
        await CheckpointAsync(epochStream.Epoch);
    }
    catch (Exception ex)
    {
        // Epoch not completed - can retry from last checkpoint
        Console.WriteLine($"Epoch {epochStream.Epoch} failed: {ex.Message}");
        throw;
    }
}
```

### Example 3: Multi-Source Fan-In

```csharp
// Source 1 produces epoch streams
var source1Epochs = EpochSegmenter.SegmentByKey(
    source1Data,
    item => item.BatchId,
    "source1");

// Source 2 produces epoch streams
var source2Epochs = EpochSegmenter.SegmentByKey(
    source2Data,
    item => item.BatchId,
    "source2");

// Merge epochs using vector composition
await foreach (var (epoch1, epoch2) in ZipEpochs(source1Epochs, source2Epochs))
{
    // Merge vectors
    var mergedVector = epoch1.Epoch.Merge(epoch2.Epoch);
    
    Console.WriteLine($"Processing merged epoch: {mergedVector}");
    
    // Process both streams
    await Task.WhenAll(
        ProcessStreamAsync(epoch1.Items),
        ProcessStreamAsync(epoch2.Items));
}
```

### Example 4: Global Alignment for Checkpointing

```csharp
var alignment = new GlobalEpochAlignment();

// Register blocks
var producerProgress = alignment.GetOrCreateBlockProgress("producer");
var transformerProgress = alignment.GetOrCreateBlockProgress("transformer");
var sinkProgress = alignment.GetOrCreateBlockProgress("sink");

// Each block processes epochs and reports completion
// ... (processing logic) ...

// Periodically check for safe checkpoint boundary
var watermark = alignment.GetGlobalCompletionWatermark();

if (!watermark.Equals(EpochVector.None))
{
    Console.WriteLine($"Safe to checkpoint up to: {watermark}");
    await CommitCheckpointAsync(watermark);
    
    // Cleanup old epoch tracking
    producerProgress.ClearCompletedEpochsUpTo(watermark);
    transformerProgress.ClearCompletedEpochsUpTo(watermark);
    sinkProgress.ClearCompletedEpochsUpTo(watermark);
}
```

## Execution Policies

### Sequential Policy

One epoch in flight at a time. Simplest and safest.

**Characteristics:**
- ✅ Minimal memory usage
- ✅ Predictable resource consumption
- ✅ Simple reasoning about state
- ❌ Lower throughput
- ❌ No pipelining

**Use When:**
- Strong ordering required
- Limited memory available
- Simplicity preferred over throughput

```csharp
var config = new EpochSegmenterConfig
{
    ExecutionPolicy = EpochExecutionPolicy.Sequential
};

await foreach (var epochStream in EpochSegmenter.SegmentByKey(..., config))
{
    // Only one epoch processed at a time
    await ProcessEpochAsync(epochStream);
}
```

### Overlapped Policy

Multiple epochs in flight with bounded concurrency.

**Characteristics:**
- ✅ Higher throughput
- ✅ Natural backpressure
- ✅ Rate limiting via max concurrent epochs
- ⚠️ Higher memory usage
- ⚠️ More complex state management

**Use When:**
- High throughput required
- Sufficient memory available
- Epochs are independent
- Processing time varies per epoch

```csharp
var config = new EpochSegmenterConfig
{
    ExecutionPolicy = EpochExecutionPolicy.Overlapped,
    MaxConcurrentEpochs = 4  // At most 4 epochs in flight
};

// Process epochs concurrently up to the limit
var tasks = new List<Task>();

await foreach (var epochStream in EpochSegmenter.SegmentByKey(..., config))
{
    if (tasks.Count >= config.MaxConcurrentEpochs)
    {
        // Wait for one to complete before starting next
        await Task.WhenAny(tasks);
        tasks.RemoveAll(t => t.IsCompleted);
    }
    
    tasks.Add(ProcessEpochAsync(epochStream));
}

await Task.WhenAll(tasks);
```

## Alignment Semantics

### Block-Level Progress

Each block tracks its own epoch progress:

```
Block State = { Started: [e1, e2, e3], Completed: [e1, e2] }
```

- **Started** - Epoch stream has been received and processing begun
- **Completed** - All items in epoch stream have been processed

### Global Alignment

Global alignment occurs when **all blocks** have completed an epoch:

```
Block1: Completed [e1, e2, e3]
Block2: Completed [e1, e2]
Block3: Completed [e1, e2, e3, e4]

Global Watermark: e2  (min of all blocks)
```

### Checkpoint Guarantees

After committing a checkpoint at epoch E:

✅ **Guaranteed** - All data items with epoch ≤ E have been processed by all blocks
✅ **Guaranteed** - No in-flight data exists for epochs ≤ E
✅ **Guaranteed** - State is consistent across all blocks at epoch E
❌ **Not Guaranteed** - Data for epochs > E may or may not be processed

## Performance Considerations

### Memory Usage

**Sequential Policy:**
- One epoch buffered at a time
- Memory = BufferCapacity × ItemSize
- Predictable and bounded

**Overlapped Policy:**
- Multiple epochs buffered concurrently
- Memory = MaxConcurrentEpochs × BufferCapacity × ItemSize
- Higher but still bounded

### Throughput

**Hot Path:**
- No per-item type checks ✅
- No control signal routing ✅
- Direct enumeration ✅
- Natural async/await flow ✅

**Cold Path (epoch boundaries):**
- Vector creation and comparison
- Progress tracking updates
- Alignment checks

**Expected Overhead:**
- Sequential: ~2-5% vs raw stream processing
- Overlapped: ~1-3% (amortized over concurrent epochs)

### Latency

**Sequential Policy:**
- Latency = EpochProcessingTime
- No concurrency benefits

**Overlapped Policy:**
- Latency ≈ EpochProcessingTime / min(Concurrency, MaxConcurrentEpochs)
- Pipelining reduces end-to-end latency

## Testing and Validation

### Test Coverage

✅ **EpochVectorTests** (15 tests)
- Vector creation and composition
- Merge operations (fan-in)
- Comparison operators
- Increment operations
- Equality and hashing

✅ **EpochSegmenterTests** (8 tests)
- Key-based segmentation
- Clock-based segmentation
- Completion tracking
- Global alignment
- Fix for known premature alignment bug

### Correctness Tests

The test suite validates:

1. **Monotonic Sequences** - Each source has increasing sequence numbers
2. **Vector Composition** - Merge follows element-wise max rule
3. **Completion-Based Progress** - Epochs only marked complete after stream drain
4. **Global Watermarks** - Minimum across all blocks computed correctly
5. **Sequential Processing** - One epoch at a time when policy is Sequential
6. **Premature Alignment Fix** - Test demonstrates bug is fixed

### Known Limitations

1. **Buffering Strategy** - Current implementation collects items into lists. For production, consider streaming buffers.

2. **Clock-Based Segmentation** - Simplified implementation. Production may need more sophisticated coordination.

3. **Error Handling** - Current design doesn't specify retry or failure recovery semantics for partial epoch processing.

4. **Ordering Guarantees** - Overlapped policy may interleave epoch completion. If strict ordering required, use Sequential policy.

## Migration from Phase 2

### Phase 2 (Out-of-Band Epochs)

```csharp
// Old approach - broadcast based
var epochManager = new EpochManager();
epochManager.RegisterPublisher("source", producer);
epochManager.RegisterSubscriber("block", block);

// Premature alignment possible!
await epochManager.BroadcastEpochAsync(marker, ct);
```

### Phase 3 (Stream Segmentation)

```csharp
// New approach - completion based
var epochs = EpochSegmenter.SegmentByKey(
    dataStream,
    item => item.EpochKey,
    "source");

var progress = new CompletionBasedEpochProgress();

await foreach (var epochStream in epochs)
{
    progress.RegisterEpochStarted(epochStream.Epoch);
    
    await foreach (var item in epochStream.Items)
    {
        await ProcessAsync(item);
    }
    
    // Alignment only after stream completes
    progress.RegisterEpochCompleted(epochStream.Epoch);
}
```

## Future Enhancements

### Potential Improvements

1. **Streaming Buffers** - Replace list-based buffering with channel-based streaming
2. **Automatic Checkpointing** - Integrate with checkpoint coordinator
3. **Epoch Timeout Detection** - Detect and handle stalled epochs
4. **Dynamic Policy Switching** - Change execution policy at runtime
5. **Source Generation** - Generate typed segmenters at compile time
6. **Epoch Metadata** - Rich metadata beyond just vector sequences
7. **Recovery from Checkpoint** - Resume processing from a saved epoch

### Integration Points

- **DI Scope Rotation** - Apply scoped DI per epoch if needed
- **ActorBlock** - Combine with actor model for per-epoch actors
- **Metrics** - Collect epoch timing and completion metrics
- **Observability** - Trace epoch flow through pipeline
- **Persistence** - Save/restore epoch progress for recovery

## Conclusion

The stream-per-epoch model provides:

✅ **Correctness** - No premature alignment
✅ **Simplicity** - Natural stream boundaries
✅ **Flexibility** - Sequential or overlapped execution
✅ **Performance** - Minimal overhead on hot path
✅ **Type Safety** - Strongly typed throughout
✅ **Composability** - Vector operations for multi-source scenarios

This design resolves the Phase 2 premature alignment bug while providing a clean, extensible foundation for checkpoint-based dataflow resumption.
