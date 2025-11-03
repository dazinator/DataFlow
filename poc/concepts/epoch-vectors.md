# Epoch Vectors

## What are Epoch Vectors?

An **EpochVector** is a multi-source epoch identifier that tracks progress across multiple independent data sources simultaneously. It enables correct coordination in pipelines where data converges from multiple origins.

## Problem: Single-Source Limitation

A simple epoch counter works for linear pipelines:
```
Source → Transform → Sink
Epoch:  1, 2, 3, 4...
```

But fails when multiple sources merge:
```
Source A (epochs: 1, 2, 3...)  ─┐
                                 ├─→ Merge → Sink
Source B (epochs: 1, 2, 3...)  ─┘
```

**Questions:**
- Which epoch is being processed after the merge?
- When can we commit if Source A is at epoch 5 but Source B is at epoch 3?
- How do we track completion across both sources?

## Solution: Vector Clocks

`EpochVector` uses a vector clock approach, maintaining a sequence number for each source:

```csharp
public sealed record EpochVector
{
    public ImmutableDictionary<string, long> Sequences { get; }
}
```

**Examples:**
```
Single source:     EpochVector[source1=5]
Two sources:       EpochVector[source1=5, source2=3]  
Three sources:     EpochVector[sourceA=10, sourceB=7, sourceC=2]
```

## Vector Operations

### Creation

```csharp
// Single source
var epoch = EpochVector.FromSingleSource("source1", 5);
// → EpochVector[source1=5]

// Multiple sources
var epoch = EpochVector.FromSources(new Dictionary<string, long>
{
    ["source1"] = 10,
    ["source2"] = 5
});
// → EpochVector[source1=10, source2=5]
```

### Fan-In: Merge (Element-Wise Max)

When streams from multiple sources converge, their epoch vectors merge using **element-wise maximum**:

```csharp
var v1 = EpochVector[source1=10, source2=5];
var v2 = EpochVector[source1=8, source2=12];

var merged = v1.Merge(v2);
// → EpochVector[source1=10, source2=12]  (max of each source)
```

This represents: "The merged stream has seen up to epoch 10 from source1 and up to epoch 12 from source2."

### Comparison: IsLessThanOrEqual

Vectors support partial ordering:

```csharp
bool IsLessThanOrEqual(EpochVector other)
```

`v1 ≤ v2` if **every** sequence in `v1` is ≤ the corresponding sequence in `v2`:

```csharp
EpochVector[source1=5, source2=3] ≤ EpochVector[source1=6, source2=3]  // true
EpochVector[source1=5, source2=3] ≤ EpochVector[source1=5, source2=5]  // true
EpochVector[source1=5, source2=7] ≤ EpochVector[source1=6, source2=3]  // false (source2: 7 > 3)
```

### Subsumption: Ancestor Detection

A vector **subsumes** another if it's strictly greater or equal in all dimensions:

```csharp
bool Subsumes(EpochVector other)
```

Used for detecting when merged epochs can reuse contexts from parent epochs.

```csharp
EpochVector[source1=5, source2=3].Subsumes(EpochVector[source1=5])           // true
EpochVector[source1=5, source2=3].Subsumes(EpochVector[source2=3])           // true
EpochVector[source1=5, source2=3].Subsumes(EpochVector[source1=4, source2=2]) // true
```

## Pipeline Composition Rules

### Single Source (Linear Pipeline)
```
Source → Transform → Sink
```
Epochs propagate unchanged:
```
Source:     EpochVector[source1=5]
Transform:  EpochVector[source1=5]  (propagated)
Sink:       EpochVector[source1=5]  (propagated)
```

### Fan-Out (Broadcast)
```
             ┌→ Sink A
Source → Split
             └→ Sink B
```
Both branches inherit the same vector:
```
Source:  EpochVector[source1=5]
Sink A:  EpochVector[source1=5]  (inherited)
Sink B:  EpochVector[source1=5]  (inherited)
```

### Fan-In (Merge)
```
Source A ─┐
          ├→ Merge → Sink
Source B ─┘
```
Vectors merge using element-wise max:
```
Source A:  EpochVector[sourceA=10]
Source B:  EpochVector[sourceB=5]
After Merge:  EpochVector[sourceA=10, sourceB=5]
```

### Multi-Stage Merge
```
Source A ─┐               ┌→ Sink X
          ├→ Merge 1 ─┐   │
Source B ─┘           ├───┤
                      │   └→ Sink Y
Source C ────────────→ Merge 2 → Sink Z
```

At Merge 2:
```
From Merge 1:  EpochVector[sourceA=10, sourceB=5]
From Source C: EpochVector[sourceC=7]
Result:        EpochVector[sourceA=10, sourceB=5, sourceC=7]
```

## Global Alignment

The **global alignment watermark** is the minimum vector across all blocks:

```
Block 1: EpochVector[sourceA=10, sourceB=5]
Block 2: EpochVector[sourceA=8, sourceB=7]
Block 3: EpochVector[sourceA=9, sourceB=6]

Watermark = min(Block1, Block2, Block3)
          = EpochVector[sourceA=8, sourceB=5]
```

This represents: "All blocks have completed at least sourceA=8 and sourceB=5."

It's safe to commit transactions for any epoch ≤ watermark.

## Context Promotion in Merges

When a tracking block manages per-epoch database contexts and encounters a merged epoch, it can **promote** an ancestor's context instead of creating a new one:

```csharp
// Scenario:
// 1. Receive EpochVector[source1=1] → Create Context1
// 2. Receive EpochVector[source2=1] → Create Context2
// 3. Receive EpochVector[source1=1, source2=1] → Promote Context1 (or Context2)

var ancestor = mergedEpoch.FindMostSpecificAncestor(_existingContexts.Keys);
if (ancestor != null)
{
    // Reuse ancestor's context, promoting it to the merged epoch
    var ctx = _contexts[ancestor];
    _contexts.Remove(ancestor);
    _contexts[mergedEpoch] = ctx;
}
```

This ensures entities from both sources are tracked in a single `DbContext`, maintaining transaction atomicity.

## Use Cases

### Multi-Source ETL
Process data from multiple databases, coordinating commits across all sources:
```
DB1 (orders) ───┐
                ├─→ Join → Tracking Block → Commit when both aligned
DB2 (customers) ┘
```

### Partitioned Sources
Process partitioned data while maintaining global consistency:
```
Partition 1 ─┐
Partition 2 ─┼─→ Merge → Global Checkpoint
Partition 3 ─┘
```

### Event Sourcing
Merge events from multiple event streams while preserving causal ordering:
```
Event Stream A ─┐
Event Stream B ─┼─→ Event Processor → State Store
Event Stream C ─┘
```

## Related Concepts

- [Epochs](./epochs.md) - Core epoch concept
- [Global Alignment](./global-alignment.md) - Computing safe checkpoint boundaries
- [Merge Handling](./merge-handling.md) - Fan-in strategies and context promotion
- [Transaction Boundaries](./transaction-boundaries.md) - When to commit safely

## References

- Phase 3: EpochVector design and vector operations
- Phase 6: Context promotion and merge handling
