# Architecture Diagrams

## Current Design - Block Owns Everything

```
┌─────────────────────────────────────────────────────┐
│             TransformBlock<int, string>             │
│                                                     │
│  ┌────────────────────────────────────────────┐   │
│  │  Business Logic (Transformation)            │   │
│  │  • Transform int → string                   │   │
│  │  • Process items                            │   │
│  └────────────────────────────────────────────┘   │
│                                                     │
│  ┌────────────────────────────────────────────┐   │
│  │  Edge Management                            │   │
│  │  • SetSource(ISourceBlock<int>)             │   │
│  │  • _source field                            │   │
│  │  • GetAsyncEnumerable() for downstream     │   │
│  └────────────────────────────────────────────┘   │
│                                                     │
│  ┌────────────────────────────────────────────┐   │
│  │  Buffer Management                          │   │
│  │  • _outputChannel field                     │   │
│  │  • Channel creation                         │   │
│  │  • Channel completion                       │   │
│  │  • Capacity configuration                   │   │
│  └────────────────────────────────────────────┘   │
│                                                     │
│  ┌────────────────────────────────────────────┐   │
│  │  Execution Coordination                     │   │
│  │  • ExecuteAsync()                           │   │
│  │  • Parallel activities                      │   │
│  │  • Scope management                         │   │
│  └────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘

Problems:
❌ Blocks have too many responsibilities
❌ Tight coupling between transformation logic and infrastructure
❌ Hard to test blocks in isolation
❌ Difficult to change buffering behavior
❌ Complex routing requires blocks to manage multiple channels
```

## New Design - Separation of Concerns

```
┌────────────────────────────────────────────────────────────────────┐
│                          DataFlowGraph                              │
│                                                                      │
│  Responsibilities:                                                   │
│  • Topology management (blocks + edges)                            │
│  • Channel creation based on edge policies                         │
│  • Orchestration (parallel execution)                              │
│  • Completion and error coordination                               │
│                                                                      │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐          │
│  │   Block A   │    │   Block B   │    │   Block C   │          │
│  │  (Producer) │    │ (Transform) │    │ (Processor) │          │
│  └──────┬──────┘    └──────┬──────┘    └──────┬──────┘          │
│         │                  │                   │                    │
│    ┌────▼─────┐       ┌───▼────┐         ┌───▼────┐             │
│    │  Edge 1  │       │ Edge 2 │         │ Edge 3 │             │
│    │ Buffered │       │ Inline │         │Buffered│             │
│    │ Cap: 100 │       │        │         │ Cap: 10│             │
│    │ ┌──────┐ │       │        │         │┌──────┐│             │
│    │ │Channel│ │       │(direct)│         ││Channel││             │
│    │ └──────┘ │       │        │         │└──────┘│             │
│    └──────────┘       └────────┘         └────────┘             │
└────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────┐
│  TransformerBlock<int, string>      │
│                                     │
│  Responsibilities:                  │
│  • ONLY transformation logic        │
│                                     │
│  public override async              │
│    IAsyncEnumerable<string>         │
│    ExecuteAsync(                    │
│      IAsyncEnumerable<int> input,   │
│      IExecutionContext context)     │
│  {                                  │
│    await foreach (var item in input)│
│    {                                │
│      yield return Transform(item);  │
│    }                                │
│  }                                  │
└─────────────────────────────────────┘

Benefits:
✅ Blocks are simple, focused, testable
✅ Easy to change buffering without touching blocks
✅ Graph has full visibility into flow topology
✅ Can add features at edge level (rate limiting, monitoring)
✅ Routing is explicit via edge filtering
```

## Edge Strategy Pattern - Delivery Semantics

The POC implements edge strategies to formalize how data flows from source to target(s):

```
┌─────────────────────────────────────────────────────────────┐
│                  Edge Strategy Abstraction                   │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌────────────────────────────────────────────────────┐    │
│  │  BroadcastEdgeStrategy                             │    │
│  │  • One channel per target                          │    │
│  │  • All targets get all items                       │    │
│  │  • Independent backpressure per target             │    │
│  │                                                     │    │
│  │  Source ──┬─[Channel A]──▶ Target 1               │    │
│  │           └─[Channel B]──▶ Target 2               │    │
│  └────────────────────────────────────────────────────┘    │
│                                                              │
│  ┌────────────────────────────────────────────────────┐    │
│  │  CompetingEdgeStrategy (NEW)                       │    │
│  │  • ONE shared channel for all targets              │    │
│  │  • Each item consumed once                         │    │
│  │  • True competing consumers                        │    │
│  │                                                     │    │
│  │  Source ──[Shared Channel]──▶ Target 1            │    │
│  │                         ├────▶ Target 2            │    │
│  │                         └────▶ Target 3            │    │
│  │  (Targets compete for items)                       │    │
│  └────────────────────────────────────────────────────┘    │
│                                                              │
│  ┌────────────────────────────────────────────────────┐    │
│  │  CloningEdgeStrategy (NEW)                         │    │
│  │  • One channel per target                          │    │
│  │  • Items cloned before delivery                    │    │
│  │  • Independent copies for mutation isolation       │    │
│  │                                                     │    │
│  │  Source ──┬─[Channel A]──▶ Target 1 (clone)       │    │
│  │           └─[Channel B]──▶ Target 2 (clone)       │    │
│  └────────────────────────────────────────────────────┘    │
│                                                              │
│  ┌────────────────────────────────────────────────────┐    │
│  │  RoutingEdge (Existing)                            │    │
│  │  • Route-based filtering                           │    │
│  │  • Each route has own channel                      │    │
│  │  • Filters decide what passes through              │    │
│  └────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

### Edge Strategy Usage Examples

```csharp
// Broadcast - all consumers get all items
var broadcastEdge = new Edge(
    producer,
    new[] { consumer1, consumer2 },
    new BroadcastEdgeStrategy(BufferMode.Bounded, 100));

