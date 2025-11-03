# Understanding Anchors vs Epoch Vectors vs Checkpoints

This document clarifies the key concepts in resumable data processing, based on feedback and architectural guidance.

## Core Concepts

### 1. Epoch Vector
**Scope:** Framework-level  
**Purpose:** Track alignment and completion across blocks  
**Example:** `{" orders-source": 42, "payments-source": 38}`

- Internal sequencing metadata for the dataflow runtime
- Used for coordination and determining when blocks are aligned
- Managed by the framework, not exposed to sources
- **NOT** used directly for resuming data sources

### 2. Anchor
**Scope:** Source-level  
**Purpose:** Domain-specific resume point for data query  
**Example:** `{"lastOrderId": 84210}` or `{"timestamp": "2025-11-03T10:00:00Z"}`

- Source-specific information for resuming after interruption
- Examples: database primary key, timestamp, offset, cursor
- **Domain-specific** - varies by source type
- Internal to the source; becomes part of checkpoint if exposed

### 3. Checkpoint
**Scope:** Graph-level  
**Purpose:** Aggregated progress snapshot for recovery  
**Example:**
```json
{
  "epochVector": {"orders-source": 120},
  "sources": {
    "orders-source": {"lastOrderId": 84210}
  },
  "blocks": {
    "ef-writer": {"lastCommittedEpoch": 120}
  },
  "timestamp": "2025-11-03T10:45:00Z"
}
```

- Created at global epoch alignment
- Aggregates metadata from all participating blocks
- Sources contribute their anchors
- Blocks can attach their own metadata
- Serialized and persisted for recovery

### 4. Checkpoint Persistence
**Scope:** External  
**Purpose:** Durable storage of checkpoint object  
**Examples:** JSON file, blob storage, database table

- Serialization format (JSON, protobuf, etc.)
- Storage mechanism (disk, cloud, database)
- On restart: deserialize and pass to graph bootstrapper

## Relationship Table

| Concept | Owned By | Purpose | Persisted in Checkpoint |
|---------|----------|---------|------------------------|
| EpochVector | Framework | Track block alignment | ✅ Yes |
| Anchor | Source Actor | Resume position in source system | ✅ Yes |
| Checkpoint | Coordinator | Bundle of both + metadata | ✅ Yes (the checkpoint itself) |

## Two Layers of Persistence

The system has two distinct persistence layers:

### Layer 1: Epoch Lifecycle (Every Epoch)
**Frequency:** Every epoch  
**Purpose:** Local transactional boundary

```csharp
// Commit EF Core changes
await dbContext.SaveChangesAsync();

// Clear change tracker
await dbContext.DisposeAsync();

// Optionally wrap in explicit transaction
```

This ensures **local consistency** - each epoch is an atomic unit of work.

### Layer 2: Checkpoint Policy (Every N Epochs)
**Frequency:** Every N epochs or T seconds  
**Purpose:** Global recovery boundary

```csharp
if (epochCount % checkpointFrequency == 0)
{
    var checkpoint = new Checkpoint
    {
        EpochVector = GetCurrentEpochVectors(),      // Framework tracking
        SourceAnchors = CollectSourceAnchors(),       // Domain resume points
        BlockMetadata = CollectBlockState(),          // Block-specific data
        Timestamp = DateTime.UtcNow
    };
    
    await checkpointStore.SaveAsync(checkpoint);
}
```

This creates a **global recovery point** - if the system restarts, it can resume from this checkpoint.

## Current Demo Implementation

### What This Demo Shows
✅ Epoch lifecycle with per-epoch transactions  
✅ Framework-level epoch vectors for alignment  
✅ Observer pattern for lifecycle events  
✅ Domain-based anchoring with `lastProcessedId`  
✅ Query resumption using `WHERE Id > lastProcessedId`

### Current Implementation
✅ **The demo now properly separates domain anchors from epoch vectors:**

**Implementation Details:**
- `DatabaseSourceActor` maintains `_lastProcessedId` (int) as a private field - the domain anchor
- Query uses domain anchor: `WHERE r.Id > _lastProcessedId` (not the Processed flag)
- Each epoch updates `_lastProcessedId` with the last record ID in that epoch
- Epoch sequence increments independently (1, 2, 3...) for framework alignment
- **Simplification for demo**: We derive initial `lastProcessedId` from `epochSequence * epochSize` when resuming

