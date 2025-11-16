# Merge Handling

## Overview

**Merge handling** (also called fan-in) occurs when multiple data streams converge into a single downstream block. In epoch-based pipelines, merges require special consideration for epoch vector coordination and context management.

## The Merge Problem

When two or more sources feed into a single block:

```
Source A (emits epochs: 1, 2, 3...) ─┐
                                      ├─→ Merge Block → Downstream
Source B (emits epochs: 1, 2, 3...) ─┘
```

**Challenges:**
- Which epoch does the merged stream represent?
- How to track entities from both sources in a single transaction?
- How to avoid creating duplicate contexts?

## Epoch Vector Merging

Epoch vectors merge using **element-wise maximum**:

```csharp
Source A emits: EpochVector[sourceA=5]
Source B emits: EpochVector[sourceB=3]

At merge point: EpochVector[sourceA=5, sourceB=3]
```

This represents: "The merged stream has seen up to epoch 5 from sourceA and up to epoch 3 from sourceB."

### Merge Progression

```
Time →
      Source A          Source B          Merged Stream
t1:   [A=1]            [B=1]             [A=1, B=1]
t2:   [A=2]            [B=1] (slower)    [A=2, B=1]
t3:   [A=2] (waiting)  [B=2]             [A=2, B=2]
t4:   [A=3]            [B=2] (slower)    [A=3, B=2]
```

At each point, the merged vector contains the maximum sequence from each source.

## Context Management

### The Fragmentation Problem

Without careful handling, merges can fragment tracked entities across multiple contexts:

```
❌ Naive approach:
1. Receive EpochVector[sourceA=1] → Create ContextA
2. Receive EpochVector[sourceB=1] → Create ContextB  
3. Receive EpochVector[sourceA=1, sourceB=1] → Create ContextAB

Result: 3 separate contexts, entities fragmented
```

### Solution: Context Promotion

Detect when a merged epoch **subsumes** an existing ancestor epoch and promote the ancestor's context:

```csharp
✅ Context promotion:
1. Receive EpochVector[sourceA=1] → Create ContextA
2. Receive EpochVector[sourceB=1] → Create ContextB
3. Receive EpochVector[sourceA=1, sourceB=1]:
   - Detect: This subsumes both [sourceA=1] and [sourceB=1]
   - Choose most specific ancestor: [sourceA=1] (or [sourceB=1])
   - Promote: Reuse ContextA, rename to merged epoch
   
Result: Single context with all entities, atomic commit ✅
```

## Implementation

### Finding the Most Specific Ancestor

```csharp
public EpochVector? FindMostSpecificAncestor(IEnumerable<EpochVector> candidates)
{
    // Find candidates that this epoch subsumes
    var ancestors = candidates.Where(c => this.Subsumes(c)).ToList();
    
    if (ancestors.Count == 0)
        return null;
    
    // Return the most specific (largest) ancestor
    return ancestors.OrderByDescending(a => a.Sequences.Count)
                    .ThenByDescending(a => a.Sequences.Values.Sum())
                    .First();
}
```

### Context Promotion in Tracking Block

```csharp
public async IAsyncEnumerable<T> ProcessAsync(
    IAsyncEnumerable<IEpochStream<T>> input,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await foreach (var epochStream in input.WithCancellation(cancellationToken))
    {
        TContext ctx;
        
        if (!_epochContexts.TryGetValue(epochStream.Epoch, out ctx))
        {
            // Fast path: Single-source linear progression
            if (epochStream.Epoch.Sequences.Count == 1)
            {
                ctx = _contextFactory.CreateDbContext();
                _epochContexts[epochStream.Epoch] = ctx;
            }
            else
            {
                // Merge scenario: check for reusable ancestor
                var ancestor = epochStream.Epoch.FindMostSpecificAncestor(_epochContexts.Keys);
                
                if (ancestor != null && _epochContexts.TryRemove(ancestor, out var ancestorCtx))
                {
                    // Promote the ancestor's context to the merged epoch
                    ctx = ancestorCtx;
                    _epochContexts[epochStream.Epoch] = ctx;
                    
                    _logger.LogDebug(
                        "Promoted context from {Ancestor} to {Merged}",
                        ancestor, epochStream.Epoch);
                }
                else
                {
                    // No ancestor found, create new context
                    ctx = _contextFactory.CreateDbContext();
                    _epochContexts[epochStream.Epoch] = ctx;
                }
            }
        }
        
        // Track entities in the context
        await foreach (var item in epochStream.Items.WithCancellation(cancellationToken))
        {
            ctx.Attach(item);
            yield return item;
        }
    }
}
```

