# Future Enhancements - Issue Ideas

This document captures potential enhancements identified during Phase 6 development for future GitHub issues.

## Per-Epoch Cache Management System

### Overview
Create a cache management system that maintains separate cache instances per epoch, allowing blocks to share cached data while respecting epoch boundaries.

### Motivation
- Multiple blocks may need shared data within an epoch (lookups, reference data)
- Per-epoch isolation prevents cache pollution across transactions
- Enables better resource management (cache disposal at epoch boundaries)

### Design Considerations

**1. Cache Instance Management**
```csharp
public interface IEpochCacheManager
{
    ICache GetOrCreateCache<TKey, TValue>(EpochVector epoch, string cacheId);
    void DisposeCache(EpochVector epoch);
}
```

**Questions**:
- Should cache manager be a singleton or per-pipeline?
- How to handle cache size limits per epoch?
- What eviction strategy within epoch boundaries?

**2. Cache Promotion at Merge Points**
Similar to DbContext promotion, should caches be promoted when epochs merge?

```csharp
// Scenario:
// Epoch {source1=1} → cache1 with 100 entries
// Epoch {source2=1} → cache2 with 50 entries
// Epoch {source1=1, source2=1} → promote cache1? merge both?
```

**Options**:
- **A. Promote most specific cache** (like DbContext pattern)
- **B. Merge caches** (combine entries, handle conflicts)
- **C. Invalidate caches** (start fresh at merge points)

**Trade-offs**:
| Approach | Pro | Con |
|----------|-----|-----|
| Promote | Reuse cached data, consistent with DbContext | May have stale data from other source |
| Merge | Includes all cached data | Complex conflict resolution, potential memory bloat |
| Invalidate | Simplest, always fresh | Performance hit, re-fetching data |

**Recommendation**: Start with **Option A (Promote)** for consistency with DbContext pattern.

**3. Concurrency Considerations**
Should the cache itself be thread-safe?

**Analysis**:
- Each epoch processed sequentially within block → no concurrency within epoch
- Different epochs processed concurrently → each has own cache → no contention
- **Conclusion**: Cache does NOT need to be thread-safe (ConcurrentDictionary not required)

**However**: If cache is shared across blocks (not just within one block), then thread-safety needed.

**Recommendation**: 
- Per-block caches: Not thread-safe (simpler, faster)
- Shared caches: Use ConcurrentDictionary or locking

**4. Integration with EntityTrackingBlock**
How should tracking blocks interact with cache system?

**Scenario**: EntityTrackingBlock loads reference data from cache before attaching entities.

```csharp
public class EntityTrackingBlock<T, TContext> 
{
    private readonly IEpochCacheManager _cacheManager;
    
    public async IAsyncEnumerable<T> ProcessAsync(...)
    {
        await foreach (var epochStream in input)
        {
            var cache = _cacheManager.GetOrCreateCache<int, RefData>(epochStream.Epoch, "refdata");
            var ctx = GetOrCreateDbContext(epochStream.Epoch);
            
            await foreach (var item in epochStream.Items)
            {
                // Use cache for lookups
                var refData = await cache.GetOrAddAsync(item.RefId, id => LoadRefDataAsync(id, ctx));
                
                // Attach entity
                ctx.Attach(item);
                yield return item;
            }
        }
    }
}
```

**Questions**:
- Should DbContext and cache share same lifecycle?
- Dispose cache before or after DbContext commit?
- Should cache be transactional (rollback on failure)?

**5. Global Alignment and Cache Disposal**
When should caches be disposed?

**Options**:
- **Option A**: Dispose on `OnGlobalEpochAlignedAsync` (same as DbContext)
- **Option B**: Dispose immediately after block completion
- **Option C**: Keep caches across epochs (persistent cache with epoch tagging)

**Recommendation**: **Option A** for consistency with transaction model.

### Proposed GitHub Issue

**Title**: Implement Per-Epoch Cache Management System

**Description**:
Create a cache management system that maintains separate cache instances per epoch, enabling blocks to share cached data while respecting epoch boundaries.

**Requirements**:
- [ ] Define `IEpochCacheManager` interface
- [ ] Implement cache promotion at merge points (using ancestry detection)
- [ ] Integrate with `IEpochLifecycleParticipant` for disposal on global alignment
- [ ] Decide on per-block vs shared cache model
- [ ] Implement cache key generation strategy
- [ ] Add tests for cache promotion scenarios
- [ ] Benchmark cache overhead vs baseline

**Related Work**:
- EntityTrackingBlock pattern (Phase 6)
- Epoch ancestry detection (`Subsumes`, `FindMostSpecificAncestor`)