// Competing - consumers compete for items (concurrent processing!)
var competingEdge = new Edge(
    producer,
    new[] { processor1, processor2, processor3, processor4 },
    new CompetingEdgeStrategy(BufferMode.Bounded, 50));

// Cloning - independent copies for mutation safety
var cloningEdge = new Edge(
    producer,
    new[] { mutator1, mutator2 },
    new CloningEdgeStrategy(BufferMode.Bounded, 100));
```

### CompetingEdge Enables True Concurrent Processing

```
WITHOUT CompetingEdge (Old Approach):
┌────────────────────────────────────────────────┐
│  ConcurrentProcessorBlock<T>                   │
│  • maxConcurrency: 4                           │
│  • Internal semaphore                          │
│  • Internal task management                    │
│  • Block handles concurrency ❌                │
└────────────────────────────────────────────────┘

WITH CompetingEdge (New Approach):
┌────────────────────────────────────────────────┐
│  DataFlowGraph (Orchestration)                 │
│                                                 │
│  Producer ──[CompetingEdge]──▶ Processor1      │
│              (Shared Channel)  ├─▶ Processor2  │
│                                ├─▶ Processor3  │
│                                └─▶ Processor4  │
│                                                 │
│  • Graph handles concurrency ✅                │
│  • Processors simple & focused ✅              │
│  • Parallelism explicit in topology ✅         │
└────────────────────────────────────────────────┘

Benefits:
✅ Concurrency moved from block to orchestration layer
✅ Processors contain only business logic
✅ Easy to tune parallelism (add/remove processors)
✅ Natural load balancing via channel competition
✅ Clear separation of concerns
```

## Routing Example - Current Design

```
┌──────────────────────────────────────────────────┐
│       StructuredRoutingBlock<Item>               │
│                                                  │
│  Items arrive                                    │
│       ↓                                          │
│  ┌─────────────────┐                            │
│  │ Route Selection │ (item → "route-A")         │
│  └────────┬────────┘                            │
│           │                                      │
│  ┌────────▼─────────────────────────────────┐  │
│  │  Route Instance Management                │  │
│  │                                            │  │
│  │  _routeInstances["route-A"] = {           │  │
│  │    Channel: ...,                          │  │
│  │    DataFlow: ...,                         │  │
│  │    Task: ...                              │  │
│  │  }                                         │  │
│  │                                            │  │
│  │  Each route creates and manages:          │  │
│  │  • Its own channel                        │  │
│  │  • Its own downstream dataflow            │  │
│  │  • Its own execution task                 │  │
│  └───────────────────────────────────────────┘  │
└──────────────────────────────────────────────────┘

Problems:
❌ Router block has complex channel management
❌ Route creation is dynamic and complex
❌ Hard to see full topology (routes created at runtime)
❌ Cleanup and completion coordination is complex
```

## Routing Example - New Design

```
┌─────────────────────────────────────────────────────────────────┐
│                      DataFlowGraph                               │
│                                                                   │
│  ┌──────────┐                                                   │
│  │ Producer │                                                   │
│  └────┬─────┘                                                   │
│       │ Edge (buffered)                                         │
│  ┌────▼────────────┐                                            │
│  │  RouterBlock    │  (tags items: "route-A", "route-B")       │
│  └────┬────────────┘                                            │
│       │                                                          │
│       ├──────────────────────┬──────────────────┐              │
│       │                      │                   │              │
│  ┌────▼──────────┐    ┌─────▼─────────┐  ┌────▼─────────┐    │
│  │ RouteFilter   │    │ RouteFilter    │  │ RouteFilter  │    │
│  │ key="route-A" │    │ key="route-B"  │  │ key="route-C"│    │
│  └────┬──────────┘    └─────┬──────────┘  └────┬─────────┘    │
│       │                     │                   │              │
│       │ Edge               │ Edge              │ Edge         │
│  ┌────▼──────┐        ┌────▼──────┐      ┌────▼──────┐      │
│  │Processor A│        │Processor B│      │Processor C│      │
│  └───────────┘        └───────────┘      └───────────┘      │
└─────────────────────────────────────────────────────────────────┘

