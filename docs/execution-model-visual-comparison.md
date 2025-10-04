# Execution Model Visual Comparison

## Current Execution Model

All blocks execute concurrently via `Task.WhenAll`:

```mermaid
flowchart LR
    subgraph Producer["Producer Block"]
        P1[ExecuteAsync]
        P2[Produces Items]
        P3[Channel Buffer]
        P1 --> P2 --> P3
    end
    
    subgraph Transform["Transform Block"]
        T1[ExecuteAsync]
        T2[Pulls & Transforms]
        T3[Channel Buffer]
        T1 --> T2 --> T3
    end
    
    subgraph Processor["Processor Block"]
        R1[ExecuteAsync]
        R2[Pulls & Processes]
        R3[Outputs]
        R1 --> R2 --> R3
    end
    
    P3 -->|Data Flow| T2
    T3 -->|Data Flow| R2
    
    style Producer fill:#e1f5ff
    style Transform fill:#fff4e1
    style Processor fill:#e1ffe1
```

Each block runs independently in Task.WhenAll, requiring channels for data transfer.

## InlineTransformBlock (Inline Execution)

```mermaid
sequenceDiagram
    participant Producer
    participant InlineTransform
    participant Processor
    
    Note over Producer: ExecuteAsync
    Producer->>Producer: Produces Items 1-10
    Producer->>Producer: Writes to Channel
    
    Note over InlineTransform: ExecuteAsync (returns immediately)
    
    Note over Processor: ExecuteAsync
    Processor->>InlineTransform: foreach(transform.GetAsyncEnumerable())
    InlineTransform->>Producer: Pull item from upstream
    Producer-->>InlineTransform: Item data
    InlineTransform->>InlineTransform: Transform INLINE (in GetAsyncEnumerable)
    InlineTransform-->>Processor: Yield transformed item
    Processor->>Processor: Process item
    
    Note right of InlineTransform: NO transformation<br/>in ExecuteAsync!<br/>Work happens inline
```

**Characteristics:**
- ⚡ **Inline Execution**: Transform happens during downstream block's enumeration
- 🎯 **No Separate Transform Task**: ExecuteAsync completes immediately
- 💨 **Fast**: No separate task overhead for simple transforms
- 🔧 **1-to-1 Processing**: Single-threaded inline passthrough, no concurrency
- 💚 **Zero Buffering**: No channel buffer (completely channel-free)

**Performance**: ~12,500 items/sec (8ms for 100 items)

## TransformBlock (Default Capacity ~100)

```mermaid
sequenceDiagram
    participant Producer as Producer<br/>(Output Channel)
    participant Transform as Transform Block<br/>(Actors + Output Channel)
    participant Processor
    
    Note over Transform: ExecuteAsync starts actors
    
    loop Each Actor pulls & transforms
        Transform->>Producer: Pull item (GetAsyncEnumerable)
        Producer-->>Transform: Item data
        Note over Transform: IStreamTransformer<br/>processes item
        Transform->>Transform: Write to output channel (capacity=100)
    end
    
    Note over Transform: Output channel filling up...
    Transform->>Transform: Try write item 101 to output ❌
    Note over Transform: Blocked until downstream reads
    
    Processor->>Transform: Pull transformed item
    Transform-->>Processor: Transformed item
    
    Note over Transform: Space freed in output channel
    Transform->>Transform: Write item 101 ✅
    
    Note right of Transform: Pull-Based Model<br/>Transform pulls from upstream<br/>Downstream pulls from Transform<br/>Capacity = 100 (output buffer)
```

**Characteristics:**
- 🔄 **Pull-Based**: Transform block pulls items from upstream source via GetAsyncEnumerable
- 🔀 **Actor-Based Concurrency**: Can spawn multiple concurrent IStreamTransformer actors
- ⚡ **Concurrent Processing**: Increase MaxConcurrency for parallel transform execution
- 📤 **Output Buffering**: Transformed items written to output channel (capacity=100)
- ⚖️ **Balanced Backpressure**: Blocks when output buffer full, naturally slows upstream
- 🎯 **Good Default**: Works well for most scenarios
- 🔧 **Configurable**: Can adjust output capacity and MaxConcurrency

