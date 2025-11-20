# DataFlow POC - Glossary of Terms

## Core Concepts

### Epoch
A logical segment or batch of data items that flow together through the pipeline. Epochs provide:
- Transaction boundaries
- Checkpoint markers for recovery
- Resource scoping (e.g., per-epoch DbContext)
- Ordering guarantees within the segment

**Example**: Process 1,000 records as Epoch 1, then next 1,000 as Epoch 2.

### Epoch Vector
A multi-dimensional identifier that tracks the progression of data across multiple independent sources.

**Structure**: `Dictionary<int, long>` where:
- **Key**: Source ID (which source produced this epoch)
- **Value**: Sequence number (monotonically increasing per source)

**Examples**:
- `{sourceId=1, sequence=5}` - Epoch 5 from source 1
- `{sourceId=1, sequence=5, sourceId=2, sequence=3}` - Merged epoch from sources 1 and 2

**Operations**:
- **Element-wise max**: Merge vectors by taking max sequence per source
- **Subsumption**: One vector subsumes another if all its sequences are >= the other's
- **Comparison**: Vectors can be partially ordered (some pairs incomparable)

### Epoch Stream
A typed data structure representing an epoch with its associated items.

**Interface**: `IEpochStream<T>`
- `Epoch` property: The `EpochVector` identifier
- `Items` property: `IAsyncEnumerable<T>` of data items

**Purpose**: Enables blocks to process data while maintaining epoch association.

### Global Alignment  
The point when **all blocks** in the pipeline have completed processing a specific epoch vector.

**Key Properties**:
- Indicates safe transaction boundary (all work done)
- Enables consistent checkpoints
- Calculated as the "watermark" - the minimum completed epoch across all blocks

**Use Case**: Commit transactions only at global alignment to ensure atomicity.

### Watermark
The lowest epoch vector that has been completed by **all blocks** in the pipeline.

**Calculation**: Element-wise minimum of all blocks' completion vectors.

**Example**:
- Block A completed: `{source1=5, source2=3}`
- Block B completed: `{source1=4, source2=4}`
- **Watermark**: `{source1=4, source2=3}` (min of each source)

**Significance**: All epochs ≤ watermark are globally complete and safe to checkpoint/commit.

## Lifecycle Events

### OnEpochCreatedAsync
Triggered when a block begins processing a new epoch.

**Parameters**:
- `EpochVector epoch` - The epoch identifier
- `IBlockContext block` - Which block is processing this epoch
- `CancellationToken ct` - Cancellation token

**Use Case**: Create per-epoch resources (DbContext, transaction, cache).

**Scope**: Per-block event (each block gets notified independently).

### OnEpochCompletedAsync
Triggered when **a single block** completes processing an epoch.

**⚠️ Important**: This is a per-block event, **NOT a safe transaction boundary**.

**Parameters**:
- `EpochVector epoch` - The completed epoch
- `IBlockContext block` - Which block completed
- `CancellationToken ct` - Cancellation token

