# Global Alignment

## What is Global Alignment?

**Global alignment** is the process of determining the point at which **all blocks** in a pipeline have completed processing up to a specific epoch. This represents a safe transaction boundary where state can be committed without risk of inconsistency.

## The Synchronization Problem

In concurrent pipelines, different blocks process data at different speeds:

```
Source (fast) → Transform (medium) → Sink (slow)
Epoch 1: Complete    Complete         Processing...
Epoch 2: Complete    Processing...    Waiting...
Epoch 3: Processing  Waiting...       Waiting...
```

**Question:** When is it safe to commit Epoch 1's transaction?

**Answer:** Only when **ALL blocks** have finished processing Epoch 1.

## Completion vs. Alignment

### Per-Block Completion
When a single block finishes processing all items in an epoch:
- ⚠️ Other blocks may still be processing the same epoch
- ⚠️ **NOT safe** for committing transactions
- ✅ Useful for per-block metrics and tracking

### Global Alignment
When **ALL blocks** have completed an epoch:
- ✅ No in-flight data remains for that epoch
- ✅ **SAFE** for committing transactions
- ✅ Represents a consistent checkpoint boundary

## Computing the Watermark

The **global completion watermark** is the minimum epoch across all blocks:

```csharp
EpochVector GetGlobalCompletionWatermark()
{
    // Return the element-wise minimum of all blocks' completion epochs
    return _blockProgress.Values.Aggregate((min, next) => min.Min(next));
}
```

### Single-Source Example

```
Block 1: Completed up to Epoch 5
Block 2: Completed up to Epoch 3  ← bottleneck
Block 3: Completed up to Epoch 7

Watermark = min(5, 3, 7) = Epoch 3
```

