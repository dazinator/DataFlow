# Epochs

## What are Epochs?

An **epoch** is a logical boundary in a data stream that represents a unit of work with clear start and completion semantics. Epochs enable precise coordination of processing boundaries across concurrent operations in a distributed pipeline.

## Why Epochs?

In traditional streaming systems, determining when all data from a logical batch has been fully processed is challenging, especially when:
- Data flows through multiple concurrent processing stages
- Multiple independent sources feed into the same pipeline
- Processing happens asynchronously with varying speeds

Epochs solve this by providing:
- **Clear boundaries** - Data is segmented into discrete processing units
- **Completion tracking** - Know precisely when all data in an epoch has finished processing
- **Transaction safety** - Commit only after all related processing completes
- **Checkpoint coordination** - Save state at known-good boundaries

## Core Concept

Instead of treating data as a continuous stream with out-of-band control signals, epochs segment the stream into **substreams**:

```csharp
// Traditional: continuous stream
IAsyncEnumerable<T>

// With epochs: stream of epoch-bounded substreams  
IAsyncEnumerable<IEpochStream<T>>
```

Each `IEpochStream<T>`:
- Carries an `EpochVector` identifying the epoch
- Contains only items belonging to that specific epoch
- Completes naturally when all epoch data has been processed
- Provides a synchronization point for coordination

## Key Properties

### Natural Completion
Epoch completion is determined by **stream exhaustion** rather than control signals. When an epoch stream finishes yielding items, the epoch is complete for that processing stage.

### Multi-Source Coordination
Epochs support multiple independent data sources through `EpochVector`, which tracks progress across all sources simultaneously. See [Epoch Vectors](./epoch-vectors.md) for details.

### Processing Modes

**Sequential**: Each epoch completes before the next begins
```
Epoch 1 [====] → Epoch 2 [====] → Epoch 3 [====]
```

**Overlapped**: Multiple epochs can be processed concurrently
```
Epoch 1 [========]
Epoch 2      [========]
Epoch 3           [========]
```

## Benefits

### Correct Alignment
Unlike control-signal-based approaches, epochs ensure alignment based on actual data completion:
- ✅ No premature completion signals
- ✅ All in-flight data is accounted for
- ✅ Deterministic checkpoint boundaries

### Composability
Epochs flow naturally through pipelines:
- Transform blocks receive and emit epoch streams
- Fan-out preserves epoch identity
- Fan-in merges epochs using vector operations

### Transaction Safety
Epochs provide safe boundaries for committing transactions:
- Only commit after global epoch alignment
- Guarantee all processing for an epoch is complete
- Support atomic multi-sink transactions

## When to Use Epochs

Epochs are beneficial when you need:

✅ **Transaction coordination** - Commit data only after complete processing  
✅ **Checkpoint consistency** - Save state at known-good boundaries  
✅ **Multi-source synchronization** - Coordinate processing from multiple sources  
✅ **Exactly-once semantics** - Prevent duplicate or lost data in failure scenarios  
✅ **Progress tracking** - Monitor pipeline progress with clear milestones

## Trade-offs

### Benefits
- Precise completion semantics
- Safe transaction boundaries
- Multi-source coordination
- Natural backpressure

### Costs
- Slightly increased memory (epoch metadata)
- Coordination overhead for alignment checks
- Additional abstraction layer

For most use cases involving stateful processing or transaction management, the benefits significantly outweigh the costs.

## Related Concepts

- [Epoch Vectors](./epoch-vectors.md) - Multi-source epoch coordination
- [Global Alignment](./global-alignment.md) - Determining safe checkpoint boundaries
- [Transaction Boundaries](./transaction-boundaries.md) - When to commit safely
- [Lifecycle Events](./lifecycle-events.md) - Reacting to epoch lifecycle

## References

- Phase 3: Stream-per-epoch design and rationale
- Phase 6: Lifecycle integration and transaction patterns
