# Envelope and Control Signal Framework

This document describes the envelope and control signal framework implemented in the DataFlow POC.

## Overview

The envelope framework provides a unified way to send both **data items** and **control signals** through the dataflow pipeline. This enables features like checkpointing, heartbeats, and coordinated processing without requiring special handling in the pipeline topology.

## Core Concepts

### IDataEnvelope

All items flowing through envelope-aware pipelines implement `IDataEnvelope`:

```csharp
public interface IDataEnvelope { }
```

### Envelope Types

#### DataItem<T>

Wraps user data of type T:

```csharp
public sealed record DataItem<T>(T Value) : IDataEnvelope;

// Usage
var envelope = new DataItem<int>(42);
var envelope = 42.ToEnvelope(); // Extension method
```

#### CheckpointBarrier

Represents a checkpoint or barrier for coordinated processing:

```csharp
public sealed record CheckpointBarrier(Guid Id, DateTime CreatedAt) : IDataEnvelope;

// Usage
var barrier = new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
```

#### Heartbeat

Signals liveness and progress tracking:

```csharp
public sealed record Heartbeat(DateTime Timestamp) : IDataEnvelope;

// Usage
var heartbeat = new Heartbeat(DateTime.UtcNow);
```

## Control Signal Propagation Rules

### Broadcast Edges

Control signals and data items are both broadcast to all downstream targets:

```csharp
var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
builder.AddEdge(new Edge(source, new[] { target1, target2 }, envelopeStrategy));
```

- **Data items**: All targets receive all data
- **Control signals**: All targets receive all control signals

### Competing Edges

Data items compete (one consumer per item), control signals follow competing semantics:

```csharp
var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateCompeting();
builder.AddEdge(new Edge(source, new[] { target1, target2 }, envelopeStrategy));
```

- **Data items**: First available consumer gets the item
- **Control signals**: First available consumer gets the signal

> **Note**: For guaranteed control signal delivery to all competing consumers, use broadcast edges or implement a hybrid pattern.

### Routed Edges

Data items route based on key, control signals broadcast to all routes:

```csharp
var routeMap = new Dictionary<string, IBlock>
{
    ["route1"] = target1,
    ["route2"] = target2
};
var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateRouted(routeMap);
builder.AddEdge(new Edge(source, new[] { target1, target2 }, envelopeStrategy));
```

- **Data items**: Routed based on route key
- **Control signals**: Broadcast to all routes

## Envelope-Aware Blocks

### SimpleEnvelopeTransformerBlock<TIn, TOut>

Transforms data items 1:1 while forwarding control signals:

```csharp
var transformer = new SimpleEnvelopeTransformerBlock<int, string>(
    "transformer", 
    i => $"Value-{i}");
```

### AsyncEnvelopeTransformerBlock<TIn, TOut>

Async transformation of data items:

```csharp
var transformer = new AsyncEnvelopeTransformerBlock<int, int>(
    "async-transformer",
    async (i, ctx) => 
    {
        await SomeAsyncWork(i);
        return i * 2;
    });
```

### EnvelopeProjectorBlock<TIn, TOut>

Projects one input to zero or more outputs (flatMap pattern):

```csharp
var projector = new EnvelopeProjectorBlock<int, int>(
    "projector",
    (i, ctx) => AsyncEnumerable.Range(1, i));
```

### EnvelopeProcessorBlock<T>

Terminal block that can observe control signals:

```csharp
var processor = new EnvelopeProcessorBlock<int>(
    "processor",
    processData: async (value, ctx) => 
    {
        Console.WriteLine($"Processing: {value}");
    },
    processControl: async (signal, ctx) => 
    {
        if (signal is CheckpointBarrier barrier)
        {
            Console.WriteLine($"Checkpoint: {barrier.Id}");
        }
    });
```

## Usage Examples

### Basic Pipeline with Control Signals

