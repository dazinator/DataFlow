# Research: Flow Composability Unification with and without Epochs

## Executive Summary

This research investigates how to enable seamless composability between plain data streams and epoch-structured streams in DataFlow pipelines. The research validates that **epoch-aware wrapper blocks** successfully solve the composability gap introduced by decoupled epoch segmentation.

**Status**: Prototype complete, functional validation successful, implementation-ready issue created.

**Key Finding**: Epoch-aware blocks that process items within epoch boundaries enable full composability while preserving epoch structure and semantics.

## Research Objective

Evaluate approaches to enable DataFlow pipelines to work seamlessly with both:
- Plain streams: `IAsyncEnumerable<T>`
- Epoch streams: `IAsyncEnumerable<IEpochStream<T>>`

### Motivation

PR #146 introduced decoupled epoch segmentation with PlainSourceBlock and EpochSegmenterBlock, which separated concerns but created a composability gap:
- Plain blocks (TransformerBlock, ProcessorBlock, BatchBlock) cannot consume epoch streams
- Epoch-aware blocks (WriteContextBlock) cannot consume plain streams
- Without a segmenter, epoch-aware blocks cannot connect to plain sources

## Approaches Compared

### Approach 1: Adapter Pattern

Provide adapters to unwrap/wrap epoch streams.

**Example:**
```csharp
// Unwrap epochs to use plain block
var unwrapped = UnwrapEpochStream(epochStream);
var transformed = plainTransformer.ExecuteAsync(unwrapped, ctx);
var rewrapped = WrapInEpochStream(transformed, epoch);
```

**Pros:**
- Reuses existing plain blocks
- No new block implementations

**Cons:**
- Loses epoch boundary information during processing
- Cannot guarantee transformations respect epoch boundaries
- Complex API with wrap/unwrap operations
- Error-prone for users

### Approach 2: Epoch-Aware Wrapper Blocks (Recommended)

Create epoch-aware versions of common blocks that process items within epoch boundaries.

**Example:**
```csharp
// Transform items while preserving epoch structure
var transformer = new EpochTransformerBlock<Order, OrderDto>(
    "map",
    async (order, ctx) => { yield return MapToDto(order); });

var epochStreams = segmenter.ExecuteAsync(plainSource, ctx);
var transformed = transformer.ExecuteAsync(epochStreams, ctx);
```

**Pros:**
- Explicit epoch boundary preservation
- Type-safe: `IAsyncEnumerable<IEpochStream<T>>` throughout
- Clear API - no wrap/unwrap complexity
- Ensures operations respect epoch boundaries
- Enables composable patterns

**Cons:**
- Requires implementing epoch-aware versions of blocks
- Slightly more code to maintain

## Comparative Analysis

### 1. Composability

| Aspect | Adapter Pattern | Epoch-Aware Blocks |
|--------|----------------|-------------------|
| **Plain Pipelines** | Supported | Supported |
| **Epoch Pipelines** | Complex (wrap/unwrap) | Natural and explicit |
| **Mixed Pipelines** | Error-prone | Seamless |
| **Type Safety** | Weak (manual wrap/unwrap) | Strong (typed streams) |

**Winner**: ✅ **Epoch-Aware Blocks** - Better composability and type safety

### 2. Epoch Boundary Preservation

**Adapter Pattern:**
- Epoch information lost during unwrap
- No guarantee operations respect boundaries
- Manual tracking required

**Epoch-Aware Blocks:**
- Epoch boundaries explicitly preserved
- Operations guaranteed to respect boundaries
- Batches never span epochs
- Epoch metadata flows through pipeline

**Winner**: ✅ **Epoch-Aware Blocks** - Guarantees correctness

### 3. User Experience

**Adapter Pattern:**
```csharp
// Complex and error-prone
var unwrapped = UnwrapEpochStream(epochStream, out var epochInfo);
var result = plainBlock.ExecuteAsync(unwrapped, ctx);
var rewrapped = WrapInEpochStream(result, epochInfo);
```

**Epoch-Aware Blocks:**
```csharp
// Clear and intuitive
var result = epochAwareBlock.ExecuteAsync(epochStream, ctx);
```

