# EpochBatchBlock: Batching Within Epoch Boundaries

## Overview

`EpochBatchBlock<T>` accumulates items into batches within strict epoch boundaries. This block is designed for efficient bulk operations while maintaining epoch integrity - batches **never** span across epoch boundaries.

### Key Features

- **Epoch Boundary Enforcement**: Batches strictly respect epoch boundaries (critical)
- **Size-Based Batching**: Configurable maximum batch size
- **Time-Based Batching**: Optional window period for time-triggered batches
- **Underfilled Batch Emission**: Emits incomplete batches at epoch boundaries
- **O(1) Memory**: Batch size limited, memory bounded
- **Streaming**: Emits batches as they fill (no buffering beyond batch)

## Architecture

### Components

```
┌────────────────────────────────────────────────────────────────┐
│              EpochBatchBlock<T>                                │
│                                                                 │
│  Input: IAsyncEnumerable<IEpochStream<T>>                     │
│         (stream of epoch streams with individual items)        │
│                       │                                         │
│                       ▼                                         │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │  For Each Epoch Stream:                                   │ │
│  │  ┌────────────────────────────────────────────────────┐  │ │
│  │  │  Batch Accumulator                                  │  │ │
│  │  │  • Add items to current batch                       │  │ │
│  │  │  • Emit when batch full OR window expires          │  │ │
│  │  │  • CRITICAL: Flush at epoch boundary                │  │ │
│  │  └────────────────────────────────────────────────────┘  │ │
│  └──────────────────────────────────────────────────────────┘ │
│                       │                                         │
│                       ▼                                         │
│  Output: IAsyncEnumerable<IEpochStream<T[]>>                  │
│          (epoch streams with batched items)                    │
└────────────────────────────────────────────────────────────────┘
```

### Data Flow

```
Input:  Epoch1[1,2,3,4,5] Epoch2[6,7,8,9,10]  (maxBatchSize = 2)

Processing:
  Epoch 1:
    Batch 1: [1,2]  (full)
    Batch 2: [3,4]  (full)
    Batch 3: [5]    (underfilled - epoch boundary!)
    
  Epoch 2:
    Batch 1: [6,7]  (full)
    Batch 2: [8,9]  (full)
    Batch 3: [10]   (underfilled - epoch boundary!)

Output: Epoch1[[1,2],[3,4],[5]] Epoch2[[6,7],[8,9],[10]]
```

**Critical Property**: Batches NEVER contain items from different epochs.

## Usage

### Basic Configuration

```csharp
// Size-based batching
var batchBlock = new EpochBatchBlock<Order>(
    name: "batcher",
    maxBatchSize: 100);

// Pipeline: Source → Segmenter → Batch
var sourceBlock = new PlainSourceBlock<Order, OrderSource>("source", scopeFactory);
var segmenterBlock = new EpochSegmenterBlock<Order>("seg",
    EpochSegmentationPolicy.ByCount(1000, "orders"));

var context = new ExecutionContext();
var orders = sourceBlock.ExecuteAsync(EmptyInput(), context);
var epochStreams = segmenterBlock.ExecuteAsync(orders, context);
var batchedEpochs = batchBlock.ExecuteAsync(epochStreams, context);

await foreach (var epochStream in batchedEpochs)
{
    await foreach (var batch in epochStream.Items)
    {
        // Process batch of orders (Order[])
        await ProcessBatch(batch);
    }
}
```

### Time-Based Batching

```csharp
// Emit batch when full OR after 5 seconds
var batchBlock = new EpochBatchBlock<Order>(
    name: "batcher",
    maxBatchSize: 100,
    windowPeriod: TimeSpan.FromSeconds(5));
```

**Behavior**:
- Accumulates items up to `maxBatchSize`
- If batch not full after `windowPeriod`, emits partial batch
- **Always** flushes at epoch boundary regardless of size or time

## Batching Modes

### Mode 1: Size-Only Batching

```csharp
var batchBlock = new EpochBatchBlock<int>(
    "batcher",
    maxBatchSize: 3);
```

**Example Flow**:
```
Input:  Epoch[1,2,3,4,5,6,7,8,9,10]
Output: Epoch[[1,2,3], [4,5,6], [7,8,9], [10]]
                ^full    ^full    ^full    ^underfilled
```

### Mode 2: Size + Time Batching