Benefits:
✅ Router is simple (just tags items)
✅ Routing logic in RouteFilter blocks (edge concern)
✅ All routes visible in graph topology
✅ Graph handles channel creation for all edges
✅ Standard edge management (no special routing code)
```

## Broadcast Example - New Design

```
┌─────────────────────────────────────────────────────────────────┐
│                      DataFlowGraph                               │
│                                                                   │
│  ┌──────────┐                                                   │
│  │ Producer │                                                   │
│  └────┬─────┘                                                   │
│       │                                                          │
│       │                                                          │
│  ┌────▼──────────┐                                              │
│  │ BroadcastBlock│  (pass-through)                             │
│  └────┬──────────┘                                              │
│       │                                                          │
│       ├──────────────────────┬────────────────┬────────────┐   │
│       │                      │                │            │   │
│       │ Edge 1              │ Edge 2         │ Edge 3     │   │
│       │ (buffered)          │ (buffered)     │ (buffered) │   │
│       │ Channel A           │ Channel B      │ Channel C  │   │
│       │                      │                │            │   │
│  ┌────▼──────┐          ┌───▼──────┐    ┌───▼──────┐    │   │
│  │Processor 1│          │Processor 2│    │Processor 3│    │   │
│  └───────────┘          └───────────┘    └───────────┘    │   │
└─────────────────────────────────────────────────────────────────┘

Graph coordinates:
1. Producer outputs items
2. BroadcastBlock is pass-through
3. Graph writes each item to all 3 edge channels (broadcast fanout)
4. Each processor reads from its own channel at its own pace
5. Natural backpressure per channel

✅ Broadcasting is graph behavior, not block complexity
✅ Each downstream has independent buffering and backpressure
✅ Easy to add/remove broadcast targets (just edges)
```

## Complex Flow - Diamond Topology

```
┌─────────────────────────────────────────────────────────────────┐
│                      DataFlowGraph                               │
│                                                                   │
│                    ┌──────────┐                                 │
│                    │ Producer │                                 │
│                    └────┬─────┘                                 │
│                         │                                        │
│                    ┌────▼───────┐                               │
│                    │ Broadcast  │                               │
│                    └────┬───────┘                               │
│                         │                                        │
│           ┌─────────────┴─────────────┐                        │
│           │                           │                        │
│      ┌────▼────────┐           ┌─────▼────────┐              │
│      │Transform A  │           │ Transform B  │              │
│      │  (heavy)    │           │   (light)    │              │
│      └────┬────────┘           └─────┬────────┘              │
│           │                           │                        │
│           └──────────┬────────────────┘                        │
│                      │                                          │
│                 ┌────▼─────────┐                               │
│                 │  Merge Block  │                               │
│                 │  (processor)  │                               │
│                 └───────────────┘                               │
└─────────────────────────────────────────────────────────────────┘

Benefits:
✅ Graph naturally handles splits and merges
✅ Each path can have different buffering/processing characteristics
✅ Clear visual representation of data flow
✅ Easy to modify topology without changing block code
```

## Code Example: Building a Flow

### Current Design

```csharp
public class MyFlowConfig : IDataFlowConfiguration
{
    public void Configure(DataFlowBuilder builder)
    {
        builder
            .AddProducer<int>("source", sp => new NumberProducer())
            .AddTransform<int, string>("transform", sp => new ToStringTransformer())
            .ReceiveFrom("source")  // Wiring via method calls
            .AddProcessor<string>("sink", sp => new LogProcessor())
            .ReceiveFrom("transform");  // More wiring
    }
}

// Behind the scenes: blocks call SetSource() on each other
// Topology is implicit in ReceiveFrom() calls
```

### New Design

```csharp
var producer = new ProducerBlock<int>("source", ctx => GenerateNumbers());
var transformer = new SimpleTransformerBlock<int, string>("transform", n => n.ToString());
var processor = new ProcessorBlock<string>("sink", async (item, ctx) => Log(item));

var graph = new DataFlowGraphBuilder("my-flow")
    .AddBlock(producer)
    .AddBlock(transformer)
    .AddBlock(processor)
    .Connect(producer, transformer, BufferMode.Bounded, capacity: 100)
    .Connect(transformer, processor, BufferMode.Bounded, capacity: 50)
    .Build();

await graph.ExecuteAsync(context);

// Topology is explicit via Connect() calls with edge configuration
// Easy to visualize, debug, and modify
```

## Summary

The new design provides:

1. ✅ **Clearer Separation**: Blocks, Edges, Graph each have distinct responsibilities
2. ✅ **Better Visibility**: Full topology known upfront (not created dynamically)
3. ✅ **Simpler Blocks**: Blocks are just stream transformers
4. ✅ **Flexible Buffering**: Edge-level configuration without changing blocks
5. ✅ **Natural Patterns**: Broadcasting, routing, merging are graph behaviors
6. ✅ **Easier Testing**: Each component testable in isolation
7. ✅ **Better Debugging**: Clear separation makes issues easier to locate