**Performance**: ~14,000 items/sec (7ms for 100 items)

**Key Difference**: TransformBlock can execute multiple `IStreamTransformer` actors concurrently (controlled by `MaxConcurrency` option), all outputting to the same buffered channel. This provides in-block parallelism for CPU-intensive transformations. InlineTransformBlock cannot do this - it's designed for lightweight 1-to-1 inline passthrough transforms with zero buffering.

## Comparison Summary

| Block Type | Execution Model | Concurrency | Throughput | Memory | Best For |
|------------|----------------|-------------|------------|---------|----------|
| **InlineTransformBlock** | ⚡ **GetAsyncEnum** | Single (1-to-1) | 🚀 12,500/sec | 💚 Zero buffer | Lightweight transforms |
| **TransformBlock** | ⚖️ **ExecuteAsync + Actor** | Multiple workers | ⚡ 14,000/sec | 💼 Moderate (100) | CPU-intensive work |

## Performance vs Concurrency Trade-off

```mermaid
graph LR
    A[InlineTransformBlock<br/>12.5K items/sec<br/>Zero buffer<br/>1-to-1 inline] 
    B[TransformBlock<br/>14K items/sec<br/>100 item buffer<br/>Actor-based concurrency]
    
    A -->|Inline Execution<br/>GetAsyncEnumerable| C[Execution Model]
    B -->|Buffered + Actors<br/>ExecuteAsync MaxConcurrency| C
    
    style A fill:#90EE90
    style B fill:#87CEEB
    style C fill:#FFE4B5
```

## Recommendation Decision Tree

```mermaid
flowchart TD
    Start([Need to transform data]) --> Q1{Is transformation<br/>CPU-intensive or<br/>I/O-bound?}
    
    Q1 -->|YES| Q2{Would benefit from<br/>concurrency?}
    Q1 -->|NO<br/>lightweight| Inline[Use InlineTransformBlock<br/>✅ Inline execution<br/>✅ Zero buffering<br/>✅ Optimal for simple transforms]
    
    Q2 -->|YES| Transform[Use TransformBlock<br/>✅ Actor-based<br/>✅ Set MaxConcurrency > 1<br/>✅ Parallel processing]
    Q2 -->|NO| Inline2[Use InlineTransformBlock<br/>✅ Simpler<br/>✅ Zero buffering]
    
    style Start fill:#e1f5ff
    style Inline fill:#90EE90
    style Inline2 fill:#90EE90
    style Transform fill:#87CEEB
```

## Key Insights

### InlineTransformBlock
> **Transformation work doesn't have to happen in ExecuteAsync. It can happen inline during GetAsyncEnumerable enumeration by the downstream block.**

This eliminates:
- Separate task overhead for the transformation
- All channel buffering overhead
- Coordination overhead between ExecuteAsync tasks

**Use when**: Transformation is lightweight (simple mapping, formatting) and doesn't need concurrency.

### TransformBlock
> **Provides actor-based concurrency with multiple IStreamTransformer instances working in parallel, all outputting to a shared buffered channel. Uses pull-based architecture where actors pull from upstream.**

Key capabilities:
- **Pull-Based**: Actors pull items from upstream source via GetAsyncEnumerable()
- **Output Buffer**: Default 100-item channel capacity (configurable) for transformed items
- **Concurrency**: Set `MaxConcurrency` to spawn multiple concurrent actors
- **Parallelism**: Multiple `IStreamTransformer` instances can process items simultaneously
- **Natural Backpressure**: When output buffer fills, actors slow down, which propagates upstream

**Use when**: Transformation is CPU-intensive or I/O-bound and would benefit from parallel processing.

## Choosing the Right Block

- **Lightweight transforms** → InlineTransformBlock
  - Simple mapping, formatting, string operations
  - 1-to-1 inline passthrough
  - Zero buffering overhead
  
- **CPU-intensive work** → TransformBlock
  - Complex calculations, parsing, encoding
  - Set MaxConcurrency > 1 for parallel execution
  - Built-in buffering with actor-based concurrency