**Acceptance Criteria**:
- Cache instances created per epoch
- Caches promoted at merge points (no fragmentation)
- Caches disposed at global alignment
- No memory leaks with long-running pipelines
- Overhead < 5% vs no-cache baseline

---

## Event-Driven Lifecycle Coordination (EventChannelNode)

### Overview
Replace callback-based `EpochLifecycleCoordinator` with channel-based event broadcasting using existing dataflow semantics.

### Motivation (from PR review)
Current `EpochLifecycleCoordinator` uses:
- Locking for participant list
- Sequential foreach for notifications
- Custom event broadcasting logic

Could be simplified by treating lifecycle events as **data flowing through channels**.

### Design

**Core Concept**:
```
Source → DataBlock → EventChannelNode<LifecycleEvent> → ParticipantBlock
                  ↘                                     ↗
```

Lifecycle events become just another data type flowing through the graph.

**Event Types**:
```csharp
public abstract record LifecycleEvent(EpochVector Epoch);
public record EpochCreatedEvent(EpochVector Epoch, IBlockContext Block) : LifecycleEvent(Epoch);
public record EpochCompletedEvent(EpochVector Epoch, IBlockContext Block) : LifecycleEvent(Epoch);
public record GlobalAlignmentEvent(EpochVector Watermark) : LifecycleEvent(Watermark);
```

**EventChannelNode**:
```csharp
public class EventChannelNode<TEvent> : IDataFlowBlock
{
    private readonly Channel<TEvent> _channel;
    
    public void PublishEvent(TEvent evt)
    {
        _channel.Writer.TryWrite(evt); // Non-blocking
    }
    
    public ChannelReader<TEvent> Subscribe()
    {
        return _channel.Reader; // Multiple subscribers
    }
}
```

**Benefits**:
- No custom locking (Channel handles synchronization)
- Natural backpressure
- Extensible event types
- Observable and decoupled
- Consistent with framework semantics

**Challenges**:
- Event ordering guarantees
- Handling slow subscribers
- Error propagation
- Acknowledgment mechanism (if needed)

### Proposed GitHub Issue

**Title**: Implement EventChannelNode for Lifecycle Event Broadcasting

**Description**:
Replace callback-based lifecycle coordination with channel-based event broadcasting, treating lifecycle events as data flowing through the graph.

**Requirements**:
- [ ] Define lifecycle event types (EpochCreatedEvent, etc.)
- [ ] Implement EventChannelNode<TEvent>
- [ ] Support multiple subscribers (broadcast)
- [ ] Integrate with graph topology
- [ ] Add automatic registration (when lifecycle enabled)
- [ ] Benchmark: EventChannel vs callback-based

**Acceptance Criteria**:
- Events broadcast to all subscribers
- No locking complexity
- Overhead within 5% of callback-based approach
- Deterministic ordering per source
- Failed subscribers don't block others

---

## Built-In Tracking Blocks Package (DataFlow.EntityFramework)

### Overview
Create a NuGet package with production-ready tracking block implementations for common scenarios.

### Proposed Blocks

**1. EFCoreTrackingBlock<T, TContext>**
- Generic EntityTrackingBlock from Phase 6
- Production-hardened with error handling
- Configurable options (max entities, commit strategy)
- Metrics integration

**2. TransactionBlock<T, TContext>**
- Manages explicit database transactions per epoch
- Begin transaction on epoch creation
- Commit on global alignment
- Rollback on errors

**3. CacheTrackingBlock<T>**
- Per-epoch in-memory cache
- Configurable eviction policies
- Cache promotion at merges
- Integrates with EntityTrackingBlock

**4. AuditTrackingBlock<T>**
- Captures audit information per epoch
- Tracks who, what, when for each item
- Writes audit log on global alignment

### Package Structure
```
DataFlow.EntityFramework/
├── Blocks/
│   ├── EFCoreTrackingBlock.cs
│   ├── TransactionBlock.cs
│   ├── CacheTrackingBlock.cs
│   └── AuditTrackingBlock.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs  // .AddDataFlowTracking()
├── Options/
│   ├── TrackingBlockOptions.cs
│   └── TransactionOptions.cs
└── README.md
```

### Proposed GitHub Issue

**Title**: Create DataFlow.EntityFramework NuGet Package

**Description**:
Package production-ready tracking blocks for Entity Framework Core integration, including transaction management, caching, and audit logging.

**Requirements**:
- [ ] Extract EntityTrackingBlock from POC
- [ ] Add production error handling
- [ ] Add configuration options
- [ ] Create TransactionBlock implementation
- [ ] Add service registration extensions
- [ ] Write comprehensive tests
- [ ] Create usage documentation
- [ ] Publish to NuGet