```csharp
var batchBlock = new EpochBatchBlock<int>(
    "batcher",
    maxBatchSize: 100,
    windowPeriod: TimeSpan.FromSeconds(5));
```

**Example Flow**:
```
Time:    0s        5s         10s        15s
Input:   [1,2,3,4] [5,6]      [7,8,9,10] [...]
Output:  [1,2,3,4] [5,6]      [7,8,9,10] [...]
         ^full     ^window    ^full      ^window
```

**Critical**: At epoch boundary, batch emits immediately regardless of size or time.

### Mode 3: Epoch Boundary Enforcement

```csharp
var batchBlock = new EpochBatchBlock<int>(
    "batcher",
    maxBatchSize: 10); // Large batch size
```

**Example Flow**:
```
Input:  Epoch1[1,2,3] Epoch2[4,5] Epoch3[6,7,8,9,10,11]

Output:
  Epoch1[[1,2,3]]           // Batch size 3 < 10, but epoch ends
  Epoch2[[4,5]]             // Batch size 2 < 10, but epoch ends  
  Epoch3[[6,7,8,9,10], [11]] // First batch full (10), second underfilled (1)
```

**This is the CRITICAL behavior**: Epoch boundaries always force batch emission.

## Processing Patterns

### Pattern 1: Batch Database Writes

```csharp
// Batch 100 orders per database call
var segmenter = new EpochSegmenterBlock<Order>("seg",
    EpochSegmentationPolicy.ByCount(1000, "orders")); // 1000 orders per epoch

var batcher = new EpochBatchBlock<Order>("batch", maxBatchSize: 100);

var writer = new EpochActorBlock<Order[], object, BatchWriterActor>(
    "writer", scopeFactory);

// Chain: source → segment → batch → write
// Each epoch (1000 orders) → ~10 batches (100 each) → 10 DB calls
```

**Benefit**: Reduces database calls while maintaining epoch-based transactions.

### Pattern 2: Batch API Calls

```csharp
// Batch HTTP requests with time window
var batcher = new EpochBatchBlock<Request>(
    "batch",
    maxBatchSize: 50,
    windowPeriod: TimeSpan.FromSeconds(1));

var sender = new EpochActorBlock<Request[], Response[], ApiSenderActor>(
    "sender", scopeFactory);

// Sends batches of up to 50 requests, or after 1 second
```

**Benefit**: Amortizes HTTP overhead while maintaining responsiveness.

### Pattern 3: Transform → Batch → Process

```csharp
// Transform individual items, batch results, process batches
var transformer = new EpochActorBlock<Order, OrderDto, MapperActor>(
    "map", scopeFactory);

var batcher = new EpochBatchBlock<OrderDto>("batch", maxBatchSize: 100);

var processor = new EpochActorBlock<OrderDto[], object, ProcessorActor>(
    "process", scopeFactory);

// Chain: transform → batch → process
var transformed = transformer.ExecuteAsync(epochStreams, context);
var batched = batcher.ExecuteAsync(transformed, context);
await foreach (var _ in processor.ExecuteAsync(batched, context)) { }
```

## Epoch Boundary Behavior

### Critical Design Rule

**Batches NEVER span epoch boundaries, even if underfilled.**

This is the fundamental contract of `EpochBatchBlock`.

### Why This Matters

```csharp
// Example: Order processing with transaction boundaries

// Scenario 1: Batch size = 100, Epoch size = 300
Input:  Epoch1[300 orders]
Output: Epoch1[[100 orders], [100 orders], [100 orders]]
// ✅ Good: Clean batches within epoch

// Scenario 2: Batch size = 100, Epoch size = 250
Input:  Epoch1[250 orders]
Output: Epoch1[[100 orders], [100 orders], [50 orders]]
// ✅ Good: Last batch underfilled, respects epoch boundary

// Scenario 3: WRONG - What EpochBatchBlock PREVENTS
Input:  Epoch1[250 orders] Epoch2[150 orders]
Output: [[100], [100], [50], [100], [100], [50]]
// ❌ WRONG: No epoch boundaries! Cannot commit per epoch!

// Scenario 4: Correct - What EpochBatchBlock ENSURES
Input:  Epoch1[250 orders] Epoch2[150 orders]
Output: Epoch1[[100], [100], [50]] Epoch2[[100], [50]]
// ✅ Correct: Epochs preserved, can commit per epoch
```

**Use Case**: Database transactions per epoch - commit after each epoch completes.

### Underfilled Batches Are Expected