```csharp
var producer = new ProducerBlock<IDataEnvelope>("producer", ctx => ProduceData(ctx));
var transformer = new SimpleEnvelopeTransformerBlock<int, string>("transform", i => $"Item-{i}");
var processor = new EnvelopeProcessorBlock<string>("processor", async (s, ctx) => 
{
    Console.WriteLine(s);
});

var builder = new DataFlowGraphBuilder("example");
builder.AddBlock(producer)
    .AddBlock(transformer)
    .AddBlock(processor);

var strategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
builder.AddEdge(new Edge(producer, transformer, strategy));
builder.AddEdge(new Edge(transformer, processor, strategy));

var graph = builder.Build();
await graph.ExecuteAsync(context);

async IAsyncEnumerable<IDataEnvelope> ProduceData(IExecutionContext ctx)
{
    yield return new DataItem<int>(1);
    yield return new DataItem<int>(2);
    yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
    yield return new DataItem<int>(3);
}
```

### Progress Tracking with Heartbeats

```csharp
var processor = new EnvelopeProcessorBlock<int>(
    "progress-processor",
    processData: async (value, ctx) => 
    {
        itemsProcessed++;
        await DoWork(value);
    },
    processControl: async (signal, ctx) => 
    {
        if (signal is Heartbeat heartbeat)
        {
            Console.WriteLine($"Progress: {itemsProcessed} items at {heartbeat.Timestamp}");
        }
    });
```

### Multi-Path Pipeline

```csharp
// Producer broadcasts data and control signals to multiple processing paths
var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
builder.AddEdge(new Edge(producer, new[] { path1, path2, path3 }, envelopeStrategy));

// All paths receive all data items and all control signals
// Each path can transform data independently
// Control signals coordinate across all paths
```

## Adapter Utilities

### Converting Plain Streams to Envelope Streams

```csharp
// Wrap a plain data stream
IAsyncEnumerable<int> plainStream = GetData();
IAsyncEnumerable<IDataEnvelope> envelopeStream = 
    EnvelopeAdapter.WrapInEnvelopes(plainStream);

// Unwrap back to plain data (control signals ignored)
IAsyncEnumerable<int> unwrapped = 
    EnvelopeAdapter.UnwrapEnvelopes<int>(envelopeStream);
```

### Filtering Streams

```csharp
// Get only data items
var dataOnly = EnvelopeAdapter.FilterDataItems(mixedStream);

// Get only control signals
var controlOnly = EnvelopeAdapter.FilterControlSignals(mixedStream);
```

## Extension Methods

```csharp
// Check envelope type
bool isData = envelope.IsDataItem();
bool isControl = envelope.IsControlSignal();

// Extract value (throws if wrong type)
int value = envelope.GetValue<int>();

// Try extract value (returns false if wrong type)
if (envelope.TryGetValue<int>(out var value))
{
    Console.WriteLine(value);
}
```

## Design Principles

1. **Unified Stream**: Both data and control flow through the same channels
2. **Transparent Forwarding**: Blocks forward control signals by default
3. **Type Safety**: Generic envelope blocks provide compile-time type checking
4. **Minimal Overhead**: Control signals add minimal overhead when not used
5. **Extensibility**: New control signal types can be added without changing infrastructure

## Future Enhancements

The envelope framework enables:

1. **Checkpoint and Recovery**: Use CheckpointBarrier to coordinate state snapshots
2. **Adaptive Scaling**: Control signals to trigger scaling up/down
3. **Watermarks**: Time-based progress tracking for event-time processing
4. **Flow Control**: Backpressure signaling through control messages
5. **Metrics Injection**: Per-item metadata without polluting data model

## Known Limitations

1. **Competing Edges**: Control signals follow competing semantics (not broadcast) in competing mode
2. **Order Guarantees**: Control signal order relative to data depends on edge strategy
3. **Type Constraints**: All blocks in an envelope pipeline must work with `IDataEnvelope`

## Best Practices

1. **Use broadcast edges** when control signals must reach all consumers
2. **Insert control signals judiciously** - they add overhead
3. **Handle control signals at pipeline boundaries** for state management
4. **Use typed envelope blocks** for compile-time safety
5. **Document control signal semantics** in your pipeline design

## Conclusion

The envelope framework provides a solid foundation for building sophisticated dataflow pipelines with coordinated control. By separating data from control signals while keeping them in-band, the framework enables features like checkpointing and progress tracking without complicating the pipeline topology.

The design emphasizes pragmatism and simplicity - control signals flow naturally through the pipeline, blocks forward them transparently by default, and advanced scenarios can be handled with specialized blocks or stream processing.

As the POC evolves, this framework can be extended with additional control signal types and more sophisticated propagation semantics to meet emerging requirements.