**Safe to commit:** Epochs 1, 2, and 3  
**NOT safe to commit:** Epochs 4+ (Block 2 hasn't finished them)

### Multi-Source Example

```
Block 1: EpochVector[sourceA=10, sourceB=5]
Block 2: EpochVector[sourceA=8, sourceB=7]  ← bottleneck for sourceA
Block 3: EpochVector[sourceA=9, sourceB=6]

Watermark = EpochVector[
    sourceA=min(10, 8, 9) = 8,
    sourceB=min(5, 7, 6) = 5   ← bottleneck for sourceB
]
```

**Safe to commit:** Any epoch where `epoch ≤ EpochVector[sourceA=8, sourceB=5]`

## Alignment Checking

### IsGloballyAligned

Check if a specific epoch has been completed by all blocks:

```csharp
bool IsGloballyAligned(EpochVector epoch)
{
    var watermark = GetGlobalCompletionWatermark();
    return epoch.IsLessThanOrEqual(watermark);
}
```

**Example:**
```
Watermark: EpochVector[sourceA=8, sourceB=5]

IsGloballyAligned(EpochVector[sourceA=7, sourceB=4])  → true  ✅
IsGloballyAligned(EpochVector[sourceA=8, sourceB=5])  → true  ✅
IsGloballyAligned(EpochVector[sourceA=9, sourceB=5])  → false ⚠️ (sourceA not ready)
IsGloballyAligned(EpochVector[sourceA=8, sourceB=6])  → false ⚠️ (sourceB not ready)
```

## Alignment Lifecycle

### 1. Block Reports Completion

```csharp
public void NotifyEpochCompleted(string blockName, EpochVector epoch)
{
    _blockProgress[blockName] = epoch;
    
    // Check if this triggered global alignment
    CheckForNewAlignment();
}
```

### 2. Check for New Alignment

After any block reports completion, check if the watermark advanced:

```csharp
private void CheckForNewAlignment()
{
    var newWatermark = GetGlobalCompletionWatermark();
    
    if (newWatermark > _lastWatermark)
    {
        _lastWatermark = newWatermark;
        
        // Notify lifecycle participants
        BroadcastGlobalAlignment(newWatermark);
    }
}
```

### 3. Broadcast to Participants

```csharp
private async Task BroadcastGlobalAlignment(EpochVector watermark)
{
    foreach (var participant in _lifecycleParticipants)
    {
        await participant.OnGlobalEpochAlignedAsync(watermark, _cancellationToken);
    }
}
```

### 4. Participants React

Transaction blocks commit their work:

```csharp
public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
{
    // Find all contexts for epochs ≤ watermark
    var ready = _epochContexts
        .Where(kvp => kvp.Key.IsLessThanOrEqual(watermark))
        .Select(kvp => kvp.Key)
        .ToList();
    
    foreach (var epoch in ready)
    {
        if (_epochContexts.TryRemove(epoch, out var ctx))
        {
            await using (ctx)
            {
                await ctx.SaveChangesAsync(ct);  // Safe to commit!
                _logger.LogInformation("Committed epoch {Epoch}", epoch);
            }
        }
    }
}
```

## Why This Matters

### Transaction Safety

**Without global alignment:**
```
Block 1 commits Epoch 5
Block 2 crashes while processing Epoch 5
→ Partial commit = data inconsistency ❌
```

**With global alignment:**
```
Block 1 completes Epoch 5, waits for alignment
Block 2 crashes while processing Epoch 5
→ Watermark stays at Epoch 4
→ Only Epoch 4 and earlier are committed ✅
→ Epoch 5 will be reprocessed on recovery
```

### Checkpoint Consistency

Checkpoints saved at the watermark guarantee:
- All data up to that point has been fully processed
- No in-flight data needs to be reprocessed
- Recovery can resume from the checkpoint without gaps or duplicates

### Backpressure

Global alignment naturally creates backpressure:
- Fast blocks wait for slow blocks before committing
- Prevents memory exhaustion from unbounded in-flight epochs
- Provides natural flow control

## Design Principles

### Separation of Concerns

**Completion Tracking** (existing infrastructure):
- `GlobalEpochAlignment` class tracks per-block progress
- Computes watermarks efficiently
- Provides alignment queries

**Lifecycle Events** (Phase 6 addition):
- `EpochLifecycleCoordinator` listens for alignment events
- Broadcasts to interested participants
- Decouples tracking from reaction

### Composability

Any component can participate in lifecycle events:
- Transaction blocks commit on alignment
- Metrics collectors track epoch latency
- Checkpoint managers save state
- Cache managers clear per-epoch caches

## Performance Considerations

### Alignment Latency

Watermark advances only as fast as the **slowest block**:

```
Fast blocks: ████████████ (Epoch 20)
Slow block:  ██ (Epoch 2) ← bottleneck
Watermark:   ██ (can only commit up to Epoch 2)
```

**Mitigation:**
- Profile and optimize slow blocks
- Consider parallelism within blocks
- Use appropriate buffer sizes to smooth processing bursts

### Alignment Frequency

Checking alignment on every block completion can be expensive in high-throughput scenarios.

**Optimization:**
```csharp
// Only check alignment if completion could affect watermark
if (epoch.IsLessThanOrEqual(_lastWatermark.IncrementSource(blockName)))
{
    CheckForNewAlignment();
}
```

### Memory Pressure

Unaligned epochs accumulate in memory (contexts, tracked entities, buffers).

**Monitoring:**
- Track number of unaligned epochs per block
- Monitor memory usage of per-epoch contexts
- Alert if watermark is significantly behind newest epoch

## Related Concepts

- [Epochs](./epochs.md) - Core epoch concept
- [Epoch Vectors](./epoch-vectors.md) - Multi-source coordination
- [Transaction Boundaries](./transaction-boundaries.md) - When to commit safely
- [Lifecycle Events](./lifecycle-events.md) - Reacting to alignment

## References

- Phase 5: `CompletionBasedEpochProgress.cs` - Global alignment tracking
- Phase 6: Lifecycle event integration with alignment
