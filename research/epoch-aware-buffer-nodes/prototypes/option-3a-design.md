# Option 3A: Epoch-Aware Buffer Block - Detailed Design

## Overview

Create a specialized block that buffers items within epoch boundaries while preserving epoch metadata.

## Key Design Principles

1. **Epoch Preservation**: Each input epoch stream maintains its identity through the buffer
2. **Item Buffering**: Buffer operates on items (`T`), not containers (`IEpochStream<T>`)
3. **Standard Block**: Integrates as a normal block in the graph (uses optimized routing)
4. **Fan-In/Fan-Out**: Supports multiple producers and multiple consumers

## Architecture

### Block Type Signature
```csharp
public class EpochBufferBlock<T> : BlockBase<IEpochStream<T>, IEpochStream<T>>
```

**Input**: `IAsyncEnumerable<IEpochStream<T>>` (stream of epoch streams)  
**Output**: `IAsyncEnumerable<IEpochStream<T>>` (stream of epoch streams with buffered items)

### Behavior

For each input epoch stream:
1. **Unwrap**: Extract items from `IEpochStream<T>.Items`
2. **Buffer**: Write items to `Channel<T>` (bounded, configured capacity)
3. **Re-wrap**: Create output `IEpochStream<T>` with same epoch metadata
4. **Yield**: Output epoch stream with buffered items

### Key Implementation Details

#### Per-Epoch Buffering
```csharp
public async IAsyncEnumerable<IEpochStream<T>> ExecuteAsync(
    IAsyncEnumerable<IEpochStream<T>> input,
    IExecutionContext context)
{
    await foreach (var epochStream in input)
    {
        // Create buffer for this epoch's items
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(_capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false, // Multiple consumers may read
            SingleWriter = true   // Single epoch enumeration writes
        });
        
        // Start writer task (unwrap items into channel)
        var writerTask = WriteEpochItemsAsync(epochStream, channel.Writer, context.CancellationToken);
        
        // Create output epoch stream (re-wrap from channel)
        var outputStream = new ChannelBackedEpochStream<T>(
            epochStream.Epoch,
            epochStream.EpochScope,
            channel);
        
        yield return outputStream;
        
        // Ensure writer completes before next epoch
        await writerTask;
    }
}
```

#### Writer Task (Unwrap)
```csharp
private async Task WriteEpochItemsAsync(
    IEpochStream<T> epochStream,
    ChannelWriter<T> writer,
    CancellationToken cancellationToken)
{
    try
    {
        await foreach (var item in epochStream.Items.WithCancellation(cancellationToken))
        {
            await writer.WriteAsync(item, cancellationToken);
        }
    }
    finally
    {
        writer.Complete();
    }
}
```

## Integration with Graph Builder

### API Design

```csharp
public static class EpochBufferBlockExtensions
{
    public static DataFlowGraphBuilder AddEpochBuffer<T>(
        this DataFlowGraphBuilder builder,
        string name,
        int capacity,
        Action<IServiceCollection>? configureServices = null)
    {
        return builder.AddBlock<EpochBufferBlock<T>>(
            name,
            services =>
            {
                services.AddTransient<IBufferConfiguration>(sp => 
                    new BufferConfiguration(capacity));
                configureServices?.Invoke(services);
            });
    }
}
```

### Usage Example

```csharp
builder
    .AddSourceActor<MySourceActor1>("source1")
    .AddSourceActor<MySourceActor2>("source2")
    .AddEpochBuffer<int>("buffer", capacity: 100)
        .ReceiveFrom("source1")
        .ReceiveFrom("source2")
    .AddProcessor<MyProcessor>("processor")
        .ReceiveFrom("buffer");
```

## Comparison with Current Buffer Nodes

| Feature | BufferNode | EpochBufferBlock |
|---------|-----------|------------------|
| **Target type** | Plain types | Epoch streams |
| **Buffering** | Items | Items (within epochs) |
| **Epoch preservation** | N/A | ✅ Preserves |
| **Graph appearance** | Explicit node | Standard block |
| **Routing** | TypedBufferNodeRouter | TypedEdgeRouter (optimized) |
| **Fan-in** | ✅ Multiple producers | ✅ Multiple producers |
| **Fan-out** | ✅ Multiple consumers | ✅ Multiple consumers |
| **DI support** | ❌ No | ✅ Yes (BlockContext) |

