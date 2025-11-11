# ADR-003: ActorBlock Consolidation Strategy

**Status**: Accepted  
**Date**: 2025-11-05  
**Context**: Flow Composability Unification Research

## Context

Following the decision to create epoch-aware blocks for composability (ADR-001), we faced a choice: create separate specialized blocks (EpochTransformerBlock, EpochProcessorBlock) or consolidate around a unified ActorBlock pattern.

During prototyping, we recognized that both TransformerBlock and ProcessorBlock lack critical DI safety features that become essential when processing streams concurrently:
- No DI scope isolation (risk of sharing dependencies across concurrent operations)
- No scope rotation capability (accumulated state in scoped dependencies like caches, DbContext)

ActorBlock already provides these safety features through its actor pattern design.

## Decision

**We will consolidate around the ActorBlock pattern for both plain and epoch-aware stream processing.**

### Implications

1. **Epoch-Aware Blocks**: Implement `EpochActorBlock<TIn, TOut, TActor>` as the primary block, NOT separate EpochTransformerBlock/EpochProcessorBlock
2. **Plain Blocks**: Phase out TransformerBlock/ProcessorBlock in favor of ActorBlock (Phase 2, controlled migration)
3. **Unified Pattern**: Single "business logic participant" mental model across all stream processing

## Rationale

### DI Safety is a Default Concern

**Problem**: Concurrent stream processing without scope isolation leads to subtle bugs:
```csharp
// UNSAFE: Multiple concurrent operations sharing same DbContext instance
var transformer = new TransformerBlock<Order, OrderDto>(async order => 
{
    // If max concurrency > 1, multiple operations share the same DbContext!
    var customer = await dbContext.Customers.FindAsync(order.CustomerId);
    return new OrderDto { Customer = customer };
});
```

**Solution**: ActorBlock ensures each concurrent operation gets its own DI scope:
```csharp
// SAFE: Each actor instance has its own DI scope
public class OrderTransformActor : IActor<Order, OrderDto>
{
    private readonly AppDbContext _dbContext; // Scoped to this actor instance
    
    public OrderTransformActor(AppDbContext dbContext) => _dbContext = dbContext;
    
    public async IAsyncEnumerable<OrderDto> ProcessAsync(Order order, ...)
    {
        var customer = await _dbContext.Customers.FindAsync(order.CustomerId);
        yield return new OrderDto { Customer = customer };
    }
}
```

### Optional Scope Rotation

Actors can trigger scope rotation when dependencies accumulate state:
```csharp
public class CachingActor : IActor<Input, Output>, IScopedActorRotation
{
    private readonly ICacheService _cache; // Accumulates dictionary entries
    
    public async IAsyncEnumerable<Output> ProcessAsync(Input input, ...)
    {
        // ... use cache
        yield return output;
    }
    
    public bool ShouldRotateScope() => _cache.Size > 1000; // Rotate when cache grows
}
```

This is **opt-in** - simple actors pay zero overhead for rotation capability.

### Unified Mental Model

**Before** (fragmented):
- Need transformation? Use TransformerBlock
- Need side effects? Use ProcessorBlock
- Need DI safety? Use ActorBlock
- Need epochs + transformation? Use EpochTransformerBlock
- Need epochs + side effects? Use EpochProcessorBlock

**After** (unified):
- Need stream processing? Use ActorBlock (plain) or EpochActorBlock (epoch-aware)
- DI safety is automatic
- Optional scope rotation when needed
- Single pattern to learn and maintain

### No Performance Penalty

With proper implementation and warmup:
- Target: <1% overhead compared to specialized blocks
- ActorBlock pattern is lightweight (scope creation amortizes over many items)
- Rotation is opt-in (zero cost if not used)

## Consequences

### Positive

✅ **Safety by default**: DI scope isolation prevents concurrent dependency sharing bugs  
✅ **Flexibility**: Optional scope rotation for stateful dependencies  
✅ **Simplicity**: Single unified pattern across plain and epoch streams  
✅ **Maintainability**: Less code to maintain (no separate Transformer/Processor blocks)  
✅ **Composability**: Same actor interface works in both plain and epoch contexts  

### Negative

⚠️ **Migration effort**: Existing TransformerBlock/ProcessorBlock usage must migrate  
⚠️ **Slight verbosity**: Actor class definition vs inline lambda (mitigated by helper factories)  
⚠️ **Performance validation required**: Must prove <1% overhead target is met  

### Neutral

- Breaking change acceptable (POC not yet released externally)
- Test consolidation required (20-40% reduction target eliminates redundancy)

## Implementation Plan

### Phase 1: Epoch-Aware Blocks
- Implement `EpochActorBlock<TIn, TOut, TActor>` matching ActorBlock pattern
- Add helper factories for common patterns (map, filter, side effects)
- Implement `EpochBatchBlock<T>` for batching
- **Skip** EpochTransformerBlock/EpochProcessorBlock entirely

### Phase 2: Plain Block Consolidation (Separate Issue)
- Capture baseline performance with warmup methodology
- Mark TransformerBlock/ProcessorBlock as `[Obsolete]`
- Validate ActorBlock meets <1% overhead target
- Migrate existing usage to ActorBlock
- Consolidate tests (20-40% reduction target)
- Remove obsolete blocks after validation

## Alternatives Considered

### Alternative 1: Additive Approach
Create EpochTransformerBlock/EpochProcessorBlock alongside plain equivalents.

**Rejected because**:
- Doesn't address DI safety concerns in existing blocks
- Increases block count and mental overhead (6+ block types instead of 2)
- Duplicates transformation/processing logic across block types
- Misses opportunity to establish safety-first pattern

### Alternative 2: Keep Specialized Blocks, Add DI Features
Add DI scope isolation to TransformerBlock/ProcessorBlock.

**Rejected because**:
- Breaking change anyway (constructor signatures change)
- Rotation capability requires more complex interface
- Ends up reimplementing ActorBlock pattern in each specialized block
- More code to maintain

### Alternative 3: ActorBlock Optional
Make ActorBlock an "advanced" option for users who need DI features.

**Rejected because**:
- Safety should be default, not opt-in
- Users won't know they need DI isolation until bugs appear
- Fragmentary approach (some blocks safe, others not)

## Validation

### Performance Target
**Requirement**: <1% overhead after warmup compared to specialized blocks

**Methodology**:
1. Warmup phase: 1000+ items (eliminate JIT/initialization)
2. Measurement: Steady-state over 10,000+ items
3. Comparison: ActorBlock vs TransformerBlock/ProcessorBlock equivalents
4. If >1%: Profile and optimize before proceeding

### Test Consolidation
**Requirement**: 20-40% reduction in test count while maintaining coverage

**Approach**:
- Identify overlapping test scenarios (same logic, different block types)
- Create test coverage matrix
- Consolidate redundant edge case tests
- Ensure full coverage preserved

## References

- ADR-001: Epoch-Aware Block Pattern (composability solution)
- ADR-002: Multi-Source Segmentation Strategy (unified-then-segment pattern)
- `/research/flow-composability-unification/notes/actor-block-consolidation-analysis.md` (detailed analysis)
- `/research/flow-composability-unification/handover/github-issue-consolidate-plain-blocks.md` (implementation plan)

## Decision Makers

- Research Lead: @copilot
- Reviewer: @dazinator
- Date: 2025-11-05

## Status History

- **2025-11-05**: Accepted - ActorBlock consolidation strategy finalized