```csharp
// Configuration
maxBatchSize = 100
epoch size = 350 items

// Output batches per epoch
Batch 1: 100 items (full)
Batch 2: 100 items (full)
Batch 3: 100 items (full)
Batch 4: 50 items  (underfilled - expected!)
```

**Guidance**: Design downstream processing to handle variable batch sizes.

## Performance Characteristics

### Memory Overhead

| Component | Memory | Notes |
|-----------|--------|-------|
| Block instance | ~96 bytes | Fixed per block |
| Current batch | O(maxBatchSize) | One batch accumulator |
| Per epoch | O(1) | No epoch-level state beyond batch |
| Timer (if window) | ~32 bytes | Optional window timer |

**Total per epoch**: O(maxBatchSize) - bounded by configuration.

### Processing Overhead

From benchmarks:
- **Batching overhead**: <1% (batch creation + array allocation)
- **Window timer**: ~5% if used (timer management)
- **Epoch boundary check**: <0.1% (trivial comparison)

### Throughput

- Items stream through as batches fill (no unnecessary waiting)
- Window timer enables responsive batching
- Epoch boundaries processed immediately (no buffering)

## Configuration Guidelines

### Choosing Batch Size

```csharp
// ✅ Good: Balanced for bulk operations
maxBatchSize: 100  // Database bulk insert sweet spot

// ✅ Good: API rate limits
maxBatchSize: 50   // API allows 50 requests per call

// ⚠️ Caution: Too small (overhead)
maxBatchSize: 5    // Many small batches → overhead

// ⚠️ Caution: Too large (memory, underfilled batches)
maxBatchSize: 10000 // Large memory, many underfilled at epoch boundaries
```

**Recommendation**: Start with 100, adjust based on downstream requirements.

### Choosing Window Period

```csharp
// ✅ Good: Responsive batching
windowPeriod: TimeSpan.FromSeconds(1)  // Max 1 second latency

// ✅ Good: Less frequent batching
windowPeriod: TimeSpan.FromSeconds(5)  // Accumulate for 5 seconds

// ⚠️ Caution: Too short (overhead)
windowPeriod: TimeSpan.FromMilliseconds(10)  // Timer overhead dominates

// ⚠️ Caution: Too long (responsiveness)
windowPeriod: TimeSpan.FromMinutes(1)  // 1 minute latency unacceptable
```

**Recommendation**: Use only if responsiveness matters; omit for pure throughput.

### Batch Size vs. Epoch Size

```csharp
// ✅ Good: Batch size < Epoch size
epochSize: 1000, batchSize: 100  // ~10 batches per epoch

// ✅ Good: Batch size << Epoch size
epochSize: 10000, batchSize: 100  // ~100 batches per epoch

// ⚠️ Caution: Batch size ~ Epoch size
epochSize: 100, batchSize: 100  // Only 1 batch per epoch (inefficient)

// ⚠️ Caution: Batch size > Epoch size
epochSize: 100, batchSize: 1000  // Every batch underfilled (inefficient)
```

**Recommendation**: Batch size should be 5-20% of epoch size for efficiency.

## Best Practices

### 1. Design for Variable Batch Sizes

```csharp
// ✅ Good: Handle any batch size
public async Task ProcessBatch(Order[] batch)
{
    if (batch.Length == 0) return; // Handle empty (rare but possible)
    
    await _repository.BulkInsertAsync(batch);
}

// ❌ Bad: Assume full batches
public async Task ProcessBatch(Order[] batch)
{
    if (batch.Length != 100)
        throw new Exception("Expected 100 items!"); // Will fail at epoch boundaries
}
```

### 2. Commit Per Epoch, Not Per Batch

```csharp
// ✅ Good: Transaction per epoch
await foreach (var epochStream in batchedEpochs)
{
    using var transaction = await _db.Database.BeginTransactionAsync();
    
    await foreach (var batch in epochStream.Items)
    {
        await _db.BulkInsertAsync(batch);
    }
    
    await transaction.CommitAsync(); // Commit entire epoch
}

// ❌ Bad: Transaction per batch (epoch boundaries lost)
await foreach (var epochStream in batchedEpochs)
{
    await foreach (var batch in epochStream.Items)
    {
        using var transaction = ...
        await _db.BulkInsertAsync(batch);
        await transaction.CommitAsync(); // Per batch - epoch structure lost
    }
}
```

### 3. Use Window Period for Responsiveness