## Advantages

1. **Semantic Correctness**:
   - Buffers items, not containers
   - Preserves epoch boundaries
   - Capacity means item count, not epoch count

2. **Performance**:
   - Uses optimized routing path (no dynamic cast)
   - Direct channel writer access via SingleTargetRouter
   - No additional overhead beyond channel operations

3. **Integration**:
   - Standard block (no special graph handling)
   - Works with existing edge strategies
   - Supports DI naturally

4. **User Experience**:
   - Clear, intuitive API
   - Consistent with other blocks
   - Type-safe

## Disadvantages

1. **New Concept**:
   - Users must learn about epoch buffers vs plain buffers
   - Two buffer patterns in codebase

2. **Per-Epoch Overhead**:
   - Creates new channel for each epoch
   - Writer task per epoch
   - May be higher overhead than single shared channel

3. **Epoch Boundary Dependency**:
   - Strictly preserves epoch boundaries
   - Can't merge items from different epochs
   - May not match all use cases

## Alternative: Epoch-Merging Buffer

If strict epoch preservation is not required, alternative design:

### Epoch-Merging Buffer
```csharp
public class EpochMergingBufferBlock<T> : BlockBase<IEpochStream<T>, IEpochStream<T>>
{
    private readonly Channel<T> _sharedChannel;
    
    // All input epochs feed into same channel
    // Output as single merged epoch stream
}
```

**Trade-offs**:
- ✅ Lower overhead (single channel)
- ✅ Can buffer across epoch boundaries
- ❌ Loses epoch identity
- ❌ Unclear epoch semantics on output

**Recommendation**: Start with strict epoch preservation (per-epoch buffering), add merging variant only if needed.

## Implementation Complexity

**Estimated effort**: Medium

**Components**:
1. `EpochBufferBlock<T>` class (~100-150 LOC)
2. `IBufferConfiguration` interface (~10 LOC)
3. Extension methods (~30 LOC)
4. Tests (~200-300 LOC)
5. Documentation updates (~50 LOC)

**Total**: ~400-500 LOC

**Risk**: Low - builds on existing block infrastructure

## Testing Strategy

1. **Unit tests**:
   - Single epoch buffering
   - Multiple epochs in sequence
   - Capacity enforcement (backpressure)
   - Cancellation handling
   - Epoch metadata preservation

2. **Integration tests**:
   - Multiple producers → buffer → single consumer
   - Single producer → buffer → multiple consumers
   - Multiple producers → buffer → multiple consumers
   - Buffer in middle of larger pipeline

3. **Performance tests**:
   - Compare with plain buffer nodes (non-epoch)
   - Measure overhead vs direct connection
   - Benchmark different capacity settings

## Open Questions

1. **Epoch completion**: How do we signal epoch completion downstream?
   - Current design: When channel completes, epoch stream completes naturally
   - Alternative: Explicit completion signal?

2. **Error handling**: What happens if writing to channel fails?
   - Current design: Exception propagates, epoch stream faults
   - Alternative: Dead letter channel?

3. **Capacity semantics**: Should capacity be per-epoch or global?
   - Current design: Per-epoch (each epoch gets its own channel with capacity)
   - Alternative: Global capacity across all active epochs?

4. **Multiple consumers**: How do multiple consumers share buffered items?
   - Current design: Uses competing/broadcast edge strategy (same as other blocks)
   - Should buffer block itself handle this?

## Decision Points

Before implementation:
1. ✅ Confirm per-epoch buffering approach (vs merging)
2. ❓ Decide on capacity semantics (per-epoch vs global)
3. ❓ Clarify multiple consumer behavior
4. ❓ Define error handling strategy

## Next Steps

1. Create minimal prototype
2. Test with realistic scenarios
3. Measure performance impact
4. Refine design based on findings
5. Create implementation specification for handover