## Multi-Stage Merges

Merges can occur at multiple stages in a pipeline:

```
Source A ─┐
          ├─→ Merge1 ─┐
Source B ─┘           │
                      ├─→ Merge2 → Sink
Source C ────────────→┘
```

**Progression:**

```
Merge1 produces: EpochVector[sourceA=10, sourceB=5]
Source C at:     EpochVector[sourceC=7]

Merge2 produces: EpochVector[sourceA=10, sourceB=5, sourceC=7]
```

Context promotion works transitively:
1. Merge1 promotes either sourceA or sourceB's context
2. Merge2 promotes Merge1's context (or sourceC's context)
3. Result: Single context with entities from all three sources

## Diamond Topology

A special case where a single source splits and later merges:

```
        ┌→ Transform A ─┐
Source ─┤               ├─→ Merge → Sink
        └→ Transform B ─┘
```

**Epoch vectors:**
```
Source:       EpochVector[source=5]
Transform A:  EpochVector[source=5]  (inherited)
Transform B:  EpochVector[source=5]  (inherited)
Merge:        EpochVector[source=5]  (unchanged, both branches have same vector)
```

In this case, no context promotion is needed - all epochs are identical.

## When Contexts Are Created vs. Promoted

### Create New Context
- First time seeing an epoch
- Single-source epoch (fast path)
- Merged epoch with no ancestor contexts available

### Promote Existing Context
- Merged epoch that subsumes an existing epoch
- Ancestor context is available
- Optimization to avoid fragmentation

### Example Timeline

```
Time  Event                           Action
----  ------------------------------  ------------------------------------
t1    Receive [A=1]                   Create ContextA
t2    Receive [B=1]                   Create ContextB
t3    Receive [A=1, B=1]              Promote ContextA → [A=1, B=1]
t4    Receive [A=2]                   Create ContextA2
t5    Receive [B=2]                   Create ContextB2
t6    Receive [A=2, B=1]              Promote ContextA2 → [A=2, B=1]
t7    Receive [A=2, B=2]              Promote ContextA2 → [A=2, B=2]
                                      (Note: [A=2, B=1] was already promoted)
```

## Entity Accumulation in Promoted Contexts

In continuous merge scenarios, a promoted context can accumulate many entities:

```
Epoch [A=1] → Create ContextA (100 entities)
Epoch [A=1, B=1] → Promote ContextA (now 200 entities)
Epoch [A=1, B=1, C=1] → Promote ContextA (now 300 entities)
```

**Why this is bounded:**
1. Global alignment commits and disposes contexts regularly
2. Contexts only live until watermark alignment
3. New epochs after commit get fresh contexts

**If alignment is delayed:**
- Context could accumulate many entities (10,000+)
- May hit EF Core change tracker limits
- Monitor context size and apply mitigation if needed

**Mitigation:**
```csharp
// Option 1: Intermediate commit for large contexts
if (ctx.ChangeTracker.Entries().Count() > 5000)
{
    await ctx.SaveChangesAsync(ct);
    ctx = _contextFactory.CreateDbContext();
}

// Option 2: Monitor and alert
if (ctx.ChangeTracker.Entries().Count() > 10000)
{
    _logger.LogWarning("Context has {Count} entities", 
        ctx.ChangeTracker.Entries().Count());
}
```