**Winner**: ✅ **Epoch-Aware Blocks** - Simpler, clearer API

### 4. Implementation Complexity

| Aspect | Adapter Pattern | Epoch-Aware Blocks |
|--------|----------------|-------------------|
| **Code to Write** | Adapter utilities | Epoch-aware block implementations |
| **Maintenance** | Low (one adapter) | Medium (per block type) |
| **Correctness** | Hard (manual tracking) | Easy (type-enforced) |

**Winner**: ⚖️ **Tie** - Trade-off between code volume and correctness

## Prototype Validation

### Blocks Implemented

1. **EpochTransformerBlock<TIn, TOut>**
   - Applies transformations to items within epoch boundaries
   - Supports 1-to-1, 1-to-many, and filtering
   - Preserves epoch structure

2. **SimpleEpochTransformerBlock<TIn, TOut>**
   - Simplified 1-to-1 synchronous transformations
   - For simple mapping scenarios

3. **EpochProcessorBlock<T>**
   - Terminal block for side-effect processing
   - Processes items within epoch boundaries

4. **EpochBatchBlock<T>**
   - Batches items within epochs
   - **Key feature**: Batches never span epoch boundaries

### Test Results

All prototype tests pass (6 new tests, 168 total):

1. ✅ EpochTransformerBlock preserves epochs during transformation
2. ✅ SimpleEpochTransformerBlock handles synchronous mappings
3. ✅ EpochProcessorBlock processes items within epochs
4. ✅ EpochBatchBlock creates batches within epoch boundaries
5. ✅ EpochBatchBlock does not span batches across epochs
6. ✅ Complex composed pipelines work correctly

### Epoch Boundary Behavior

```
Input:  Epoch1[1,2,3] Epoch2[4,5,6,7] Epoch3[8,9]
Batch size: 5

Output: Epoch1[[1,2,3]] Epoch2[[4,5,6,7]] Epoch3[[8,9]]

Key: Batches respect epoch boundaries even when under-filled
```

## Design Principles

### 1. Epoch Boundary Respect

All epoch-aware blocks strictly preserve epoch boundaries:
- Transformations process items within their epochs
- Batches never cross epoch boundaries
- Epoch metadata flows through unchanged

### 2. Streaming Semantics

Blocks maintain streaming characteristics:
- No buffering beyond what's necessary for the operation
- Lazy evaluation with backpressure
- Items flow through as consumed

### 3. Type Safety

Strong typing ensures correctness:
- `IAsyncEnumerable<IEpochStream<T>>` input and output
- Compiler enforces epoch-aware connections
- No runtime type errors

### 4. Composability

Multiple composition patterns supported:

**Pattern 1: Plain Pipeline**
```csharp
PlainSourceBlock → TransformerBlock → ProcessorBlock
```

**Pattern 2: Full Epoch Pipeline**
```csharp
PlainSourceBlock → EpochSegmenterBlock → EpochTransformerBlock → EpochBatchBlock → EpochProcessorBlock
```

**Pattern 3: Mixed Pipeline**
```csharp
PlainSourceBlock → TransformerBlock → EpochSegmenterBlock → EpochProcessorBlock
```

## Performance Considerations

### Overhead Analysis

**Expected Overhead:**
- Minimal: One additional method call per item for epoch wrapper
- Streaming maintained: No buffering overhead
- Type checks: Compile-time, no runtime cost

**Benchmark Requirements:**
- Compare epoch-aware vs plain block performance
- Validate streaming semantics (no buffering)
- Measure memory usage

### Memory Usage

- **EpochTransformerBlock**: O(1) per item (streaming)
- **EpochBatchBlock**: O(batch_size) per batch (necessary for batching)
- **EpochProcessorBlock**: O(1) per item (terminal)

## Recommendations

### Recommendation: ✅ **Adopt Epoch-Aware Block Pattern**

**Rationale:**