**Why this works:**
- **Domain Anchor** (`lastProcessedId`): Determines WHERE clause for resumption
- **Epoch Sequence**: Framework-level counter for alignment tracking
- Both are maintained but serve distinct purposes

**What's still simplified:**
- For persistence, we use `IAnchorStore` which currently saves EpochVector
- In production, checkpoints would persist both:
  ```json
  {
    "epochVector": {"db-source": 42},
    "sources": {"db-source": {"lastProcessedId": 84210}}
  }
  ```
- The source would contribute its `lastProcessedId` to checkpoints at global alignment

### Key Improvements from Feedback
1. ✅ Query based on domain anchor: `WHERE r.Id > lastProcessedId`
2. ✅ Epoch sequence separate from anchor
3. ✅ Domain anchor updated after each epoch
4. ✅ Clear conceptual separation documented

## Future Work

To evolve this demo into a full checkpoint system:

1. **Implement checkpoint system** - Coordinator that creates checkpoints at global alignment
2. **Aggregate metadata** - Collect anchors from all sources + state from all blocks
3. **Add checkpoint persistence** - Serialize and store checkpoint objects
4. **Support multiple sources** with independent anchors
5. **Enable checkpoint-based recovery** (deserialize and bootstrap from checkpoint)

## Current Demo Implementation Status

This demonstration now properly implements **source-internal anchor management** without external persistence:

### ✅ What's Implemented

1. **Domain Anchor Management**
   - `DatabaseSourceActor` maintains `_lastProcessedId` as private field
   - Query uses `WHERE r.Id > _lastProcessedId` for correct resumption pattern
   - Anchor updates after each epoch completion
   - `GetLastProcessedId()` exposes anchor for future checkpoint contribution

2. **Proper Separation**
   - Domain anchor (`lastProcessedId`): Source-specific resume point
   - Epoch sequence: Framework-level alignment counter
   - Both tracked independently with distinct purposes

3. **Simplified Architecture**
   - ❌ NO external anchor store (`IAnchorStore` removed per feedback)
   - ❌ NO standalone persistence mechanism
   - ✅ Source manages anchor internally
   - ✅ Ready for future checkpoint system integration

### ❌ What's NOT Implemented (Future Work)

1. **Checkpoint System**
   - Graph-level checkpoint creation at global alignment
   - Aggregation of anchors from all sources
   - Metadata collection from all blocks  
   - Checkpoint serialization and persistence

2. **Checkpoint Recovery**
   - Loading checkpoints on restart
   - Passing anchors to sources via constructor/initialization
   - Coordinated recovery across all blocks

### Future Integration Pattern

When the core library implements the checkpoint system:

**On Checkpoint Creation (at global epoch alignment):**
```csharp
// Checkpoint coordinator asks each source for its anchor
var anchor = source.GetLastProcessedId();

// Aggregates into checkpoint object
var checkpoint = new Checkpoint
{
    EpochVector = currentEpochVector,
    Sources = {
        { "database-source", new { lastProcessedId = anchor } }
    },
    Blocks = {
        // Other blocks contribute their state
    },
    Timestamp = DateTime.UtcNow
};

// Serializes and persists via checkpoint system
await checkpointStore.SaveAsync(checkpoint);
```

**On Restart/Recovery:**
```csharp
// Load checkpoint from storage
var checkpoint = await checkpointStore.LoadAsync();

// Extract anchor for this source
var anchor = checkpoint.Sources["database-source"].lastProcessedId;

// Initialize source with anchor from checkpoint
var source = new DatabaseSourceActor(
    dbOptions,
    epochSize,
    sourceId,
    initialLastProcessedId: anchor);  // ← From loaded checkpoint
```

**Benefits of This Approach:**
- Sources don't need standalone persistence mechanisms
- All sources/blocks share consistent recovery boundary at global alignment
- Checkpoint system provides centralized coordination
- Simple, clean abstraction - sources just expose their anchor
- No confusion about persistence timing or scope

## Key Takeaway

> **Anchor** = "where to resume" (source-specific, domain data, managed internally)  
> **EpochVector** = "what's aligned" (framework-level, sequencing)  
> **Checkpoint** = "what the whole graph knows" (global state bundle created at alignment)  
> **Checkpoint Persistence** = "how it survives restarts" (serialization + storage)

Don't conflate these concepts - they serve distinct purposes and exist at different layers of the system.

**Current Demo:** Implements internal anchor management. Sources manage anchors privately and expose them via `GetLastProcessedId()` for future checkpoint contribution. No standalone persistence - checkpoint system will handle that when implemented.
