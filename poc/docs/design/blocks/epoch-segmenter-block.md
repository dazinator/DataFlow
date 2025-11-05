# EpochSegmenterBlock: External Epoch Segmentation

## Overview

`EpochSegmenterBlock<T>` applies epoch segmentation to continuous data streams externally, separating segmentation concerns from data sources. It transforms plain `IAsyncEnumerable<T>` streams into `IAsyncEnumerable<IEpochStream<T>>` based on configurable policies.

### Key Features

- **Policy-Based Segmentation**: Configurable strategies (Count, Key, Clock, Custom, None)
- **External Configuration**: Segmentation logic separate from source implementation
- **Composability**: Bridge between plain and epoch-aware blocks
- **O(1) State**: Minimal memory overhead per segmenter
- **Streaming**: Items flow through without buffering (except Clock mode)

## Architecture

### Components

```
┌────────────────────────────────────────────────────────────────┐
│              EpochSegmenterBlock<T>                            │
│                                                                 │
│  Input: IAsyncEnumerable<T>                                   │
│         (plain continuous stream)                              │
│                       │                                         │
│                       ▼                                         │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │  Segmentation Policy                                     │ │
│  │  • None: Single epoch (pass-through)                    │ │
│  │  • Count: Fixed items per epoch                         │ │
│  │  • Key: Segment on key changes                          │ │
│  │  • Clock: Segment on epoch clock ticks                  │ │
│  │  • Custom: User-defined function                        │ │
│  └─────────────────────────────────────────────────────────┘ │
│                       │                                         │
│                       ▼                                         │
│  Output: IAsyncEnumerable<IEpochStream<T>>                    │
│          (stream of epoch streams)                             │
└────────────────────────────────────────────────────────────────┘
```

### Data Flow

```
PlainSourceBlock → IAsyncEnumerable<T> → EpochSegmenterBlock → IAsyncEnumerable<IEpochStream<T>> → Downstream
```

## Segmentation Policies

### 1. Count-Based Segmentation

Segments by fixed number of items per epoch.

```csharp
var policy = EpochSegmentationPolicy.ByCount(
    itemsPerEpoch: 100,
    sourceId: "orders");

var segmenter = new EpochSegmenterBlock<Order>("segmenter", policy);
```

**Flow:**
```
Input:  [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]  (itemsPerEpoch = 3)
Output: 
  Epoch 1: [1, 2, 3]
  Epoch 2: [4, 5, 6]
  Epoch 3: [7, 8, 9]
  Epoch 4: [10]
```

**Use Cases:**
- Fixed-size batches for batch processing
- Load balancing (equal-sized work units)
- Memory-constrained environments

### 2. Key-Based Segmentation

Segments when key changes, creating natural domain boundaries.

```csharp
var policy = EpochSegmentationPolicy.ByKey<Order, DateTime>(
    keySelector: order => order.Date.Date,
    sourceId: "orders");

var segmenter = new EpochSegmenterBlock<Order>("segmenter", policy);
```

**Flow:**
```
Input:  [(A,1), (A,2), (B,3), (B,4), (A,5)]
Output:
  Epoch 1: [(A,1), (A,2)]  (key = A)
  Epoch 2: [(B,3), (B,4)]  (key = B)
  Epoch 3: [(A,5)]         (key = A again - new epoch)
```

**Use Cases:**
- Date-based partitioning (daily, monthly)
- Customer/tenant-based segmentation
- Natural transaction boundaries
- Time-series data with ordered timestamps

### 3. Clock-Based Segmentation

Segments when external epoch clock advances.

```csharp
var clock = new ManualEpochClock();
var policy = EpochSegmentationPolicy.ByClock(clock, sourceId: "orders");
var segmenter = new EpochSegmenterBlock<Order>("segmenter", policy);

// Advance clock externally to create epoch boundaries
clock.AdvanceEpoch("orders", 2);
```

**Use Cases:**
- Coordinated epoch boundaries across multiple sources
- Time-window based processing
- External event-driven segmentation

### 4. None Policy (Pass-Through)

Wraps entire input in a single epoch - useful as adapter.

```csharp
var policy = EpochSegmentationPolicy.None;
var segmenter = new EpochSegmenterBlock<Order>("adapter", policy);
```

**Flow:**
```
Input:  [1, 2, 3, 4, 5]
Output:
  Epoch 1: [1, 2, 3, 4, 5]  (all items in single epoch)
```

**Use Cases:**
- Bridge plain sources to epoch-aware downstream blocks
- Optional epoch usage without source changes
- Testing/migration scenarios

### 5. Custom Segmentation

User-defined segmentation logic.

```csharp
var policy = EpochSegmentationPolicy.Custom<Order>(
    customSegmenter: input => MyCustomSegmenter(input),
    sourceId: "orders");

async IAsyncEnumerable<IEpochStream<Order>> MyCustomSegmenter(
    IAsyncEnumerable<Order> input)
{
    // Custom logic: segment on order total > threshold
    var currentBatch = new List<Order>();
    var sequence = 1L;
    
    await foreach (var order in input)
    {
        currentBatch.Add(order);
        
        if (currentBatch.Sum(o => o.Total) >= 10000m)
        {
            yield return new EpochStream<Order>(
                EpochVector.FromSingleSource("orders", sequence++),
                currentBatch.ToAsyncEnumerable());
            currentBatch.Clear();
        }
    }
    
    if (currentBatch.Any())
    {
        yield return new EpochStream<Order>(
            EpochVector.FromSingleSource("orders", sequence),
            currentBatch.ToAsyncEnumerable());
    }
}
```