1. **Correctness**: Type-safe epoch boundary preservation
2. **Composability**: Enables all three pipeline patterns seamlessly
3. **User Experience**: Clear, intuitive API
4. **Maintainability**: Explicit contracts, easier to understand
5. **Extensibility**: Pattern scales to additional block types

### Implementation Priority

**Phase 1: Core Blocks (Must Have)**
- EpochTransformerBlock
- EpochProcessorBlock
- EpochBatchBlock

**Phase 2: Advanced Blocks (Should Have)**
- SimpleEpochTransformerBlock
- EpochRouterBlock (if routing within epochs needed)
- EpochBroadcastBlock (if broadcasting within epochs needed)

**Phase 3: Specialized Blocks (Nice to Have)**
- Custom domain-specific epoch-aware blocks

### Migration Strategy

1. **Add epoch-aware blocks** to POC codebase
2. **Maintain plain blocks** for non-epoch pipelines
3. **Document both patterns** with clear usage guidelines
4. **Provide examples** showing when to use each

## Trade-offs Summary

### Advantages of Epoch-Aware Blocks

1. ✅ **Type Safety**: Compiler-enforced epoch handling
2. ✅ **Correctness**: Guaranteed epoch boundary preservation
3. ✅ **Clarity**: Explicit epoch-aware operations
4. ✅ **Composability**: Seamless pipeline construction
5. ✅ **Maintainability**: Clear contracts and behavior

### Disadvantages of Epoch-Aware Blocks

1. ❌ **Code Volume**: Need epoch-aware version of each block type
2. ❌ **Learning Curve**: Users need to understand two sets of blocks
3. ❌ **Maintenance**: More code to maintain and test

### Why Advantages Outweigh Disadvantages

- Correctness is paramount for data processing pipelines
- Type safety prevents runtime errors
- Clear API reduces user errors
- Maintenance cost is one-time; user benefits are ongoing

## Implementation Guidance

### Key Patterns

**Pattern: Epoch-Aware Transformation**
```csharp
public override async IAsyncEnumerable<IEpochStream<TOut>> ExecuteAsync(
    IAsyncEnumerable<IEpochStream<TIn>> input,
    IExecutionContext context)
{
    await foreach (var epochStream in input.WithCancellation(context.CancellationToken))
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
    await foreach (var item in epochStream.Items.WithCancellation(context.CancellationToken))
    {
        yield return Transform(item);
    }
}
```

**Pattern: Epoch-Aware Batching**
```csharp
// Batch within epoch, emit when batch full OR epoch ends
private async IAsyncEnumerable<T[]> BatchEpochItems(
    IEpochStream<T> epochStream,
    IExecutionContext context)
{
    var batch = new List<T>();
    
    await foreach (var item in epochStream.Items)
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

## Next Steps

1. ✅ Create comprehensive implementation issue with specifications
2. ✅ Document all findings and recommendations
3. ✅ Save prototype code as reference in handover folder
4. ⏳ Await reviewer approval for code reversion
5. ⏳ Revert POC code changes after approval
6. ⏳ Assign implementation issue to engineering team

## References

### Prototype Code

Located in `/research/flow-composability-unification/handover/prototype/`:
- `EpochTransformerBlock.cs` - Reference transformer implementation
- `EpochProcessorBlock.cs` - Reference processor implementation
- `EpochBatchBlock.cs` - Reference batcher implementation
- `EpochAwareBlockTests.cs` - Test scenarios and validation

### Related Research

- `/research/epoch-stream-separation/` - Decoupled epoch segmentation research
- PR #146 - Implementation of decoupled epoch segmentation

### Documentation Created

- `/research/flow-composability-unification/research-plan.md` - Research plan
- `/research/flow-composability-unification/handover/github-issue-implement-epoch-aware-blocks.md` - Implementation issue

## Conclusion

Epoch-aware wrapper blocks successfully solve the composability challenge introduced by decoupled epoch segmentation. The prototype validates that this approach:
- Preserves epoch boundaries correctly
- Maintains streaming semantics
- Enables flexible pipeline composition
- Provides clear, type-safe APIs

**Recommendation**: Implement epoch-aware blocks in the POC codebase following the specifications in the implementation handoff issue.
