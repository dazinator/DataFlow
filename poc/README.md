# DataFlow POC - New Edge-First Design

This is a proof-of-concept implementation exploring a new architecture for the DataFlow library.

## Goals

The primary goal is to better separate concerns in the dataflow execution model:

1. **Blocks** - Pure transformation logic (`IAsyncEnumerable<TIn>` → `IAsyncEnumerable<TOut>`)
2. **Edges** - Connection management, buffering policy, and data routing
3. **Graph** - Orchestration, topology management, and execution coordination

## Key Design Principles

### Current Architecture Issues
- Blocks handle three distinct concerns: node definition, edge management, and execution policy
- Buffered vs inline mode logic is mixed into block implementations
- Routing blocks must manage their own channels for each downstream route
- Topology and execution mechanics are tightly coupled

### New Architecture Benefits
- **Separation of Concerns**: Wiring and scheduling live in the Graph; blocks remain composable units
- **Simpler Buffering**: The Edge determines buffering policy, not the block
- **Better Diagnostics**: The graph can inspect edge states (queue length, backpressure)
- **Extensibility**: New policies (rate limiting, fan-out, retry) attach to edges without rewriting blocks

## Architecture Overview

### IBlock Interface
Pure transformation logic - takes input, produces output:
```csharp
public interface IBlock<TIn, TOut>
{
    string Name { get; }
    IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input, 
        CancellationToken cancellationToken);
}
```

### Edge Class
Represents a connection between blocks with buffering policy:
```csharp
public class Edge<T>
{
    public IBlock SourceBlock { get; }
    public IBlock TargetBlock { get; }
    public BufferMode BufferMode { get; }
    public int BufferCapacity { get; }
    // Owns channel for buffered edges
}
```

### Graph Class
Orchestrates execution:
```csharp
public class DataFlowGraph
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Wire up blocks with edges
        // Start all blocks
        // Wait for completion
    }
}
```

## Block Types Supported

- **Producer** - Source blocks that generate data
- **ActorBlock** - DI-aware transformation and processing with scope isolation and rotation ([see guide](./ACTOR_BLOCK.md))
- **Batch** - Accumulates items into batches
- **Router** - Routes items to different downstream targets based on criteria
- **Broadcast** - Sends each item to multiple downstream targets

**Note**: As of November 2025, the POC has consolidated around the ActorBlock pattern for DI scope safety. The previous TransformerBlock and ProcessorBlock have been removed in favor of ActorBlock, which provides automatic dependency injection scope management and prevents common concurrency bugs. See `/poc/docs/migrations/actor-block-migration.md` for migration guidance.

## Testing Strategy

The POC includes comprehensive tests demonstrating:
1. Basic linear flows (Producer → Transformer → Processor)
2. Batching scenarios
3. Routing with multiple downstream paths
4. Broadcasting to multiple consumers
5. Complex topologies combining multiple patterns
6. ActorBlock with DI scope rotation and memory management

## Comparison with Current Design

See test results and documentation for detailed comparison of:
- Code complexity
- Performance characteristics
- Extensibility scenarios
- Debugging experience
