# Routing Mechanisms: Detailed Comparison Tables

**Date**: 2025-11-20  
**Status**: Updated to reflect SelectiveRoutingEdgeStrategy  
**Related**: [Analysis: POC Routing Behavior](./README.md)

This document provides detailed comparison tables for the routing mechanisms available in the POC.

---

## Table of Contents

1. [Feature Comparison](#feature-comparison)
2. [Performance Characteristics](#performance-characteristics)
3. [Use Case Matrix](#use-case-matrix)
4. [API Complexity](#api-complexity)
5. [Delivery Semantics](#delivery-semantics)
6. [Scalability Analysis](#scalability-analysis)

---

## Feature Comparison

### Core Capabilities

**Note**: Structured Routing exists only in production code (`/src`), not in POC (`/poc`).

| Feature | SelectiveRoutingEdgeStrategy | CompetingEdgeStrategy | BroadcastEdgeStrategy | Structured Routing* |
|---------|------------------------------|----------------------|----------------------|-------------------|
| **Selective Content Routing** | ✅ Yes (optimal) | ❌ No | ❌ No | ❌ No (broadcast-filter) |
| **Selective Load Balancing** | ❌ No | ✅ Yes | ❌ No | ❌ No |
| **Broadcast Routing** | ❌ No | ❌ No | ✅ Yes (concurrent) | ✅ Yes (concurrent) |
| **Content-Based Routing** | ✅ Yes (optimal) | ❌ No | ❌ No | ✅ Yes (inefficient) |
| **Static Routes** | ✅ Yes | N/A | N/A | ✅ Yes |
| **Dynamic Routes** | ❌ No | N/A | N/A | ✅ Yes (template-based) |
| **Multi-Block Routes** | ⚠️ Manual | N/A | N/A | ✅ Yes (built-in) |
| **Route Isolation** | ⚠️ Manual | N/A | N/A | ✅ Yes (DI scopes) |
| **Complex Routing Logic** | ✅ Yes (via selector) | ❌ No | ❌ No | ✅ Yes |
| **Type Safety** | ✅ Compile-time | ✅ Compile-time | ✅ Compile-time | ✅ Compile-time |
| **Route Limits** | ⚠️ Manual | N/A | N/A | ✅ Yes (configurable) |

*Structured Routing is **production code only** (`/src/DataFlow/Builder/Graph/`), not part of POC.

### Architectural Characteristics

| Characteristic | SelectiveRoutingEdgeStrategy | CompetingEdgeStrategy | BroadcastEdgeStrategy | Structured Routing |
|----------------|------------------------------|----------------------|----------------------|-------------------|
| **Separation of Concerns** | ✅ Excellent | ✅ Excellent | ✅ Excellent | ✅ Excellent |
| **Testability** | ✅ High | ✅ High | ✅ High | ⚠️ Moderate |
| **Composability** | ✅ High | ✅ High | ✅ High | ✅ High |
| **Graph Visibility** | ✅ Full | ✅ Full | ✅ Full | ⚠️ Routes hidden |
| **Learning Curve** | ⚠️ Moderate | ✅ Low | ✅ Low | ⚠️ Moderate |
| **Code Maintainability** | ✅ High | ✅ High | ✅ High | ✅ High |
| **Error Handling** | ✅ Simple | ✅ Simple | ✅ Simple | ⚠️ Complex |

---

## Performance Characteristics

### Memory and CPU

| Metric | SelectiveRoutingEdgeStrategy | CompetingEdgeStrategy | BroadcastEdgeStrategy | Structured Routing |
|--------|------------------------------|----------------------|----------------------|-------------------|
| **Allocations per Item** | 0 | 0 | 0 | 1+ (RoutedItem + route overhead) |
| **GC Pressure** | ✅ None | ✅ None | ✅ None | ⚠️ High (records + routes) |
| **CPU per Item** | ✅ Minimal (O(1) lookup + write) | ✅ Minimal (channel write) | ⚠️ Moderate (N concurrent writes) | ⚠️ High (route lookup + checks) |
| **Channel Overhead** | ✅ N channels (1 per route) | ✅ 1 shared channel | ⚠️ N channels (N=targets) | ⚠️ N channels (N=routes) |
| **Boxing/Unboxing** | ✅ None (typed channels) | ✅ None (typed channels) | ✅ None (typed channels) | ✅ None (typed channels) |

### Throughput Estimates (Items/Second)

*Note: These are estimated ranges based on architectural analysis. Actual benchmarking is recommended.*

| Load Pattern | SelectiveRoutingEdgeStrategy | CompetingEdgeStrategy | BroadcastEdgeStrategy | Structured Routing |
|--------------|------------------------------|----------------------|----------------------|-------------------|
| **Light (100 items/sec)** | ~100 (optimal) | ~100 (no bottleneck) | ~95 (broadcast overhead) | ~90 (route overhead) |
| **Medium (10K items/sec)** | ~10K (optimal) | ~10K (no bottleneck) | ~9K (broadcast overhead) | ~7K (GC + route lookup) |
| **High (100K items/sec)** | ~100K (near optimal) | ~100K (channel limit) | ~80K (broadcast overhead) | ~40K (GC + overhead) |
| **Very High (1M items/sec)** | ~900K (optimal scaling) | ~500K (channel contention) | ⚠️ Broadcast overhead | ⚠️ GC thrashing + memory |

**Legend**:
- ✅ Green: Handles well
- ⚠️ Yellow: Performance degradation
- ❌ Red: Not recommended

### Backpressure Behavior

| Scenario | SelectiveRoutingEdgeStrategy | CompetingEdgeStrategy | BroadcastEdgeStrategy | Structured Routing |
|----------|------------------------------|----------------------|----------------------|-------------------|
| **Slow Consumer** | ✅ Per-route backpressure | ✅ Shared channel blocks | ✅ Per-target backpressure | ✅ Per-route backpressure |
| **One Slow, Others Fast** | ✅ Independent | ⚠️ All affected (shared) | ✅ Independent | ✅ Independent |
| **Backpressure Propagation** | ✅ Immediate | ✅ Immediate | ✅ Immediate | ✅ Immediate |
| **Memory Bounded** | ✅ Yes (per-route capacity) | ✅ Yes (channel capacity) | ✅ Yes (per-target capacity) | ✅ Yes (per-route capacity) |

---

## Use Case Matrix

### When to Use Each Mechanism (POC)

**Note**: SelectiveRoutingEdgeStrategy, CompetingEdgeStrategy, and BroadcastEdgeStrategy are available in POC. Structured Routing is production code only.

| Use Case | Recommended Approach | Why? |
|----------|---------------------|------|
| **Load balancing identical workers** | CompetingEdgeStrategy | Zero overhead, natural load balancing |
| **Concurrent processing (N workers)** | CompetingEdgeStrategy | Simplest, most efficient |
| **Route by item property (2-5 routes)** | SelectiveRoutingEdgeStrategy | Optimal - zero overhead, O(1) lookup |
| **Route by complex business logic** | SelectiveRoutingEdgeStrategy | Flexible selector function |
| **High-throughput content routing** | SelectiveRoutingEdgeStrategy | Zero allocation, optimal performance |
| **Dynamic routes (per customer/tenant)** | ⚠️ **Not available in POC** | Use production Structured Routing |
| **Routes with multi-step pipelines** | ⚠️ **Not available in POC** | Use production Structured Routing |
| **Routes needing DI scoping** | ⚠️ **Not available in POC** | Use production Structured Routing |
| **Fan-out to monitoring/logging** | BroadcastEdgeStrategy | All consumers need all items |

### Anti-Patterns

| ❌ Don't Do This | ✅ Do This Instead | Why? |
|------------------|-------------------|------|
| Use BroadcastEdgeStrategy for content routing | Use SelectiveRoutingEdgeStrategy | Avoid wasted broadcasts and CPU |
| Use CompetingEdgeStrategy for content routing | Use SelectiveRoutingEdgeStrategy | Can't inspect content at edge level |
| Create 100s of static routes | Use dynamic routing | Code bloat, maintenance nightmare |
| Use Structured Routing for simple 2-way split | Use SelectiveRoutingEdgeStrategy | Over-engineering |
| ~~Use RouterBlock + RouteFilterBlock~~ | Use SelectiveRoutingEdgeStrategy | RouterBlock removed - obsolete approach |

---

## API Complexity

### Lines of Code Comparison

**Scenario**: Route integers by even/odd to different processors.

#### SelectiveRoutingEdgeStrategy (8 lines)

```csharp
var routeMapping = new Dictionary<string, IBlock>
{
    ["even"] = evenProcessor,
    ["odd"] = oddProcessor
};

var strategy = new SelectiveRoutingEdgeStrategy<int>(
    routeKeyToBlock: routeMapping,
    routeSelector: i => i % 2 == 0 ? "even" : "odd");

var edge = new Edge(producer, new[] { evenProcessor, oddProcessor }, strategy);
builder.AddEdge(edge);
```

#### CompetingEdgeStrategy (N/A - doesn't support content-based routing)

```csharp
// Not applicable - CompetingEdgeStrategy doesn't inspect content
// Would need upstream block to separate even/odd first
```

#### ~~RouterBlock + Filter~~ ❌ REMOVED (was 12 lines)

```csharp
// This approach has been removed - use SelectiveRoutingEdgeStrategy instead
```

#### Structured Routing (15 lines)

```csharp
builder.AddRouter<int>("router", i => i % 2 == 0 ? "even" : "odd")
    .RegisterRoute("even", context => {
        return context.RouteBuilder
            .AddProcessor("even-proc", sp => new EvenProcessor())
            .AsEntry();
    })
    .RegisterRoute("odd", context => {
        return context.RouteBuilder
            .AddProcessor("odd-proc", sp => new OddProcessor())
            .AsEntry();
    })
    .ReceiveFrom("source");

var flow = builder.Build();
```

**Verdict**: 
- SelectiveRoutingEdgeStrategy is most concise and performant for simple routing
- Structured Routing pays off when routes are complex or dynamic

---

## Delivery Semantics

### Item Delivery Guarantees

| Guarantee | SelectiveRoutingEdgeStrategy | CompetingEdgeStrategy | BroadcastEdgeStrategy | Structured Routing |
|-----------|------------------------------|----------------------|----------------------|-------------------|
| **Each item delivered at least once** | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes |
| **Each item delivered at most once** | ✅ Yes (per route) | ✅ Yes (per target) | ✅ Yes (per target) | ✅ Yes (per route) |
| **Exactly-once semantics** | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes |
| **Order preservation** | ⚠️ Per-route yes | ⚠️ No (competing) | ⚠️ Per-target yes | ⚠️ Per-route yes |
| **All targets see all items** | ❌ No (selective) | ❌ No (competing) | ✅ Yes (broadcast) | ❌ No (filtered) |

### Concurrency Model

| Aspect | SelectiveRoutingEdgeStrategy | CompetingEdgeStrategy | BroadcastEdgeStrategy | Structured Routing |
|--------|------------------------------|----------------------|----------------------|-------------------|
| **Target Concurrency** | ✅ All routes run concurrently | ✅ All targets run concurrently | ✅ All targets run concurrently | ✅ All routes run concurrently |
| **Item Distribution** | ✅ By content (selective) | ✅ Dynamic (competing) | ❌ All get all | ✅ By content (filtered) |
| **Load Balancing** | ⚠️ By route distribution | ✅ Automatic | ❌ None | ⚠️ By route distribution |
| **Worker Isolation** | ✅ Perfect | ✅ Perfect | ✅ Perfect (+ DI scopes) |

---

## Scalability Analysis

### Scaling Dimensions

| Dimension | CompetingEdgeStrategy | RouterBlock + Filter | Structured Routing |
|-----------|----------------------|---------------------|-------------------|
| **Item Volume** | ✅ Excellent (100K+/sec) | ⚠️ Moderate (10K-50K/sec) | ⚠️ Moderate (10K-50K/sec) |
| **Number of Routes** | ✅ N/A (not route-based) | ⚠️ Linear degradation | ⚠️ Memory per route |
| **Route Complexity** | ✅ N/A | ✅ No impact | ⚠️ Moderate impact |
| **Concurrent Workers** | ✅ Excellent (add more blocks) | ⚠️ One per route | ⚠️ One per route |
| **Dynamic Route Creation** | ✅ N/A | ❌ Not supported | ⚠️ Memory + lookup cost |

### Resource Utilization

| Resource | CompetingEdgeStrategy | RouterBlock + Filter | Structured Routing |
|----------|----------------------|---------------------|-------------------|
| **CPU** | ✅ Low (channel ops) | ⚠️ Medium (filter checks) | ⚠️ High (route lookup + checks) |
| **Memory** | ✅ Low (1 channel) | ⚠️ Medium (N channels) | ⚠️ High (N routes + scopes) |
| **Threads** | ✅ Per worker block | ✅ Per filter + processor | ⚠️ Per route block |
| **GC** | ✅ Minimal | ⚠️ Record allocations | ⚠️ Records + route objects |

### Breaking Points

| Mechanism | Memory Breaking Point | CPU Breaking Point | Recommended Limit |
|-----------|----------------------|-------------------|-------------------|
| **CompetingEdgeStrategy** | ~1M items in channel | Channel contention | 100-1000 workers |
| **RouterBlock** | ~1M items * N filters | Filter iteration | 10-100 routes |
| **Structured Routing** | Route objects * items | Route lookup + filter | 100-10000 dynamic routes |

---

## Decision Matrix

### Quick Reference Guide

Use this matrix to quickly determine which routing mechanism to use:

| Your Requirements | Recommended Approach |
|-------------------|---------------------|
| ✅ Need load balancing | CompetingEdgeStrategy |
| ✅ Need concurrent processing | CompetingEdgeStrategy |
| ✅ Content-based routing, <10 routes | RouterBlock + Filter |
| ✅ Content-based routing, 10-100 routes | RouterBlock + Filter |
| ✅ Dynamic routes (runtime creation) | Structured Routing |
| ✅ Multi-block routes | Structured Routing |
| ✅ Route-level DI scoping | Structured Routing |
| ✅ High throughput (>50K items/sec) | CompetingEdgeStrategy |
| ✅ Complex routing logic | RouterBlock or Structured Routing |
| ✅ Need all consumers to see all items | BroadcastEdgeStrategy |

### Hybrid Approaches

You can combine mechanisms for complex scenarios:

```csharp
// Example: Content-based routing THEN load balancing
// Step 1: Route by content
var router = new RouterBlock<Order>("router", 
    o => o.Priority == "high" ? "express" : "normal");

// Step 2: Load balance within each route using CompetingEdgeStrategy
var expressWorker1 = /* ... */;
var expressWorker2 = /* ... */;
var expressWorker3 = /* ... */;

var normalWorker1 = /* ... */;
var normalWorker2 = /* ... */;

// Express route: 3 competing workers
var expressEdge = new Edge(
    expressFilter,
    new[] { expressWorker1, expressWorker2, expressWorker3 },
    new CompetingEdgeStrategy());

// Normal route: 2 competing workers
var normalEdge = new Edge(
    normalFilter,
    new[] { normalWorker1, normalWorker2 },
    new CompetingEdgeStrategy());
```

---

**Document Version**: 1.0  
**Last Updated**: 2025-11-20  
**Status**: Complete