## Global Alignment in Merge Scenarios

Global alignment ensures all blocks (including merged blocks) complete before commit:

```
Block A completes:  EpochVector[sourceA=10]
Block B completes:  EpochVector[sourceB=8]
Merged block:       EpochVector[sourceA=9, sourceB=8]

Watermark = min across all blocks:
  EpochVector[sourceA=9, sourceB=8]
```

Only epochs ≤ watermark are committed, ensuring consistency.

## Testing Merge Handling

### Unit Test

```csharp
[Test]
public async Task TrackingBlock_PromotesContextOnMerge()
{
    var trackingBlock = CreateTrackingBlock();
    
    // Setup
    var epoch1 = EpochVector.FromSingleSource("A", 1);
    var epoch2 = EpochVector.FromSingleSource("B", 1);
    var merged = epoch1.Merge(epoch2);
    
    // Act: Process epochs
    await ProcessEpoch(trackingBlock, epoch1, new[] { new Entity { Id = 1 } });
    await ProcessEpoch(trackingBlock, epoch2, new[] { new Entity { Id = 2 } });
    await ProcessEpoch(trackingBlock, merged, new[] { new Entity { Id = 3 } });
    
    // Assert: Should have 2 contexts (one promoted)
    // epoch1's context was promoted to merged
    Assert.That(trackingBlock.ContextCount, Is.EqualTo(2));
}
```

### Integration Test

```csharp
[Test]
public async Task Pipeline_HandlesMultiSourceMerge()
{
    var pipeline = BuildMultiSourcePipeline();
    
    await pipeline.ExecuteAsync(CancellationToken.None);
    
    // Verify all entities committed in single transaction
    using var verifyContext = new TestDbContext();
    var committed = await verifyContext.Entities.ToListAsync();
    
    Assert.That(committed.Count, Is.EqualTo(expectedTotal));
    Assert.That(committed.Select(e => e.SourceId).Distinct().Count(), Is.EqualTo(2));
}
```

## Best Practices

### ✅ Do

- Use context promotion to avoid fragmentation
- Monitor context size in continuous merge scenarios
- Test with multiple sources and varying speeds
- Log context promotions for debugging

### ❌ Don't

- Create new context for every merged epoch (inefficient)
- Ignore ancestor detection (causes fragmentation)
- Assume single context per source (merges change this)
- Commit before global alignment (unsafe)

## Common Patterns

### Pattern: Fan-In Join

```csharp
// Join data from two sources
var pipeline = new DataFlowGraphBuilder()
    .AddSource<Order, OrderSource>("orders")
    .AddSource<Customer, CustomerSource>("customers")
    
    // Both feed into join block
    .AddBlock<IEpochStream<Order>, IEpochStream<Customer>, JoinedData>("join", 
        sp => new JoinBlock(...))
    
    // Track joined data
    .AddBlock("tracker", sp => new EntityTrackingBlock<JoinedData, AppDbContext>(...))
    
    .Build();
```

### Pattern: Partitioned Source

```csharp
// Process partitions independently, merge results
var pipeline = new DataFlowGraphBuilder()
    .AddSource<Data, Partition1Source>("partition1")
    .AddSource<Data, Partition2Source>("partition2")
    .AddSource<Data, Partition3Source>("partition3")
    
    // All feed into single processor
    .AddProcessor<Data>("processor", sp => new DataProcessor(...))
    
    .Build();
```

## Related Concepts

- [Epoch Vectors](../concepts/epoch-vectors.md) - Multi-source coordination
- [Transaction Boundaries](../concepts/transaction-boundaries.md) - Safe commit points
- [Global Alignment](../concepts/global-alignment.md) - Computing watermarks

## Related Guides

- [Using Epochs](../guides/using-epochs.md) - Formalized epoch system overview
- [EF Core with Epochs](../guides/ef-core-epochs.md) - Entity Framework Core transaction patterns

## References

- Phase 3: EpochVector merge operations
- Phase 6: Context promotion and ancestor detection
