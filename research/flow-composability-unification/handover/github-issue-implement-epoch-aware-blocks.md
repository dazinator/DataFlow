# Implementation Issue: Epoch-Aware Blocks for Flow Composability

## Context and Objectives

### Problem Statement

The decoupled epoch segmentation (PR #146) separated epoch concerns from sources but created a composability gap:
- **Plain blocks** (TransformerBlock, ProcessorBlock, BatchBlock) operate on `IAsyncEnumerable<T>`
- **Epoch-aware blocks** (WriteContextBlock) operate on `IAsyncEnumerable<IEpochStream<T>>`
- Without a segmenter, downstream blocks expecting epochs cannot connect to plain sources
- This limits flexible pipeline composition

### Research Background

Research was conducted to validate approaches for enabling composability between plain and epoch streams.

**Research Documentation:**
- **Research Report**: `/research/flow-composability-unification/README.md`
- **Design Doc**: `/research/flow-composability-unification/design/epoch-aware-blocks.md`
- **ADR**: `/research/flow-composability-unification/adr/2025-11-05-epoch-aware-block-pattern.md`

**Key Research Findings:**
- ✅ Epoch-aware wrapper blocks successfully solve composability
- ✅ Prototype validates epoch boundary preservation
- ✅ Streaming semantics maintained (no unnecessary buffering)
- ✅ Enables flexible composition patterns
- ✅ Type-safe approach prevents runtime errors

### Objectives

Implement epoch-aware blocks that enable seamless composability:

- [ ] Items processed within epoch boundaries
- [ ] Epoch structure and metadata preserved
- [ ] Batching respects epoch boundaries (no cross-epoch batches)
- [ ] Streaming semantics maintained
- [ ] Type-safe APIs
- [ ] All composability patterns supported

## Implementation Guidance

### Recommended Approach: ActorBlock Pattern

Based on research findings, **consolidate around the ActorBlock pattern** for epoch-aware blocks to provide DI scope safety by default.

**Core Pattern:**
```csharp
public class EpochActorBlock<TIn, TOut, TActor> : 
    BlockBase<IEpochStream<TIn>, IEpochStream<TOut>>
    where TActor : IStreamActor<TIn, TOut>
{
    public override async IAsyncEnumerable<IEpochStream<TOut>> ExecuteAsync(
        IAsyncEnumerable<IEpochStream<TIn>> input,
        IExecutionContext context)
    {
        await foreach (var epochStream in input.WithCancellation(context.CancellationToken))
        {
            // Process items within this epoch using actor pattern
            yield return new EpochStream<TOut>(
                epochStream.Epoch,
                ProcessEpochItems(epochStream, context));
        }
    }
}
```

**Why ActorBlock?**
- DI scope isolation prevents concurrent dependency sharing bugs
- Optional scope rotation for stateful dependencies (caches, DbContext)
- Unified pattern with plain ActorBlock (consistency)
- Epoch boundaries can serve as natural rotation points

### Design References

- **Research Findings**: `/research/flow-composability-unification/README.md`
- **Detailed Design**: `/research/flow-composability-unification/design/epoch-aware-blocks.md`
- **Architecture Decision**: `/research/flow-composability-unification/adr/2025-11-05-epoch-aware-block-pattern.md`
- **ActorBlock Consolidation Analysis**: `/research/flow-composability-unification/notes/actor-block-consolidation-analysis.md`
- **Prototype Code**: `/research/flow-composability-unification/handover/prototype/`
  - **`EpochActorBlock.cs`** - **RECOMMENDED** implementation matching ActorBlock pattern
  - `EpochBatchBlock.cs` - Batch items respecting epoch stream boundaries
  - `EpochAwareBlockTests.cs` - Test scenarios and validation
  - ~~`EpochTransformerBlock.cs`~~ - REMOVED (exploratory/redundant)
  - ~~`EpochProcessorBlock.cs`~~ - REMOVED (exploratory/redundant)

**Prototype Status**: The EpochTransformerBlock and EpochProcessorBlock were exploratory prototypes and have been removed as redundant. **EpochActorBlock is the recommended implementation** that consolidates both transformation and processing capabilities with DI safety.

### Blocks to Implement

#### Priority 1: Core Blocks (Must Have)

**1. EpochActorBlock<TIn, TOut, TActor>** ⭐ **RECOMMENDED PRIMARY BLOCK**
```csharp
/// <summary>
/// Epoch-aware actor block with DI scope isolation and optional rotation.
/// Consolidates transformation and processing within epoch boundaries.
/// Mirrors ActorBlock pattern for consistency.
/// </summary>
public class EpochActorBlock<TIn, TOut, TActor> : 
    BlockBase<IEpochStream<TIn>, IEpochStream<TOut>>
    where TActor : IStreamActor<TIn, TOut>
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    public EpochActorBlock(
        string name,
        IServiceScopeFactory scopeFactory);
}
```

**Reference**: See `/research/flow-composability-unification/handover/prototype/EpochActorBlock.cs` for complete implementation.

**Key Design Points**:
- Mirrors `ActorBlock<TIn, TOut, TActor>` pattern exactly
- Processes items within each epoch stream using actor pattern
- Each actor runs in its own DI scope
- Optional scope rotation via `context.RequestRotation()`
- Preserves epoch boundaries (no cross-epoch processing)

**2. EpochBatchBlock<T>**
```csharp
/// <summary>
/// Batches items within epoch boundaries.
/// Batches NEVER span across epochs.
/// </summary>
public class EpochBatchBlock<T> : 
    BlockBase<IEpochStream<T>, IEpochStream<T[]>>
{
    public EpochBatchBlock(
        string name,
        int maxBatchSize,
        TimeSpan? windowPeriod = null);
}
```

**Reference**: See `/research/flow-composability-unification/handover/prototype/EpochBatchBlock.cs`

#### Priority 2: Helper Factories (Should Have)

**Helper Factory Methods** for common EpochActorBlock patterns:

```csharp
public static class EpochActorBlockExtensions
{
    /// <summary>
    /// Creates an EpochActorBlock for simple 1-to-1 transformations
    /// </summary>
    public static EpochActorBlock<TIn, TOut, SimpleTransformerActor<TIn, TOut>> 
        CreateEpochTransformer<TIn, TOut>(
            string name,
            IServiceScopeFactory scopeFactory,
            Func<TIn, TOut> transform);

    /// <summary>
    /// Creates an EpochActorBlock for processing with side effects
    /// </summary>
    public static EpochActorBlock<T, object, SimpleProcessorActor<T>> 
        CreateEpochProcessor<T>(
            string name,
            IServiceScopeFactory scopeFactory,
            Func<T, Task> process);
}
```

These helpers make it easy to use EpochActorBlock for common scenarios without creating custom actor types.

### Key Implementation Considerations

#### 1. Epoch Boundary Preservation

**Critical**: All operations MUST respect epoch boundaries.

```csharp
// Input: Epoch1[1,2,3] Epoch2[4,5]
// Batch size: 10

// CORRECT (EpochBatchBlock):
// Output: Epoch1[[1,2,3]] Epoch2[[4,5]]

// WRONG (would violate epoch boundaries):
// Output: [[1,2,3,4,5]]
```

**Implementation Pattern:**
- Process each epoch independently
- Never carry state across epoch boundaries
- Emit results per epoch

#### 2. Streaming Semantics

Maintain streaming characteristics:
- Use `yield return` for lazy evaluation
- No buffering beyond operation requirements
- Preserve backpressure through `WithCancellation`

```csharp
// Good: Streaming
await foreach (var item in epochStream.Items.WithCancellation(ct))
{
    yield return Transform(item);
}

// Bad: Buffering
var allItems = await epochStream.Items.ToListAsync(); // Don't do this!
foreach (var item in allItems)
{
    yield return Transform(item);
}
```

#### 3. Resource Management

Proper cleanup of resources:
```csharp
try
{
    // Processing logic
}
finally
{
    // Cleanup (timers, cancellation tokens, etc.)
    timer?.Dispose();
    cts?.Dispose();
}
```

#### 4. Cancellation Token Handling

Respect cancellation throughout:
```csharp
await foreach (var epochStream in input.WithCancellation(context.CancellationToken))
{
    await foreach (var item in epochStream.Items.WithCancellation(context.CancellationToken))
    {
        // Process item
    }
}
```

### Reusable Patterns

#### Pattern: Epoch Stream Creation

```csharp
private static IEpochStream<T> CreateEpochStream(
    EpochVector epoch,
    IAsyncEnumerable<T> items)
{
    return new EpochStreamWrapper(epoch, items);
}

private sealed class EpochStreamWrapper : IEpochStream<T>
{
    public EpochVector Epoch { get; }
    public IAsyncEnumerable<T> Items { get; }
    
    public EpochStreamWrapper(EpochVector epoch, IAsyncEnumerable<T> items)
    {
        Epoch = epoch ?? throw new ArgumentNullException(nameof(epoch));
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }
}
```

#### Pattern: Transformation Within Epoch

```csharp
private async IAsyncEnumerable<TOut> TransformEpochItems(
    IEpochStream<TIn> epochStream,
    IExecutionContext context)
{
    await foreach (var item in epochStream.Items.WithCancellation(context.CancellationToken))
    {
        await foreach (var result in _transformer(item, context).WithCancellation(context.CancellationToken))
        {
            yield return result;
        }
    }
}
```

#### Pattern: Batching Within Epoch

```csharp
private async IAsyncEnumerable<T[]> BatchEpochItems(
    IEpochStream<T> epochStream,
    IExecutionContext context)
{
    var batch = new List<T>();
    
    await foreach (var item in epochStream.Items.WithCancellation(context.CancellationToken))
    {
        batch.Add(item);
        
        if (batch.Count >= _maxBatchSize)
        {
            yield return batch.ToArray();
            batch.Clear();
        }
    }
    
    // Emit final batch (may be under-filled due to epoch boundary)
    if (batch.Count > 0)
    {
        yield return batch.ToArray();
    }
}
```

## Testing and Validation

### Test Coverage Required

#### Unit Tests

**1. EpochActorBlock Tests** ⭐ **PRIMARY BLOCK**
```csharp
[Fact]
public async Task EpochActorBlock_Should_PreserveEpochBoundaries()
{
    // Verify transformations happen within epochs
    // Verify epoch metadata preserved
    // Verify item count per epoch correct
}

[Fact]
public async Task EpochActorBlock_Should_Support1ToMany()
{
    // Verify 1-to-many transformations work
    // Verify all items stay within source epoch
}

[Fact]
public async Task EpochActorBlock_Should_SupportFiltering()
{
    // Verify filtering (1-to-0) works
    // Verify epochs can become empty
}

[Fact]
public async Task EpochActorBlock_Should_IsolateDIScopes()
{
    // Verify each actor gets its own DI scope
    // Verify scoped dependencies not shared across concurrent operations
}

[Fact]
public async Task EpochActorBlock_Should_SupportScopeRotation()
{
    // Verify scope rotation when actor requests it
    // Verify new scope created after rotation
}
```

**2. EpochBatchBlock Tests**
```csharp
[Fact]
public async Task EpochBatchBlock_Should_NeverCrossEpochBoundaries()
{
    // Critical: Batches must NEVER span epochs
    // Even if under-filled
}

[Fact]
public async Task EpochBatchBlock_Should_HandleWindowPeriod()
{
    // Verify time-based batching within epochs
}
```

#### Multi-Source Functional Tests ⭐ **HIGH PRIORITY**

These tests validate the multi-source composition patterns identified in research.

**Source**: `/research/flow-composability-unification/handover/prototype/MultiSourceSegmentationExperiments.cs`

**Test 1: Unified-Then-Segment Creates Single-Source Epochs**
```csharp
[Fact]
public async Task UnifiedThenSegment_Should_CreateSingleSourceEpochs()
{
    // Arrange: Two plain sources → UnionBlock → EpochSegmenter → EpochActorBlock
    // Act: Process through pipeline
    // Assert: 
    //   - All epochs have same sourceId ("unified")
    //   - No epoch vector ancestry
    //   - Items from both sources present
}
```

**Test 2: Segment-Then-Merge Creates Multi-Source Epochs**
```csharp
[Fact]
public async Task SegmentThenMerge_Should_CreateMultiSourceEpochs()
{
    // Arrange: Two sources → Individual Segmenters → MergeBlock
    // Act: Process through pipeline
    // Assert:
    //   - Epoch vectors have ancestry (multiple sourceIds)
    //   - Lifecycle events fire correctly
    //   - Requires lifecycle-aware blocks
}
```

**Test 3: Single-Source Boundaries Align With Transactions**
```csharp
[Fact]
public async Task SingleSource_Should_AlignEpochStreamWithTransaction()
{
    // Arrange: Single source → Segmenter → LifecycleAwareBlock
    // Act: Process and track lifecycle events
    // Assert:
    //   - OnEpochCreatedAsync fires at stream start
    //   - OnGlobalEpochAlignedAsync fires at stream end
    //   - Epoch vector consistent throughout
}
```

**Test 4: Producer Groups Work With Group-Level Segmentation**
```csharp
[Fact]
public async Task ProducerGroup_Should_WorkWithGroupSegmentation()
{
    // Arrange: ProducerGroup (4 sources, max 2 concurrent) → Single Segmenter
    // Act: Process with concurrent sources
    // Assert:
    //   - Group-level sourceId used
    //   - Correct batching across group members
    //   - No cross-epoch contamination
}
```

**Implementation Priority**: These 4 tests are CRITICAL for validating multi-source patterns (ADR-002). Implement early.

#### Integration Tests

**2. EpochProcessorBlock Tests**
```csharp
[Fact]
public async Task EpochProcessor_Should_ProcessItemsWithinEpochs()
{
    // Verify all items processed
    // Verify epoch boundaries respected
    // Verify terminal behavior (no output)
}
```

**3. EpochBatchBlock Tests**
```csharp
[Fact]
public async Task EpochBatch_Should_NotSpanEpochs()
{
    // Critical test: batches never cross epochs
    // Even with large batch size, small epochs
}

[Fact]
public async Task EpochBatch_Should_EmitUnderfilledBatches()
{
    // Verify final batch emitted even if under-filled
}
```

#### Integration Tests

**Composed Pipeline Tests**
```csharp
[Fact]
public async Task ComposedPipeline_Should_WorkCorrectly()
{
    // PlainSource → Segmenter → EpochTransform → EpochBatch → EpochProcess
    // Verify end-to-end correctness
}

[Fact]
public async Task MixedPipeline_Should_WorkCorrectly()
{
    // PlainSource → PlainTransform → Segmenter → EpochProcess
    // Verify mixed composition works
}
```

### Performance Validation

**Benchmark Scenarios:**

1. **Overhead Measurement**
   - Compare epoch-aware vs plain block throughput
   - Acceptance: < 10% overhead in micro-benchmarks

2. **Streaming Validation**
   - Verify no unnecessary buffering
   - Memory usage should be O(1) per item for transformers

3. **Batch Performance**
   - Measure batching overhead
   - Verify epoch boundary checks don't impact performance significantly

### Edge Cases

1. **Empty Input Stream**: Should produce zero epochs
2. **Single Item Epoch**: Should work correctly
3. **Large Epochs**: Should not buffer entire epoch in memory
4. **Cancellation**: Should handle cancellation cleanly at any point
5. **Exceptions**: Should propagate exceptions correctly

## Constraints and Requirements

### Technical Constraints

- **Target Framework**: .NET 8.0
- **Async/Await**: All operations async
- **Streaming**: Maintain backpressure through lazy evaluation
- **Cancellation**: Respect cancellation tokens
- **Resource Management**: Proper cleanup in finally blocks

### Performance Requirements

Based on research expectations:

- **Throughput**: < 10% reduction vs plain blocks in micro-benchmarks
- **Memory**: O(1) per item for streaming operations
- **Latency**: Minimal additional latency (one method call per item)

### API Requirements

- **Naming Convention**: Prefix with `Epoch` (e.g., `EpochTransformerBlock`)
- **Type Signatures**: `BlockBase<IEpochStream<TIn>, IEpochStream<TOut>>`
- **Consistency**: Follow same pattern across all blocks
- **Documentation**: XML comments for all public APIs

## Composability Patterns

Document and support these patterns:

### Pattern 1: Plain Pipeline (No Epochs)
```csharp
PlainSourceBlock → TransformerBlock → ProcessorBlock
```
Use when: No transactional boundaries needed

### Pattern 2: Full Epoch Pipeline
```csharp
PlainSourceBlock → EpochSegmenterBlock → EpochTransformerBlock → EpochBatchBlock → EpochProcessorBlock
```
Use when: Need epoch-based transactions, checkpointing, or tracking

### Pattern 3: Mixed Pipeline
```csharp
PlainSourceBlock → TransformerBlock → EpochSegmenterBlock → EpochProcessorBlock
```
Use when: Some processing is stateless, some needs epoch boundaries

## Documentation Requirements

### API Documentation

- XML comments on all public classes and methods
- Usage examples for each block
- When to use epoch-aware vs plain blocks

### User Guide

Create `/poc/docs/guides/epoch-aware-block-usage.md`:
- Explanation of epoch-aware blocks
- When to use which blocks
- Common patterns and examples
- Troubleshooting guide

### Update POC Glossary

Add entries to `/poc/docs/POC_GLOSSARY.md`:
- PlainSourceBlock
- EpochSegmenterBlock
- EpochTransformerBlock
- EpochProcessorBlock
- EpochBatchBlock
- Composability patterns

## Success Criteria

Implementation is complete when:

- [ ] All core blocks implemented (Transformer, Processor, Batch)
- [ ] All unit tests passing
- [ ] Integration tests validate composability
- [ ] Performance benchmarks meet requirements
- [ ] Edge cases handled correctly
- [ ] Documentation complete
- [ ] POC glossary updated
- [ ] Code review approved
- [ ] All 168+ tests passing (existing + new)

## Migration and Deployment

### Approach: Additive (No Deprecation)

**Decision**: New epoch-aware blocks are **additive**, not replacements.

**Rationale**:
1. **Plain blocks remain valid**: Many pipelines don't need epochs (stateless transformations, simple processing)
2. **Composability is the goal**: Users should be able to use plain blocks, epoch-aware blocks, or both
3. **Pattern 1 (Plain Pipeline)** is a supported use case: `PlainSource → Transformer → Processor`
4. **No functional overlap**: 
   - Plain `TransformerBlock` operates on `IAsyncEnumerable<T>`
   - `EpochTransformerBlock` operates on `IAsyncEnumerable<IEpochStream<T>>`
   - Different type signatures = different purposes

### What NOT to Deprecate

**Keep all existing plain blocks**:
- `TransformerBlock<TIn, TOut>` - for plain streams
- `ProcessorBlock<T>` - for plain streams
- `BatchBlock<T>` - for plain streams
- Any other blocks operating on `IAsyncEnumerable<T>`

**Reason**: These serve the valid Pattern 1 (plain pipeline) use case documented in the composability patterns.

### Adoption Path

Users can adopt incrementally:
1. **Continue using plain blocks** where epochs are not needed (Pattern 1)
2. **Use epoch-aware blocks** for epoch-structured pipelines (Pattern 2)
3. **Mix both approaches** by inserting segmenter where needed (Pattern 3)

### Test Migration

**For tests of existing plain blocks**:
- ✅ Keep all existing tests for plain blocks unchanged
- ✅ Add NEW tests for epoch-aware blocks (don't replace)
- Both test suites should pass independently

**Test refactoring NOT required**:
- Plain block tests validate Pattern 1 (plain pipelines)
- Epoch-aware block tests validate Pattern 2 (epoch pipelines)
- Both are valid, supported patterns

## Important: Not a Replacement Strategy

❗ **This implementation does NOT deprecate or replace existing plain blocks.**

**Why Both Block Types Are Needed**:

| Aspect | Plain Blocks | Epoch-Aware Blocks |
|--------|--------------|-------------------|
| **Input Type** | `IAsyncEnumerable<T>` | `IAsyncEnumerable<IEpochStream<T>>` |
| **Use Case** | Stateless transformations, no transaction boundaries | Epoch-based transactions, checkpointing, tracking |
| **Example** | Simple ETL, data transformation | Database writes with epochs, checkpoint tracking |
| **Composability Pattern** | Pattern 1 (plain pipeline) | Pattern 2 (epoch pipeline) |

**The Goal**: Enable **composability**, not replacement. Users should be able to:
- Use plain blocks for stateless processing
- Use epoch-aware blocks for epoch-structured processing
- Mix both by inserting `EpochSegmenterBlock` at the boundary

**Example of Valid Composition**:
```csharp
// Pattern 3: Mix plain and epoch-aware blocks
PlainSourceBlock 
  → TransformerBlock (plain, stateless)  // ✓ Keep this
  → EpochSegmenterBlock 
  → EpochProcessorBlock (epoch-aware)    // ✓ Add this
```

## ActorBlock Consolidation Opportunity

### Background

During research, a significant consolidation opportunity emerged: **ActorBlock could replace TransformerBlock and ProcessorBlock** as the unified pattern for business logic stream participants.

### Why ActorBlock?

**ActorBlock provides critical safety features**:
1. **DI scope isolation**: Each actor runs in its own async DI scope
2. **Scope rotation**: Optionally rotate scopes to clean up accumulated state (caches, DbContext, etc.)
3. **Concurrency safety**: Prevents accidental dependency sharing across concurrent operations

**Current landscape**:
- `TransformerBlock<TIn, TOut>`: Lightweight, no DI scope management
- `ProcessorBlock<T>`: Lightweight, no DI scope management
- `ActorBlock<TIn, TOut, TActor>`: DI-aware with scope management and rotation

### Recommendation: Consolidate around ActorBlock

**Replace specialized blocks with actor pattern**:
- ❌ Don't implement `EpochTransformerBlock` → ✅ Use `EpochActorBlock<TIn, TOut, TActor>` instead
- ❌ Don't implement `EpochProcessorBlock` → ✅ Use `EpochActorBlock<T, object, TActor>` instead  
- 🔄 Mark `TransformerBlock` and `ProcessorBlock` as `[Obsolete]` → Migrate to `ActorBlock`

**Rationale**:
1. **Safety first**: DI scope isolation should be the default, not optional
2. **Unified mental model**: Single "business logic participant" pattern
3. **Flexibility**: Rotation is opt-in (actors simply don't call `RequestRotation()` if not needed)
4. **No performance penalty**: Simple actors without rotation have minimal overhead

**Type support**:
- 1-to-1 transformation: `IStreamActor<T, T>` with single yield
- 1-to-many transformation: `IStreamActor<T, TOut>` with multiple yields
- Filtering: Don't yield for filtered items
- Processing: `IStreamActor<T, object>` with no yield (terminal)

### Implementation Tasks

**If consolidation is approved**:

1. **Implement EpochActorBlock<TIn, TOut, TActor>**
   - Replaces both EpochTransformerBlock and EpochProcessorBlock
   - DI scope per epoch (epoch boundaries serve as natural rotation points)
   - See `/research/flow-composability-unification/notes/actor-block-consolidation-analysis.md` for design

2. **Create Baseline Performance Benchmarks** (REQUIRED)
   - Run benchmarks for current `TransformerBlock` performance:
     * Simple 1-to-1: `x => x * 2`
     * 1-to-many: `x => [x, x * 2, x * 3]`
     * Filtering: `x => x % 2 == 0`
   - Run benchmarks for current `ProcessorBlock` performance:
     * Simple side effect: `x => counter++`
     * Async side effect: `x => SaveToDbAsync(x)`
   - Document results in `/research/flow-composability-unification/benchmarks/baseline-results.md`
   - **Critical**: These baselines needed for post-migration validation

3. **Mark Existing Blocks as Obsolete**
   - `[Obsolete("Use ActorBlock<TIn, TOut, TActor> instead")]` on TransformerBlock
   - `[Obsolete("Use ActorBlock<T, object, TActor> instead")]` on ProcessorBlock
   - Provide migration guidance in obsolete message

4. **Provide Helper Factories** (optional, eases migration)
   ```csharp
   public static ActorBlock<TIn, TOut, SimpleTransformActor<TIn, TOut>> 
       CreateTransformer<TIn, TOut>(
           string name, 
           Func<TIn, TOut> transform, 
           IServiceScopeFactory scopeFactory);
   ```

5. **Validate Performance**
   - Re-run benchmarks with ActorBlock equivalents
   - **Target**: < 10% performance regression vs baseline
   - If regression > 10%, investigate and optimize

6. **Update Tests**
   - Migrate tests to use ActorBlock pattern
   - Ensure coverage equivalent or improved

### Detailed Analysis

See comprehensive consolidation analysis:
- `/research/flow-composability-unification/notes/actor-block-consolidation-analysis.md`
- Includes: rationale, migration strategy, performance validation plan, alternatives considered

### Decision Required

**Question for implementation team**: Should we proceed with ActorBlock consolidation?

**If YES**:
- Implement `EpochActorBlock` instead of `EpochTransformerBlock`/`EpochProcessorBlock`
- Run baseline benchmarks for current blocks (TransformerBlock, ProcessorBlock)
- Mark plain blocks as obsolete after validation

**If NO**:
- Implement `EpochTransformerBlock`, `EpochProcessorBlock`, `EpochBatchBlock` as originally planned
- Keep existing plain blocks as-is (additive approach)

## Documentation Requirements

**CRITICAL**: Create user-facing documentation in codebase docs folders (not just research artifacts).

### Required Documentation

**1. Epoch Usage Guide** (High Priority)

**Location**: `/poc/docs/guides/using-epochs.md`

**Content**:
- Overview of epochs in DataFlow (what are epochs, why use them)
- Epoch segmentation strategies (when to use plain sources + segmenter)
- **Epoch granularity trade-offs** (per-entity vs per-batch)
- Multi-source patterns (unified-then-segment vs segment-then-merge)
- Decision tree: "Do I need epochs?" and "What granularity?"
- Examples: common patterns and anti-patterns

**Key Section**: **Epoch Granularity and Batching**
- Document the trade-off between granular epochs and bulk operations
- Explain when to use coarse epochs (bulk insert scenarios)
- Provide concrete examples (e.g., invoice processing with bulk insert needs)
- Reference ADR-002 for detailed technical rationale

**2. Block Documentation** (High Priority)

**Location**: `/poc/docs/blocks/epoch-actor-block.md` (or `/poc/docs/blocks/epoch-aware-blocks.md` if covering multiple)

**Content**:
- EpochActorBlock API reference
- EpochBatchBlock API reference  
- Usage examples with code samples
- DI scope behavior (isolation, rotation)
- Performance characteristics
- Comparison with plain blocks
- When to use epoch-aware blocks vs plain blocks

### Success Criteria

- [ ] `/poc/docs/guides/using-epochs.md` created with epoch granularity guidance
- [ ] `/poc/docs/blocks/` documentation created for implemented blocks
- [ ] Documentation reviewed and approved by architecture team
- [ ] Examples compile and run successfully

## Questions for Implementation Team

1. **ActorBlock Consolidation**: Should we consolidate around ActorBlock pattern? (See section above)
2. **Naming**: Confirm `Epoch` prefix is acceptable (or `EpochActor` if consolidating)
3. **Location**: Place in `/poc/DataFlow.POC/Blocks/` alongside existing blocks?
4. **Tests**: Place in `/poc/DataFlow.POC.Tests/` as `EpochAwareBlockTests.cs` or `EpochActorBlockTests.cs`?
5. **Benchmarks**: Who will run baseline performance benchmarks for TransformerBlock/ProcessorBlock?
6. **Documentation**: Who will write the epoch usage guide and block documentation? (See requirements above)

## References

### Research Documentation
- **Main Report**: `/research/flow-composability-unification/README.md`
- **Design Document**: `/research/flow-composability-unification/design/epoch-aware-blocks.md`
- **ADR**: `/research/flow-composability-unification/adr/2025-11-05-epoch-aware-block-pattern.md`

### Prototype Code
- **Location**: `/research/flow-composability-unification/handover/prototype/`
- Files: `EpochTransformerBlock.cs`, `EpochProcessorBlock.cs`, `EpochBatchBlock.cs`, `EpochAwareBlockTests.cs`

### Related Work
- **Decoupled Segmentation**: `/research/epoch-stream-separation/`
- **PR #146**: Implementation of PlainSourceBlock and EpochSegmenterBlock

---

**Created**: 2025-11-05
**Research Issue**: Flow Composability Unification
**Prototype Status**: Validated and working in research branch