**Use Cases:**
- Complex business rules
- Transaction boundaries
- Dynamic segmentation based on data content

## Usage Patterns

### Basic Pipeline

```csharp
// Source produces plain data
var source = new PlainSourceBlock<Order, OrderSource>("source", scopeFactory);

// Segmenter applies epoch boundaries
var segmenter = new EpochSegmenterBlock<Order>("segmenter",
    EpochSegmentationPolicy.ByCount(100, "orders"));

// Downstream blocks process epochs
var transformer = new TransformerBlock<Order, OrderDto>("transform", ...);
var sink = new ProcessorBlock<OrderDto>("sink", ...);

// Pipeline: source → segmenter → transformer → sink
```

### Multi-Source with Fan-In

Each source has its own segmenter with unique `sourceId`:

```csharp
// Source 1: Orders
var orderSource = new PlainSourceBlock<Order, OrderSource>("orders", scopeFactory);
var orderSegmenter = new EpochSegmenterBlock<Order>("order-seg",
    EpochSegmentationPolicy.ByKey<Order, DateTime>(
        o => o.Date.Date,
        sourceId: "orders"));  // sourceId: "orders"

// Source 2: Payments
var paymentSource = new PlainSourceBlock<Payment, PaymentSource>("payments", scopeFactory);
var paymentSegmenter = new EpochSegmenterBlock<Payment>("payment-seg",
    EpochSegmentationPolicy.ByKey<Payment, DateTime>(
        p => p.Date.Date,
        sourceId: "payments"));  // sourceId: "payments"

// Downstream merge block combines epoch vectors
// EpochVector.Merge() handles multi-source alignment
```

### Adapter Pattern

Use `None` policy to bridge to epoch-aware blocks:

```csharp
var source = new PlainSourceBlock<Order, OrderSource>("source", scopeFactory);

// Adapter wraps stream in single epoch
var adapter = new EpochSegmenterBlock<Order>("adapter",
    EpochSegmentationPolicy.None);

// Now can connect to epoch-aware downstream blocks
var epochProcessor = new EpochAwareBlock<Order>(...);

// source → adapter → epochProcessor
```

## Configuration

### EpochSegmentationPolicy Factory Methods

```csharp
// Count-based
var countPolicy = EpochSegmentationPolicy.ByCount(
    itemsPerEpoch: 100,
    sourceId: "orders");

// Key-based
var keyPolicy = EpochSegmentationPolicy.ByKey<Order, string>(
    keySelector: order => order.CustomerId,
    sourceId: "orders");

// Clock-based
var clock = new ManualEpochClock();
var clockPolicy = EpochSegmentationPolicy.ByClock(
    clock: clock,
    sourceId: "orders");

// Custom
var customPolicy = EpochSegmentationPolicy.Custom<Order>(
    customSegmenter: MySegmenter,
    sourceId: "orders");

// None (pass-through)
var nonePolicy = EpochSegmentationPolicy.None;
```

### Advanced Configuration

```csharp
var policy = new EpochSegmentationPolicy
{
    Mode = SegmentationMode.Key,
    KeySelector = (Order o) => o.Date.Date,
    SourceId = "orders",
    ExecutionPolicy = EpochExecutionPolicy.Overlapped,  // Allow concurrent epochs
    MaxConcurrentEpochs = 4
};
```

## Performance Characteristics

### Memory Overhead

| Policy | State per Segmenter | Notes |
|--------|---------------------|-------|
| Count | ~8 bytes | sequence counter + item counter |
| Key | ~12 bytes | current key + sequence counter |
| Clock | ~8 bytes | clock reference |
| None | ~0 bytes | stateless pass-through |
| Custom | Varies | depends on custom implementation |

### Streaming Behavior

- **Count, Key, None**: Items stream through immediately (no buffering)
- **Clock**: May buffer items until clock advances
- **Custom**: Depends on implementation

### Expected Overhead

From performance validation:
- **Micro-benchmark**: 5-10% overhead (pure enumeration)
- **Realistic workload**: <1-2% overhead (with 30ms processing per item)
- **Conclusion**: Overhead negligible in real-world scenarios

## Integration with Existing Systems

### Lifecycle Events

Segmenter doesn't participate in lifecycle events - downstream blocks do:

```csharp
// Segmenter produces epoch streams
segmenter → epochStream1 → downstream

// Downstream block triggers lifecycle events
downstream.OnEpochCreatedAsync(epochStream1.Epoch);
// ... process items ...
downstream.OnEpochCompletedAsync(epochStream1.Epoch);
```

### EfCore Tracking

Works identically to source-centric approach:

```csharp
var source = new PlainSourceBlock<Order, OrderSource>("source", scopeFactory);
var segmenter = new EpochSegmenterBlock<Order>("segmenter",
    EpochSegmentationPolicy.ByKey<Order, DateTime>(o => o.Date, "orders"));

// EfCore tracking block reacts to epoch boundaries
var trackingBlock = new EfCoreTrackingBlock<Order>(...);

// source → segmenter → trackingBlock
// Tracking block creates/disposes DbContext per epoch
```

### Progress Tracking

Segmenter assigns sequence numbers that progress tracking uses:

```csharp
// Each epoch has monotonically increasing sequence
Epoch 1: sourceId="orders", sequence=1
Epoch 2: sourceId="orders", sequence=2
Epoch 3: sourceId="orders", sequence=3

// Progress tracker computes watermark across all sources
```

## Design Constraints

### 1. Cannot Chain Segmenters

```csharp
// ❌ Invalid: Cannot chain segmenters
var seg1 = new EpochSegmenterBlock<T>("seg1", policy1);
var seg2 = new EpochSegmenterBlock<IEpochStream<T>>("seg2", policy2);
// Type mismatch: seg1 outputs IEpochStream<T>, seg2 expects T

// ✅ Valid: Use single segmenter after source
var source = new PlainSourceBlock<T, MySource>("source", scopeFactory);
var segmenter = new EpochSegmenterBlock<T>("segmenter", policy);
```

### 2. Use Immediately After Source

```csharp
// ✅ Correct placement
PlainSource → Segmenter → Processing Blocks

// ❌ Incorrect: Segmenter in middle of pipeline
PlainSource → Transform → Segmenter → Sink
// Once transformed, data type changes - segmenter expects original type
```

### 3. One Segmenter Per Source

For multi-source scenarios, each source needs its own segmenter with unique `sourceId`:

```csharp
// ✅ Correct: Each source has its own segmenter
source1 → segmenter1(sourceId: "source1") → merge
source2 → segmenter2(sourceId: "source2") → merge

// ❌ Incorrect: Reusing single segmenter for multiple sources
source1 → \
          → sharedSegmenter → downstream  // Epoch vectors will conflict
source2 → /
```

## Best Practices

### 1. Choose Appropriate Policy

```csharp
// ✅ Good: Natural boundaries (dates)
EpochSegmentationPolicy.ByKey<Order, DateTime>(
    o => o.Date.Date,
    "orders");

// ✅ Good: Fixed batches for load balancing
EpochSegmentationPolicy.ByCount(1000, "orders");

// ⚠️ Caution: Count on unsorted data may split related items
// Consider key-based if data has natural groupings
```

### 2. Use Meaningful SourceIds

```csharp
// ✅ Good: Descriptive sourceId
EpochSegmentationPolicy.ByCount(100, sourceId: "customer-orders");

// ❌ Bad: Generic sourceId in multi-source scenario
EpochSegmentationPolicy.ByCount(100, sourceId: "source");
// Causes conflicts when multiple sources merge
```

### 3. Configure Execution Policy

```csharp
// Sequential: One epoch at a time (default)
new EpochSegmentationPolicy { ExecutionPolicy = EpochExecutionPolicy.Sequential };

// Overlapped: Multiple epochs can be processed concurrently
new EpochSegmentationPolicy { 
    ExecutionPolicy = EpochExecutionPolicy.Overlapped,
    MaxConcurrentEpochs = 4
};
```

## Troubleshooting

### Issue: Type Mismatch When Connecting Blocks

**Problem:**
```csharp
var segmenter = new EpochSegmenterBlock<Order>("seg", policy);
var transformer = new TransformerBlock<Order, OrderDto>("transform", ...);
// Cannot connect: segmenter outputs IEpochStream<Order>, transformer expects Order
```

**Solution:**
Downstream blocks after segmenter must handle `IEpochStream<T>`:
```csharp
// Transform within epochs
var transformer = new TransformerBlock<IEpochStream<Order>, IEpochStream<OrderDto>>(
    "transform",
    async (epochStream, context) => {
        var transformedItems = epochStream.Items.Select(o => ToDto(o));
        return new[] { new EpochStream<OrderDto>(epochStream.Epoch, transformedItems) };
    });
```

### Issue: Epochs Not Aligned Across Sources

**Problem:** Multiple sources with same `sourceId` cause conflicting epoch vectors.

**Solution:** Use unique `sourceId` per source:
```csharp
// ✅ Correct
var seg1 = new EpochSegmenterBlock<Order>("seg1",
    EpochSegmentationPolicy.ByCount(100, sourceId: "orders"));
    
var seg2 = new EpochSegmenterBlock<Payment>("seg2",
    EpochSegmentationPolicy.ByCount(100, sourceId: "payments"));
```

## See Also

- [PlainSourceBlock](./plain-source-block.md) - Epoch-agnostic data sources
- [EpochSourceBlock](./epoch-source-block.md) - Source-centric approach (legacy)
- [Decoupled Epoch Architecture](/research/epoch-stream-separation/design/decoupled-epoch-architecture.md)
- [Epoch Vectors](../epoch-vectors.md) - Multi-source epoch tracking