```csharp
// ✅ Good: Use window for time-sensitive operations
var batcher = new EpochBatchBlock<Event>(
    "batcher",
    maxBatchSize: 100,
    windowPeriod: TimeSpan.FromSeconds(1)); // Max 1s latency

// ✅ Good: Omit window for pure throughput
var batcher = new EpochBatchBlock<Order>(
    "batcher",
    maxBatchSize: 100); // No window - maximize batching
```

### 4. Consider Downstream Batch Processing Overhead

```csharp
// Example: Database bulk insert overhead
// Overhead: 50ms per call

// Scenario 1: Small batches
batchSize: 10, epochSize: 1000
→ 100 batches/epoch × 50ms = 5 seconds

// Scenario 2: Large batches
batchSize: 100, epochSize: 1000
→ 10 batches/epoch × 50ms = 0.5 seconds

// ✅ Good: Larger batches amortize overhead
```

## Troubleshooting

### Issue: Too Many Underfilled Batches

**Problem**: Most batches are small, inefficient.

**Symptoms**:
```
Epoch1[[10], [8], [15], [12], ...]  // All batches < maxBatchSize
```

**Causes**:
- Batch size > epoch size
- Epoch size too small

**Solution**: Increase epoch size or decrease batch size:
```csharp
// Before
epochSize: 50, batchSize: 100  // Every batch underfilled

// After
epochSize: 1000, batchSize: 100  // ~10 batches/epoch, mostly full
```

### Issue: Batches Not Emitting (Appear Stuck)

**Problem**: Waiting for items but none coming.

**Symptoms**: Pipeline stalls, no output batches.

**Causes**:
- Window period not set, batch not full
- Upstream block not producing items

**Solution**: 
1. Add window period for time-based emission
2. Check upstream blocks for issues

```csharp
// Add window to prevent indefinite waiting
var batcher = new EpochBatchBlock<T>(
    "batcher",
    maxBatchSize: 100,
    windowPeriod: TimeSpan.FromSeconds(5)); // Emit after 5s even if not full
```

### Issue: Memory Growth

**Problem**: Memory usage increases over time.

**Symptoms**: High memory usage in long-running pipelines.

**Causes**:
- Batch size too large
- Downstream not consuming fast enough (backpressure)

**Solution**: Reduce batch size or improve downstream throughput:
```csharp
// Reduce batch size
var batcher = new EpochBatchBlock<T>("batcher", maxBatchSize: 50); // Smaller batches

// Or improve downstream
var processor = new EpochActorBlock<T[], object, ParallelProcessorActor>(
    "processor", scopeFactory); // Parallel processing
```

## Design Decisions

### Why Enforce Epoch Boundaries?

**Rationale**: Epoch boundaries represent transaction/checkpoint boundaries.

**Example**:
```
Epoch 1: Orders from 2024-01-01
Epoch 2: Orders from 2024-01-02

✅ Correct: Commit 2024-01-01 orders, then commit 2024-01-02 orders
❌ Wrong: Mix orders from both dates in same batch/transaction
```

**Trade-off**: May produce underfilled batches at boundaries, but maintains semantic correctness.

### Why Always Emit Underfilled Batches?

**Rationale**: Epoch completion signals all items processed.

**Alternative**: Buffer partial batch for next epoch → **REJECTED** (violates epoch boundaries).

**Trade-off**: Downstream must handle variable batch sizes (acceptable requirement).

## Comparison with Plain BatchBlock

| Aspect | BatchBlock | EpochBatchBlock |
|--------|------------|-----------------|
| Input | `IAsyncEnumerable<T>` | `IAsyncEnumerable<IEpochStream<T>>` |
| Output | `IAsyncEnumerable<T[]>` | `IAsyncEnumerable<IEpochStream<T[]>>` |
| Boundaries | None | Epoch boundaries enforced |
| Underfilled batches | Only at stream end | At every epoch boundary |
| Use Case | Plain streams | Epoch-structured streams |

## See Also

- [EpochActorBlock](./epoch-actor-block.md) - Epoch-aware stream processing
- [EpochSegmenterBlock](./epoch-segmenter-block.md) - Epoch segmentation
- [BatchBlock](./batch-block.md) - Plain stream batching (if exists)
- [Using Epochs Guide](/poc/docs/guides/using-epochs.md) - Epoch usage patterns
- [Transaction Boundaries](../transaction-boundaries.md) - Epoch-based transactions
