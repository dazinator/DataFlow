# Analysis: POC Routing Behavior

**Date**: 2025-11-20  
**Status**: Complete  
**Related Issue**: [#75](https://github.com/uniun-technology/lib-dataflow/issues/75) - Selective Routing Feature Request

## Executive Summary

This analysis investigates the current state of routing in the POC codebase to:
1. Document the routing mechanisms available
2. Determine if issue #75 (selective routing vs broadcast-and-filter) is still relevant
3. Identify gaps in routing documentation under `/poc/docs/guides`
4. Provide recommendations for documentation improvements

### Key Findings

1. **POC supports selective routing for load balancing but NOT for content-based routing**
2. **Issue #75 IS STILL RELEVANT** - selective content-based routing (by routing key) is not implemented
3. **Routing documentation exists but is scattered** - needs consolidation in guides
4. **Two routing mechanisms exist in POC**: Edge-level routing and Block-based routing (Structured routing is production code only)

### Recommendation

⚠️ **Issue #75 remains relevant** - POC lacks selective content-based routing:
- `CompetingEdgeStrategy` provides selective routing for load balancing (not content-based)
- `RouterBlock` + `RouteFilterBlock` uses inefficient broadcast-and-filter pattern
- Missing: Selective routing based on content (e.g., routing keys) without broadcasting

✅ **Create routing guide** under `/poc/docs/guides/routing.md` to consolidate routing documentation

✅ **Document broadcast strategy** - BroadcastEdgeStrategy uses concurrent broadcasting (Task.WhenAll)

---

## Context and Motivation

The issue #75 (referenced in the analysis request) asked whether the POC needed to develop a "selective routing strategy" rather than relying solely on "broadcast and filter based strategy." This analysis was commissioned to:

- Understand the current state of routing in the POC
- Determine if issue #75's concerns are still valid
- Ensure adequate documentation exists for routing features

---

## Observations and Analysis

### 1. Routing Mechanisms in the POC

The POC implements **two distinct routing mechanisms** (note: Structured Routing exists only in production code under `/src`, not in POC):

#### Mechanism 1: Edge Strategy Pattern (Recommended for Load Balancing)

**Location**: `poc/DataFlow.POC/Core/EdgeStrategy.cs`

The edge strategy pattern is the primary routing mechanism in the POC. It provides formalized delivery semantics at the **edge level** rather than requiring specialized blocks.

**Available Strategies**:

| Strategy | Delivery Pattern | Use Case | Selective? |
|----------|-----------------|----------|------------|
| `BroadcastEdgeStrategy` | All targets get all items (separate channels) | Fan-out scenarios, monitoring | ❌ No (broadcast) |
| `CompetingEdgeStrategy` | Each item consumed once (shared channel) | Concurrent processing, load balancing | ✅ Yes (selective) |
| `CloningEdgeStrategy` | All targets get independent clones | Mutation isolation | ❌ No (broadcast with cloning) |

**Key Architecture Points**:
- Edges own delivery semantics, not blocks
- Blocks remain simple and focused on business logic
- Graph orchestrates how data flows between blocks
- Uses typed channels to avoid boxing overhead

**Example - Selective Routing via CompetingEdgeStrategy**:
```csharp
var producer = BlockHelpers.CreateProducer<int>("producer", ...);
var processor1 = BlockHelpers.CreateProcessor<int>("proc1", ...);
var processor2 = BlockHelpers.CreateProcessor<int>("proc2", ...);
var processor3 = BlockHelpers.CreateProcessor<int>("proc3", ...);

// Create competing edge - each item goes to ONE processor
var competingEdge = new Edge(
    producer,
    new[] { processor1, processor2, processor3 },
    new CompetingEdgeStrategy(BufferMode.Bounded, 100));

graph.AddEdge(competingEdge);

// Result: Items are distributed among processors (selective routing)
// Each item is consumed by exactly ONE processor
```

This is **true selective routing for load balancing** - each item goes to exactly one target based on availability. However, this does NOT support content-based routing (routing keys).

**Limitation**: CompetingEdgeStrategy cannot inspect item content to make routing decisions. It only distributes items among competing consumers.

#### Mechanism 2: Block-Based Routing (RouterBlock + RouteFilterBlock)

**Location**: `poc/DataFlow.POC/Blocks/RouterBlock.cs`

This mechanism uses specialized blocks for routing:
- `RouterBlock<T>` - Tags items with route keys
- `RouteFilterBlock<T>` - Filters items based on route keys
- `RoutingEdge` - Specialized edge that checks route keys

**Pattern**: Broadcast-and-Filter (Concurrent)

**Broadcasting Strategy**: Items are broadcast to ALL filters **concurrently** using `Task.WhenAll`. This means:
- Each filter receives ALL items simultaneously
- Filters run in parallel
- Each filter drops items that don't match its route key
- Inefficient for high-volume scenarios with many routes

```csharp
// RouterBlock tags each item with a route key
var router = new RouterBlock<int>("router", i => i % 2 == 0 ? "even" : "odd");

// RouteFilterBlocks filter based on route keys
var evenFilter = new RouteFilterBlock<int>("even-filter", "even");
var oddFilter = new RouteFilterBlock<int>("odd-filter", "odd");

// RouterBlock broadcasts to all filters, each filter selectively passes items
graph.Connect(router, evenFilter);
graph.Connect(router, oddFilter);

// Result: Even numbers go through even filter, odd through odd filter
```

**How it works**:
1. Router broadcasts `RoutedItem<T>` to ALL downstream filters
2. Each filter receives ALL items but only passes items matching its route key
3. This is broadcast-and-filter, but appears selective from user perspective

**Performance Note**: The ADR document (`2025-11-03-routing-strategies.md`) notes this creates one record allocation per item, which could cause GC pressure at high volumes. Additionally, broadcasting to all filters is inefficient when routes are known at build time.

**Missing Feature**: Selective routing based on routing keys where each item is sent ONLY to the matching route without broadcasting. This would:
- Eliminate wasted CPU cycles filtering non-matching items
- Reduce memory pressure from broadcasting
- Improve performance for high-volume content-based routing

---

### 3. Structured Routing (Production Code Only - NOT in POC)

**Important**: This mechanism exists in `/src/DataFlow/Builder/Graph/` (production code), NOT in the POC codebase.

**Location**: `/src/DataFlow/Builder/Graph/StructuredRoutingBlockExtensions.cs` and documented in `/docs/structured-routing.md`

**⚠️ This is NOT part of the POC** - it's production code that may or may not apply to the POC architecture. The POC analysis should focus on mechanisms available in `/poc` folder only.

This provides a higher-level API for building complex routing pipelines with static and dynamic routes.

**Features**:
- Static routing (pre-registered routes)
- Dynamic routing (routes created on-demand from templates)
- Complex routes (routes can contain multiple blocks)
- DI scoping (each route gets its own scope)
- Safety limits (max dynamic routes)

**Example**:
```csharp
builder.AddRouter<Order>("router", order => order.Priority)
    .RegisterRoute("high", context => {
        // Build sub-dataflow for high-priority orders
        context.RouteBuilder
            .AddProcessor("urgent-processor", sp => new UrgentOrderProcessor())
            .AsEntry();
        return context.RouteBuilder;
    })
    .RegisterRoute("normal", context => {
        // Build sub-dataflow for normal orders
        context.RouteBuilder
            .AddBatch("batcher", maxBatchSize: 10)
            .AsEntry()
            .AddProcessor("batch-processor", sp => new BatchOrderProcessor());
        return context.RouteBuilder;
    })
    .ReceiveFrom("orders");
```

This is also broadcast-and-filter underneath, but provides a much more ergonomic API for complex scenarios.

---

### 4. Broadcast-and-Filter vs Selective Routing

The question from issue #75 was whether the POC needed selective routing or if broadcast-and-filter was sufficient.

**Current State**: **Only partially supported**

The POC has:
- ✅ Selective routing for **load balancing** (CompetingEdgeStrategy)
- ❌ NO selective routing for **content-based** routing (must use broadcast-and-filter)

| Approach | Mechanism | When to Use | Trade-offs |
|----------|-----------|-------------|------------|
| **Selective Load Balancing** | `CompetingEdgeStrategy` | Concurrent processing, load balancing, identical workers | ✅ No wasted work<br>✅ Natural load balancing<br>❌ Cannot inspect content<br>❌ Not suitable for routing keys |
| **Broadcast-and-Filter** | `RouterBlock` + `RouteFilterBlock` | Content-based routing (ONLY option in POC) | ❌ All filters see all items (concurrent broadcast)<br>❌ Record allocation per item<br>❌ Wasted CPU filtering<br>✅ Flexible routing logic |
| **Selective Content Routing** | **NOT IMPLEMENTED** | Would be ideal for routing keys | ✅ No wasted work<br>✅ No broadcasting overhead<br>✅ Efficient for many routes<br>⚠️ **Missing in POC** |

**Recommendation for Users**:

- **Use CompetingEdgeStrategy** when:
  - You want concurrent processing (multiple identical workers competing for items)
  - Items don't need content-based routing
  - You want natural load balancing
  - Performance is critical (no allocation overhead)
  - **Limitation**: Cannot route based on item content

- **Use RouterBlock + RouteFilterBlock** when:
  - Routing decision is based on item content (ONLY option for this in POC)
  - You need routing logic based on item properties
  - Routes are known at build time
  - Allocation overhead and broadcasting inefficiency are acceptable
  - **Limitation**: All filters receive all items (broadcast-and-filter)

- **Missing: Selective Content Routing** 
  - Would send each item ONLY to matching route based on content
  - No broadcasting, no filtering, no wasted work
  - **Not currently available in POC**

---

### 5. Broadcast Strategy Documentation
**Broadcasting is Concurrent**: The `BroadcastEdgeStrategy` writes to all target channels **concurrently** using `Task.WhenAll`:

```csharp
// From EdgeStrategy.cs - BroadcastEdgeStrategy.RouteTypedItemAsync
// Multiple writers: write concurrently to avoid serialization bottleneck
var writeTasks = new Task[typedWriters.Count];
// ... create tasks for all writers ...
await Task.WhenAll(writeTasks).ConfigureAwait(false);
```

This means:
- All target channels receive items **simultaneously** (not round-robin)
- Each consumer processes at its own pace (independent backpressure)
- No sequential delivery - all broadcasts happen in parallel

**Missing Documentation**: The POC lacks clear user guide documenting:
- Current broadcast strategy (concurrent vs sequential)
- When to use broadcast vs competing strategies
- How backpressure works with broadcasting
- Performance implications of concurrent broadcasting

---

### 6. Documentation Gaps

**Current State**:
- ✅ ADR exists: `poc/docs/adr/poc/2025-11-03-routing-strategies.md` (comprehensive)
- ✅ Design doc exists: `docs/structured-routing.md` (focused on structured routing API)
- ✅ Design decisions doc: `poc/docs/adr/poc/2025-11-03-routing-strategies.md` includes routing discussion
- ✅ Tests exist: `poc/DataFlow.POC.Tests/RoutingFlowTests.cs`, `EdgeStrategyTests.cs`
- ❌ **No consolidated routing guide** under `/poc/docs/guides/`

**What's Missing**:
A practical guide under `/poc/docs/guides/routing.md` that:
1. Explains the two POC routing mechanisms (Edge strategies and RouterBlock)
2. Clarifies Structured Routing is production code only
3. Provides decision tree for choosing between mechanisms
4. Shows practical examples for common scenarios
5. Links to ADR and design docs for deep dives
6. **Documents broadcast strategy** (concurrent broadcasting with Task.WhenAll)
7. **Explains the gap**: No selective content-based routing available
8. Includes performance considerations

---

### 7. Edge-Level vs Block-Level Routing

The POC strongly favors **edge-level routing** as the primary mechanism:

**Edge-Level Routing** (CompetingEdgeStrategy):
- ✅ Separates routing concerns from business logic
- ✅ Blocks remain simple and testable
- ✅ Graph has full visibility into topology
- ✅ Easy to change delivery semantics without touching blocks
- ✅ No boxing overhead (uses typed channels)

**Block-Level Routing** (RouterBlock):
- ⚠️ Routing logic embedded in blocks
- ⚠️ Creates intermediate `RoutedItem<T>` records
- ✅ More flexible for complex content-based routing
- ✅ Easier to understand for users familiar with TPL Dataflow

The ADR document explicitly states:
> "For dynamic routing support, **Option 1** better aligns with the POC's separation of concerns. However, it requires graph to support dynamic edge creation at runtime."

This suggests the architecture prefers edge-level routing but acknowledges block-level routing has its place for certain scenarios.

---

## Comparison Tables

### Routing Mechanism Comparison

| Feature | Edge Strategy | RouterBlock + Filter | Structured Routing |
|---------|--------------|---------------------|-------------------|
| **Selective Routing** | ✅ Yes (CompetingEdgeStrategy) | ❌ No (broadcast-filter) | ❌ No (broadcast-filter) |
| **Content-Based Routing** | ❌ No | ✅ Yes | ✅ Yes |
| **Dynamic Routes** | ❌ No | ❌ No | ✅ Yes (template-based) |
| **Allocation Overhead** | ✅ None | ⚠️ Record per item | ⚠️ Record per item |
| **Complexity** | Low | Medium | High |
| **Flexibility** | Low | High | Very High |
| **DI Scoping** | Block-level | Block-level | Route-level |
| **Best For** | Concurrency/load balancing | Simple content routing | Complex routing pipelines |

### Performance Characteristics

| Approach | Memory Allocation | CPU Overhead | Backpressure | Scalability |
|----------|------------------|--------------|--------------|-------------|
| **CompetingEdgeStrategy** | ✅ Zero extra allocation | ✅ Minimal (channel ops) | ✅ Natural (shared channel) | ✅ Excellent |
| **RouterBlock** | ⚠️ Record per item | ⚠️ Filter iteration | ✅ Per-filter | ⚠️ GC pressure at scale |
| **Structured Routing** | ⚠️ Record + route overhead | ⚠️ Route creation cost | ✅ Per-route | ⚠️ Memory per route |

---

## Outcome and Conclusions

### Is Issue #75 Still Relevant?

**Answer: YES - Issue #75 remains highly relevant.**

**Reasoning**:

1. **Selective content-based routing is NOT implemented** in the POC
   - CompetingEdgeStrategy only does load balancing (cannot inspect content)
   - RouterBlock uses inefficient broadcast-and-filter pattern
   - Missing: Ability to route items to specific targets based on content without broadcasting

2. **Current limitations**:
   - For content-based routing, must use broadcast-and-filter (inefficient)
   - All filters receive ALL items (concurrent broadcast using Task.WhenAll)
   - Wasted CPU cycles filtering non-matching items
   - Record allocation overhead per item

3. **The original concern from issue #75 is valid**:
   - Broadcast-and-filter IS inefficient for content-based routing
   - POC lacks selective routing based on routing keys or item content
   - This is different from load-balancing selective routing (CompetingEdgeStrategy)

### What Was the Original Concern?

Issue #75 raised the concern that broadcast-and-filter was inefficient for content-based routing because:
- Every filter receives every item (via concurrent broadcast)
- Wasted CPU cycles checking items that don't match
- Potential memory pressure from broadcasting and record allocation

**This concern is NOT fully addressed**:
- CompetingEdgeStrategy solves a different problem (load balancing, not content routing)
- RouterBlock still uses broadcast-and-filter with concurrent broadcasting
- Missing: Selective content-based routing that sends items ONLY to matching routes

### Missing Features Identified

1. **Selective Content-Based Routing**
   - Route items based on content (routing keys) without broadcasting
   - Send each item ONLY to the matching route
   - Eliminate filtering overhead
   - Example: Route by customer ID, product type, priority, etc.

2. **Broadcast Strategy Documentation**
   - Document that broadcasting is concurrent (Task.WhenAll)
   - Explain performance implications
   - Guide users on when to use broadcast vs competing strategies
   - Document backpressure behavior with broadcasting

### Recommended Next Steps

1. **Keep issue #75 open** or create new issue for selective content-based routing

2. **Create new feature**: Selective content-based routing mechanism
   - Edge strategy or block that routes based on content without broadcasting
   - Similar to RouterBlock but sends items ONLY to matching route
   - No broadcast, no filter, just direct routing

3. **Create routing guide** (`/poc/docs/guides/routing.md`) covering:
   - Overview of routing approaches in POC (2 mechanisms, not 3)
   - Clarify Structured Routing is production code only
   - Decision tree for choosing approach
   - **Document broadcast strategy** (concurrent)
   - Examples for each mechanism
   - Performance considerations
   - Migration guidance

4. **Update existing guides** to reference routing when relevant

5. **Consider benchmarking** RouterBlock broadcast-and-filter vs hypothetical selective routing:
   - Measure allocation overhead
   - Compare throughput with different route counts
   - Quantify waste from filtering
   - Document findings

6. **Update POC_GLOSSARY.md** with routing terminology

---

## Supporting Documents

### Existing Documentation

1. **ADR: Routing Strategies**
   - Path: `/poc/docs/adr/poc/2025-11-03-routing-strategies.md`
   - Status: Complete, comprehensive
   - Content: Architecture decisions, edge strategies, performance analysis

2. **Structured Routing Documentation**
   - Path: `/docs/structured-routing.md`
   - Status: Complete
   - Content: API documentation, examples, use cases

3. **Routing Unification Summary**
   - Path: `/ROUTING_UNIFICATION_SUMMARY.md`
   - Status: Complete
   - Content: Branch-based routing, migration guide

4. **Edge-First Architecture**
   - Path: `/poc/docs/design/edge-first-architecture.md`
   - Status: Complete
   - Content: Architecture diagrams, edge strategy explanation

### Tests

1. **EdgeStrategyTests.cs**: Tests for all edge strategies including CompetingEdgeStrategy
2. **RoutingFlowTests.cs**: Integration tests for RouterBlock + RouteFilterBlock
3. **Structured routing tests**: (referenced in unification summary)

### Code Locations

1. **Edge Strategies**: `poc/DataFlow.POC/Core/EdgeStrategy.cs`
2. **Router Blocks**: `poc/DataFlow.POC/Blocks/RouterBlock.cs`
3. **Edge**: `poc/DataFlow.POC/Core/Edge.cs`
4. **Tests**: `poc/DataFlow.POC.Tests/EdgeStrategyTests.cs`, `RoutingFlowTests.cs`

---

## Appendix A: Routing Decision Tree

```
Need routing?
│
├─ YES → Items need content-based routing?
│        │
│        ├─ YES → Routes are complex (multi-block)?
│        │        │
│        │        ├─ YES → Use Structured Routing
│        │        │        (AddRouter with RegisterRoute)
│        │        │
│        │        └─ NO → Use RouterBlock + RouteFilterBlock
│        │                (Simple content-based routing)
│        │
│        └─ NO → Need concurrent processing?
│                 │
│                 ├─ YES → Use CompetingEdgeStrategy
│                 │        (Load balancing, each item once)
│                 │
│                 └─ NO → Use BroadcastEdgeStrategy
│                         (All consumers get all items)
│
└─ NO → Use standard edges
        (Simple block-to-block connections)
```

---

## Appendix B: Example Scenarios

### Scenario 1: Load Balancing Work Items

**Requirement**: Process work items using 4 concurrent workers, each item processed exactly once.

**Solution**: CompetingEdgeStrategy
```csharp
var producer = /* ... */;
var worker1 = /* ... */;
var worker2 = /* ... */;
var worker3 = /* ... */;
var worker4 = /* ... */;

var competingEdge = new Edge(
    producer,
    new[] { worker1, worker2, worker3, worker4 },
    new CompetingEdgeStrategy(BufferMode.Bounded, 100));

graph.AddEdge(competingEdge);
```

**Why**: Selective routing, no wasted work, natural load balancing.

---

### Scenario 2: Route Orders by Priority

**Requirement**: High-priority orders go to express processor, normal orders go to batch processor.

**Solution**: RouterBlock + RouteFilterBlock
```csharp
var router = new RouterBlock<Order>("router", 
    order => order.Priority == "high" ? "express" : "normal");

var expressFilter = new RouteFilterBlock<Order>("express-filter", "express");
var normalFilter = new RouteFilterBlock<Order>("normal-filter", "normal");

var expressProcessor = /* ... */;
var normalProcessor = /* ... */;

graph.AddBlock(router)
    .AddBlock(expressFilter)
    .AddBlock(normalFilter)
    .AddBlock(expressProcessor)
    .AddBlock(normalProcessor)
    .Connect(router, expressFilter)
    .Connect(router, normalFilter)
    .Connect(expressFilter, expressProcessor)
    .Connect(normalFilter, normalProcessor);
```

**Why**: Content-based routing with simple logic.

---

### Scenario 3: Complex Dynamic Routing by Customer

**Requirement**: Each customer gets their own processing pipeline (transform → batch → save), routes created on-demand.

**Solution**: Structured Routing
```csharp
builder.AddRouter<Order>("router", order => order.CustomerId)
    .RegisterRoute("template", context => {
        var customerId = context.RouteName;
        
        return context.RouteBuilder
            .AddTransform("enricher", sp => new OrderEnricher(customerId))
            .AsEntry()
            .AddBatch("batcher", maxBatchSize: 50)
            .AddProcessor("saver", sp => new OrderSaver(customerId));
    })
    .WithDynamicRouting("template", maxDynamicRoutes: 1000)
    .ReceiveFrom("orders");
```

**Why**: Dynamic routes, complex pipelines, DI scoping per customer.

---

## Appendix C: Terminology

| Term | Definition | Example |
|------|------------|---------|
| **Selective Routing** | Each item goes to exactly one target | CompetingEdgeStrategy |
| **Broadcast Routing** | Each item goes to all targets | BroadcastEdgeStrategy |
| **Broadcast-and-Filter** | Broadcast to filters, each filter selects items | RouterBlock + RouteFilterBlock |
| **Competing Consumers** | Multiple consumers compete for items from shared source | CompetingEdgeStrategy |
| **Edge Strategy** | Delivery semantics defined at edge level | BroadcastEdgeStrategy, CompetingEdgeStrategy |
| **Content-Based Routing** | Routing decision based on item content | RouterBlock with selector function |
| **Dynamic Routing** | Routes created on-demand at runtime | Structured routing with templates |
| **Route Key** | Identifier used to determine item's route | String returned by route selector |

---

**Document Version**: 1.0  
**Author**: GitHub Copilot Coding Agent  
**Last Updated**: 2025-11-20  
**Status**: Complete