**Use Case**: 
- Track progression for metrics
- Signal readiness (but don't commit yet!)
- Useful for monitoring and diagnostics

**Anti-pattern**: ❌ Do NOT commit transactions here - other blocks may still be processing.

### OnGlobalEpochAlignedAsync
Triggered when **ALL blocks** have completed an epoch (watermark advances).

**✅ Safe Transaction Boundary**: This is the only safe point to commit transactions.

**Parameters**:
- `EpochVector watermark` - The globally completed epoch
- `CancellationToken ct` - Cancellation token

**Use Case**:
- Commit transactions (e.g., `DbContext.SaveChangesAsync()`)
- Create checkpoints for recovery
- Dispose per-epoch resources
- Publish events/notifications

**Guarantee**: All blocks have processed all items for this epoch.

## Transaction Concepts

### Transaction Boundary
A point in the pipeline where it is safe to commit database transactions or persist state.

**In Epoch Model**:
- ✅ **Global Alignment** = Safe transaction boundary
- ❌ **Per-Block Completion** = NOT safe (other blocks still processing)

**Rationale**: Committing before global alignment could result in partial state if downstream blocks fail.

### Per-Epoch DbContext
A database context instance scoped to a single epoch's lifetime.

**Lifecycle**:
1. Created on `OnEpochCreatedAsync`
2. Tracks entities as epoch items flow through
3. Kept alive through per-block completions
4. Committed and disposed on `OnGlobalEpochAlignedAsync`

**Benefits**:
- Natural transaction boundaries
- Bounded entity count (per epoch size)
- Automatic cleanup
- Isolation between epochs

### Context Promotion (Merge Handling)
The process of reusing a parent DbContext when epoch vectors merge.

**Scenario**:
- `Epoch {source1=1}` → creates `DbContext1`
- `Epoch {source2=1}` → creates `DbContext2`
- `Epoch {source1=1, source2=1}` → **promotes** `DbContext1` (reuses it)

**Purpose**: Prevents context fragmentation, keeps all merged entities in one transaction.

**Implementation**: `FindMostSpecificAncestor()` detects parent epochs.

## Advanced Concepts

### Epoch Ancestry
The relationship where one epoch vector subsumes (contains) another.

**Definition**: Epoch A subsumes epoch B if:
- For every source in B, A has the same source
- For every source in B, A's sequence >= B's sequence

**Example**:
- `{s1=2, s2=3}` subsumes `{s1=1, s2=2}` ✓
- `{s1=2, s2=3}` subsumes `{s1=2}` ✓
- `{s1=2}` does NOT subsume `{s1=2, s2=1}` ✗ (missing source)

**Use Case**: Detect when merged epochs should reuse existing DbContext instances.

### Element-Wise Max
Operation used to merge epoch vectors at fan-in points.

**Algorithm**:
```
max({s1=2, s2=3}, {s1=1, s2=4}) = {s1=2, s2=4}
```

**Property**: Monotonic - result is always >= both inputs.

**Use Case**: Combining epochs from multiple upstream sources.

### Checkpoint
A persistent snapshot of pipeline state enabling recovery from failures.

**Components**:
- Watermark (last globally completed epoch)
- Per-source anchors (where each source should resume)
- Block state (if any)

**Guarantee**: Can resume from checkpoint without data loss or duplication.

## Block Concepts

### IBlockContext
An interface identifying a block within lifecycle events.

**Properties**:
- `string BlockId` - Unique block identifier
- `string BlockName` - Human-readable name
- `Dictionary<string, object> Metadata` - Extensibility

**Purpose**: Enables fine-grained tracking of which block triggered which lifecycle event.

### Lifecycle Participant
A component (block or external system) that subscribes to epoch lifecycle events.

**Interface**: `IEpochLifecycleParticipant`

**Examples**:
- `EntityTrackingBlock` - Manages per-epoch DbContext
- Checkpoint manager - Records watermarks
- Metrics collector - Tracks progression
- External notification system - Publishes events

### Tracking Block
A downstream block that manages per-epoch state (typically DbContext) and participates in lifecycle events.

**Responsibilities**:
- Create resources on epoch start
- Track/modify entities as items flow
- Commit on global alignment
- Dispose resources after commit

**Pattern**: Composable - can have multiple tracking blocks in same pipeline (multi-sink).

### PlainSourceAdapter
A source block adapter that wraps plain source actors in single-epoch streams.

**Type**: `PlainSourceAdapter<T, TActor>` where `TActor : IPlainSourceActor<T>`

**Purpose**: 
- Wraps plain `IAsyncEnumerable<T>` sources in epoch streams
- Provides automatic single-epoch wrapping for legacy sources
- Outputs `IAsyncEnumerable<IEpochStream<T>>`

**Pattern**: PlainSourceAdapter → epoch-aware blocks (no segmenter needed)

**Example**:
```csharp
var sourceBlock = new PlainSourceAdapter<int, MyProducer>(
    new BlockContext("plain-source"),
    serviceScopeFactory,
    "plain-source");
```

**Note**: Replaces the deprecated `PlainSourceBlock`. For new code, prefer `EpochSourceBlock` with epoch-aware actors.

### EpochSegmenterBlock
A block that segments plain item streams into epoch streams.

**Type**: `EpochSegmenterBlock<T>`

**Purpose**:
- Converts `IAsyncEnumerable<T>` to `IAsyncEnumerable<IEpochStream<T>>`
- Applies segmentation policy (by count, by time, by predicate)
- Creates epoch boundaries for downstream processing

**Configuration**: `EpochSegmentationPolicy`
- `ByCount(n, sourceId)` - Every N items forms an epoch
- Custom policies via predicate

**Example**:
```csharp
var segmenter = new EpochSegmenterBlock<int>(
    "segmenter",
    EpochSegmentationPolicy.ByCount(1000, "my-source"));
```

### EpochActorBlock
**⭐ RECOMMENDED** primary block for epoch-aware stream processing.

**Type**: `EpochActorBlock<TIn, TOut, TActor>` where `TActor : IStreamActor<TIn, TOut>`

**Purpose**:
- Process items within epoch boundaries using actor pattern
- Provides DI scope isolation (each actor in own async scope)
- Supports scope rotation for stateful dependencies
- Consolidates transformation and processing capabilities

**Key Features**:
- **DI Safety**: Prevents concurrent dependency sharing bugs
- **Scope Rotation**: Optional via `context.RequestRotation()`
- **Flexible**: Supports 1-to-1, 1-to-many, filtering, and processing
- **Epoch Preservation**: Never mixes items across epoch boundaries

**Example**:
```csharp
// Define actor
public class MyActor : IStreamActor<int, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return $"Item-{item}";
        }
    }
}

// Use in pipeline
var actorBlock = new EpochActorBlock<int, string, MyActor>(
    "actor",
    serviceScopeFactory);
```

### EpochBatchBlock
A block that batches items within epoch boundaries.

**Type**: `EpochBatchBlock<T>`

**Purpose**:
- Accumulates items into batches of configurable size
- **Critical**: Batches NEVER span across epoch boundaries
- Supports time-based batching (window period)

**Configuration**:
- `maxBatchSize` - Maximum items per batch
- `windowPeriod` (optional) - Emit batch after duration

**Behavior**:
- Emits batch when full OR window expires
- **Always** breaks at epoch boundaries (may emit underfilled batches)
- Maintains streaming semantics (no buffering beyond batch size)

**Example**:
```csharp
var batchBlock = new EpochBatchBlock<int>(
    "batcher",
    maxBatchSize: 100,
    windowPeriod: TimeSpan.FromSeconds(5));
```

## Composability Patterns

### Epoch-Based Pipeline Pattern
Modern processing with epoch awareness - standard for all pipelines.

**Structure**: `PlainSourceAdapter → EpochActorBlock → EpochBatchBlock → EpochProcessorBlock`

**Use When**: Standard pattern for all new code (epochs provide transactional boundaries and checkpointing)

### Full Epoch Pipeline Pattern
Processing with epochs throughout the pipeline.

**Structure**: `PlainSourceBlock → EpochSegmenterBlock → EpochActorBlock → EpochBatchBlock → EpochActorBlock`

**Use When**: Transactional processing, checkpointing, or bulk operations with boundaries

### Mixed Pipeline Pattern
Combine plain and epoch-aware processing.

**Structure**: `PlainSourceBlock → TransformerBlock → EpochSegmenterBlock → EpochActorBlock`

**Use When**: Some stateless processing, some requiring transaction boundaries

### Unified-Then-Segment Pattern
Merge multiple plain sources, then segment (recommended for multi-source).

**Structure**:
```
Source1 → ┐
         ├→ UnionBlock → EpochSegmenterBlock("unified") → EpochActorBlock
Source2 → ┘
```

**Advantages**:
- Simple single-source epochs
- No ancestry tracking needed
- Easier to implement

### Segment-Then-Merge Pattern
Segment each source independently, then merge (advanced multi-source).

**Structure**:
```
Source1 → EpochSegmenterBlock("s1") → ┐
                                      ├→ MergeBlock → EpochActorBlock
Source2 → EpochSegmenterBlock("s2") → ┘
```

**Advantages**:
- Per-source checkpointing
- Independent progress tracking

**Complexity**: Requires lifecycle-aware blocks for multi-source epochs

## Performance Concepts

### Fast Path (Single-Source)
An optimization that skips ancestor detection when an epoch has only one source.

**Condition**: `epochVector.Sequences.Count == 1`

**Benefit**: O(1) check instead of O(N×M) ancestor search.

**Rationale**: Single-source linear progressions never need context promotion.

### Subsumption Check
Algorithm to determine if one epoch vector contains another.

**Complexity**: O(M) where M = number of sources in candidate ancestor.

**Used In**: `FindMostSpecificAncestor()` to detect merge scenarios.

## Coordinator Concepts

### EpochLifecycleCoordinator
Central registry and broadcaster for lifecycle events.

**Responsibilities**:
- Register participants (blocks, external systems)
- Broadcast events to all participants
- Serialize or parallelize notifications (configuration)

**Pattern**: Pub/Sub - participants register, coordinator notifies.

## Event Channel Concepts (Phase 7 - In Exploration)

### EventChannelNode
A graph-native node that propagates events (particularly epoch lifecycle events) through channels rather than centralized coordinators.

**Purpose**: Model event distribution as first-class graph connections using async channels.

**Benefits**:
- No centralized locking required
- Events visible in graph topology
- Natural backpressure handling
- Composable with standard blocks (transform, route, etc.)

**Example**: 
```csharp
var epochCreated = builder.EventChannel<EpochCreatedEvent>(capacity: 100, name: "epoch-created");
builder.ConnectEvents(epochCreated, trackingBlock);
```

**Similar To**: `BufferNode`, but for event propagation rather than data buffering.

### Event Routing Strategy
The pattern for delivering events to subscribers: sequential (one at a time) or broadcast (all simultaneously).

**Sequential Delivery**: Events sent to consumers one at a time, maintaining order. Used for transaction boundaries.

**Broadcast Delivery**: Events sent to all consumers in parallel. Used for metrics, logging.

### EpochCreatedEvent
Event emitted when a block begins processing a new epoch.

**Properties**: `EpochVector`, `IBlockContext`

**Use Case**: Initialize per-epoch resources (DbContext, cache).

### EpochCompletedEvent
Event emitted when a single block completes processing an epoch.

**Properties**: `EpochVector`, `IBlockContext`

**⚠️ Note**: Per-block event, NOT a safe transaction boundary.

### GlobalAlignmentEvent
Event emitted when ALL blocks have completed an epoch (watermark advances).

**Properties**: `EpochVector` (watermark)

**✅ Safe Transaction Boundary**: This is the only safe point to commit transactions.

### Per-Epoch Cache
A cache system that maintains separate cache instances per epoch.

**Use Case**: Share cached data across blocks within epoch boundary.

**Challenge**: Cache promotion strategy for merged epochs.

---

## Quick Reference

| Term | One-line Summary |
|------|------------------|
| **Epoch** | Logical batch of data with transaction boundary |
| **EpochVector** | Multi-source identifier with sequence numbers |
| **Global Alignment** | All blocks completed epoch - safe for commit |
| **Watermark** | Minimum epoch completed by all blocks |
| **OnEpochCompletedAsync** | Per-block completion - NOT safe boundary |
| **OnGlobalEpochAlignedAsync** | Global completion - SAFE boundary |
| **Context Promotion** | Reusing parent DbContext at merge points |
| **Subsumption** | One epoch contains another (ancestry) |
| **Transaction Boundary** | Safe point to commit (= global alignment) |
| **Tracking Block** | Block managing per-epoch state (e.g., DbContext) |
| **EventChannelNode** | Graph node for event propagation via channels |
| **Event Routing Strategy** | Sequential or broadcast event delivery |
| **EpochCreatedEvent** | Event when epoch starts (per-block) |
| **GlobalAlignmentEvent** | Event when all blocks complete (safe boundary) |

---

**See Also**:
- `PHASE5_EFCORE_ANCHORING_DEMO.md` - Epoch anchoring implementation
- `PHASE6_EPOCH_LIFECYCLE.md` - Lifecycle model and patterns
- `PHASE7_EVENT_CHANNEL_NODE_EXPLORATION.md` - EventChannelNode POC exploration
- `event-channel-node.md` - EventChannelNode design (in exploration)
- `POC_DOCUMENTATION_STRUCTURE.md` - Documentation organization
