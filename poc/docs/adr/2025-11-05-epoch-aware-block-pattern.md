# ADR-001: Epoch-Aware Block Pattern for Flow Composability

**Date**: 2025-11-05
**Status**: Accepted
**Context**: Research Issue - Flow Composability Unification

## Context

The decoupled epoch segmentation (PR #146) introduced a composability challenge:
- **PlainSourceBlock** produces `IAsyncEnumerable<T>` (plain streams)
- **EpochSegmenterBlock** converts to `IAsyncEnumerable<IEpochStream<T>>` (epoch streams)
- Plain blocks (TransformerBlock, ProcessorBlock) cannot consume epoch streams
- Epoch-aware blocks (WriteContextBlock) cannot consume plain streams

This breaks flexible pipeline composition. Users cannot freely mix and match blocks based on their needs.

## Decision

We adopt the **Epoch-Aware Block Pattern**: Create epoch-aware versions of common blocks that process items within epoch boundaries while preserving epoch structure.

**Core Pattern:**
```csharp
public class EpochTransformerBlock<TIn, TOut> : BlockBase<IEpochStream<TIn>, IEpochStream<TOut>>
{
    public override async IAsyncEnumerable<IEpochStream<TOut>> ExecuteAsync(
        IAsyncEnumerable<IEpochStream<TIn>> input,
        IExecutionContext context)
    {
        await foreach (var epochStream in input)
        {
            yield return CreateEpochStream(
                epochStream.Epoch,
                TransformItems(epochStream, context));
        }
    }
    
    private async IAsyncEnumerable<TOut> TransformItems(
        IEpochStream<TIn> epochStream,
        IExecutionContext context)
    {
        await foreach (var item in epochStream.Items)
        {
            yield return Transform(item);
        }
    }
}
```

### Blocks to Implement

**Priority 1 (Core):**
- `EpochTransformerBlock<TIn, TOut>` - Item transformations within epochs
- `EpochProcessorBlock<T>` - Terminal processing within epochs
- `EpochBatchBlock<T>` - Batching within epoch boundaries

**Priority 2 (Convenience):**
- `SimpleEpochTransformerBlock<TIn, TOut>` - Simple 1-to-1 mappings
- Additional blocks as needed

## Alternatives Considered

### Alternative 1: Adapter Pattern

**Approach**: Provide adapter utilities to unwrap/wrap epoch streams.

```csharp
var unwrapped = UnwrapEpochStream(epochStream, out var epochInfo);
var result = plainBlock.ExecuteAsync(unwrapped, ctx);
var rewrapped = WrapInEpochStream(result, epochInfo);
```

**Pros:**
- Reuses existing plain blocks
- Less code to write initially

**Cons:**
- Loses epoch boundary information during processing
- Cannot guarantee operations respect epoch boundaries
- Complex API with manual wrap/unwrap
- Error-prone for users
- Type safety lost during unwrap

**Why Not Chosen**: Correctness and type safety are more important than code volume.

### Alternative 2: Union Types

**Approach**: Create blocks that accept both plain and epoch streams.

```csharp
public class UniversalTransformerBlock<TIn, TOut> : 
    BlockBase<IAsyncEnumerable<T> | IAsyncEnumerable<IEpochStream<T>>, ...>
```

**Pros:**
- Single block handles both cases
- Less duplication

**Cons:**
- C# doesn't support union types natively
- Complex runtime type checking
- Unclear semantics for epoch handling
- Hard to maintain and test

**Why Not Chosen**: Implementation complexity and unclear semantics.

### Alternative 3: Do Nothing

**Approach**: Document that users should pick either plain OR epoch pipelines.

**Pros:**
- No code changes needed

**Cons:**
- Limits flexibility
- Users cannot mix approaches
- Poor user experience
- Defeats purpose of decoupled segmentation

**Why Not Chosen**: Doesn't solve the composability problem.

## Consequences

### Positive

1. **Type Safety**: Compiler enforces correct epoch handling
2. **Correctness**: Epoch boundaries guaranteed to be preserved
3. **Clear API**: Explicit epoch-aware operations, no wrap/unwrap
4. **Composability**: Enables flexible pipeline patterns:
   - Plain-only: `PlainSource → Transformer → Processor`
   - Epoch-only: `PlainSource → Segmenter → EpochTransformer → EpochProcessor`
   - Mixed: `PlainSource → Transformer → Segmenter → EpochProcessor`
5. **Maintainability**: Clear contracts and behavior
6. **Extensibility**: Pattern scales to new block types

### Negative

1. **Code Volume**: Need epoch-aware version of each block type
2. **Learning Curve**: Users must understand two sets of blocks
3. **Maintenance Overhead**: More code to maintain and test
4. **Documentation**: Need to document when to use which blocks

### Mitigation Strategies

**For Code Volume:**
- Share common patterns through base classes
- Generate similar blocks where feasible
- Start with core blocks only, add others as needed

**For Learning Curve:**
- Clear documentation with usage patterns
- Examples showing each composition pattern
- Guidelines on when to use epoch-aware vs plain blocks

**For Maintenance:**
- Comprehensive test coverage
- Shared test utilities
- Clear code structure

## Design Principles

### 1. Epoch Boundary Respect

All epoch-aware blocks MUST strictly preserve epoch boundaries:
- Operations process items within their source epoch
- Batches never cross epoch boundaries
- Epoch metadata flows through unchanged

**Example:** EpochBatchBlock with batch size 10, epochs of size 3:
```
Input:  Epoch1[a,b,c] Epoch2[d,e,f]
Output: Epoch1[[a,b,c]] Epoch2[[d,e,f]]

NOT: [[a,b,c,d,e,f]] ← Would violate epoch boundaries
```

### 2. Streaming Semantics

Blocks MUST maintain streaming characteristics:
- No buffering beyond what's necessary for the operation
- Lazy evaluation with backpressure preserved
- Items flow through as consumed

### 3. Consistent API

All epoch-aware blocks follow the same pattern:
```csharp
public class Epoch[Operation]Block<T> : 
    BlockBase<IAsyncEnumerable<IEpochStream<TIn>>, IAsyncEnumerable<IEpochStream<TOut>>>
{
    public override async IAsyncEnumerable<IEpochStream<TOut>> ExecuteAsync(
        IAsyncEnumerable<IEpochStream<TIn>> input,
        IExecutionContext context);
}
```

### 4. Error Propagation Semantics

An exception thrown inside an epoch-aware block terminates the entire stream (not just that epoch). This ensures consistent error handling semantics with plain blocks and prevents partial epoch processing.

### 5. Lifecycle Integration

Epoch-aware blocks can optionally implement `IEpochLifecycleParticipant` to receive creation/completion/alignment notifications from the Phase-6 lifecycle system. This enables coordination with tracking blocks and transactional boundaries.

### 6. Naming Convention

Epoch-aware blocks prefixed with `Epoch`:
- `EpochTransformerBlock` (not `TransformerEpochBlock`)
- `EpochProcessorBlock`
- `EpochBatchBlock`

## Implementation Guidance

### For Implementers

1. **Start with prototype code** in `/research/flow-composability-unification/handover/prototype/`
2. **Follow the pattern** consistently across all blocks
3. **Test epoch boundaries** thoroughly
4. **Validate streaming** - ensure no unnecessary buffering
5. **Document usage** with clear examples

### For Users

**When to use plain blocks:**
- Pipeline doesn't need epochs
- Stateless transformations
- Simple data flow

**When to use epoch-aware blocks:**
- Need transactional boundaries (DbContext per epoch)
- Checkpoint/recovery requirements
- Multi-source fan-in scenarios
- Need to track data lineage

**Composition patterns:**
```csharp
// Plain pipeline - no epochs needed
PlainSourceBlock → TransformerBlock → ProcessorBlock

// Full epoch pipeline
PlainSourceBlock → EpochSegmenterBlock → EpochTransformerBlock → EpochProcessorBlock

// Mixed - segment where needed
PlainSourceBlock → TransformerBlock → EpochSegmenterBlock → EpochProcessorBlock
```

### Migration Guidance

**Upgrading Existing Pipelines:**

Existing pipelines using plain blocks can migrate to epoch-aware equivalents by inserting an `EpochSegmenterBlock` followed by the epoch-aware version with identical logic.

**Example migration:**
```csharp
// Before (plain pipeline)
PlainSourceBlock → TransformerBlock(myLogic) → ProcessorBlock

// After (epoch-aware pipeline)
PlainSourceBlock → EpochSegmenterBlock → EpochTransformerBlock(myLogic) → EpochProcessorBlock
```

The transformation logic remains the same; only the block wrapper changes to support epoch boundaries.

## Validation

### Prototype Results

✅ All test scenarios pass:
- Transformations preserve epochs
- Batching respects epoch boundaries
- Complex pipelines compose correctly
- Streaming semantics maintained

### Performance

- Expected overhead: Minimal (one method call per item)
- No additional buffering
- Streaming maintained
- Benchmarks needed for actual implementation

### Performance Validation Task

**Phase 7 Benchmark Requirements:**
- Measure overhead of epoch-aware blocks versus plain blocks
- Ensure near parity (target: < 10% overhead in micro-benchmarks)
- Validate streaming semantics with memory profiling
- Benchmark complex pipeline compositions

## References

- Research: `/research/flow-composability-unification/README.md`
- Prototype code: `/research/flow-composability-unification/handover/prototype/`
- Implementation issue: `/research/flow-composability-unification/handover/github-issue-implement-epoch-aware-blocks.md`
- Related: `/research/epoch-stream-separation/` (Decoupled segmentation)

## Stakeholders

- **Researcher**: Validated approach through prototyping
- **Engineering Team**: Will implement based on this decision
- **Users**: Will benefit from flexible composability

## Review and Approval

**Approved By**: [Pending]
**Date**: [Pending]

This ADR documents the research finding and recommendation. Final implementation approval follows standard review process.