---

## Adaptive Epoch Sizing Based on Memory Pressure

### Overview
Automatically adjust epoch sizes based on memory usage and performance characteristics.

### Motivation
Fixed epoch sizes may not suit all scenarios:
- Small epochs: High coordination overhead
- Large epochs: Memory pressure, long commit times

### Design Ideas

**1. Memory-Based Adjustment**
Monitor DbContext entity count or memory usage, trigger epoch completion when threshold reached.

```csharp
public class AdaptiveEpochSource<T> : SourceActorBase<T>
{
    private int _itemsInEpoch = 0;
    private readonly int _minEpochSize = 100;
    private readonly int _maxEpochSize = 10000;
    
    public override async IAsyncEnumerable<IEpochStream<T>> ProduceEpochsAsync(...)
    {
        while (await source.MoveNextAsync())
        {
            // Adaptive sizing logic
            var targetSize = CalculateTargetSize(memoryPressure, avgProcessingTime);
            
            if (_itemsInEpoch >= targetSize)
            {
                yield return CompleteEpoch();
                _itemsInEpoch = 0;
            }
            
            yield return source.Current;
            _itemsInEpoch++;
        }
    }
}
```

**2. Performance-Based Adjustment**
Measure epoch processing time, adjust size to maintain target latency.

**3. Backpressure-Based Adjustment**
If downstream blocks slow down, reduce epoch size to allow more frequent commits.

### Proposed GitHub Issue

**Title**: Implement Adaptive Epoch Sizing

**Description**:
Dynamically adjust epoch sizes based on memory pressure, processing latency, and backpressure signals.

**Requirements**:
- [ ] Monitor memory usage per epoch
- [ ] Track processing time metrics
- [ ] Implement sizing algorithm (PID controller?)
- [ ] Add configuration bounds (min/max epoch size)
- [ ] Test with varying workloads
- [ ] Benchmark adaptive vs fixed sizing

---

## Object Pooling for DbContext Reuse

### Overview
Pool and reuse DbContext instances across epochs instead of creating fresh instances each time.

### Motivation
- DbContext creation has overhead
- Connection pooling helps, but instance creation still costs
- Reusing contexts (after clear) could improve performance

### Design Considerations

**1. When to Reuse**
- After global alignment (context cleared of tracked entities)
- Only if context in good state (no errors)
- Limit reuse count (recreate after N uses)

**2. Context Clearing**
```csharp
public void ClearContext(DbContext ctx)
{
    ctx.ChangeTracker.Clear();  // Remove all tracked entities
    // Reset any other state
}
```

**3. Pool Implementation**
```csharp
public class DbContextPool<TContext> where TContext : DbContext
{
    private readonly ConcurrentBag<TContext> _pool = new();
    private readonly IDbContextFactory<TContext> _factory;
    
    public TContext Get()
    {
        return _pool.TryTake(out var ctx) ? ctx : _factory.CreateDbContext();
    }
    
    public void Return(TContext ctx)
    {
        ctx.ChangeTracker.Clear();
        _pool.Add(ctx);
    }
}
```

### Proposed GitHub Issue

**Title**: Implement DbContext Pooling for EntityTrackingBlock

**Description**:
Add object pooling to reuse DbContext instances across epochs, reducing allocation overhead.

**Requirements**:
- [ ] Implement DbContextPool<TContext>
- [ ] Integrate with EntityTrackingBlock
- [ ] Clear ChangeTracker after each use
- [ ] Add reuse limit configuration
- [ ] Benchmark: pooled vs non-pooled
- [ ] Test for state leakage between epochs

---

## How to Use This Document

1. **Review** an enhancement idea
2. **Discuss** in team/PR comments
3. **Create GitHub Issue** using template
4. **Reference** this document in the issue
5. **Update** this document as ideas evolve or are implemented

**Status Tracking**:
- 🔵 Proposed - Idea captured, needs discussion
- 🟡 Accepted - Team agreed, ready for issue creation
- 🟢 In Progress - GitHub issue created and assigned
- ✅ Completed - Merged to main

## Current Status

| Enhancement | Status | Issue Link |
|-------------|--------|------------|
| Per-Epoch Cache Management | 🔵 Proposed | - |
| EventChannelNode | 🔵 Proposed | - |
| DataFlow.EntityFramework Package | 🔵 Proposed | - |
| Adaptive Epoch Sizing | 🔵 Proposed | - |
| DbContext Pooling | 🔵 Proposed | - |
