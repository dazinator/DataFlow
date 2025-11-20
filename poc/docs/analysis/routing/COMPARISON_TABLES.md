# Routing Mechanisms: Detailed Comparison Tables

**Date**: 2025-11-20  
**Related**: [Analysis: POC Routing Behavior](./README.md)

This document provides detailed comparison tables for the three routing mechanisms available in the POC.

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

| Feature | CompetingEdgeStrategy | RouterBlock + Filter | Structured Routing* |
|---------|----------------------|---------------------|-------------------|
| **Selective Load Balancing** | ✅ Yes | ❌ No | ❌ No |
| **Selective Content Routing** | ❌ No | ❌ No (broadcast-filter) | ❌ No (broadcast-filter) |
| **Broadcast Routing** | ❌ No | ✅ Yes (concurrent) | ✅ Yes (concurrent) |
| **Content-Based Routing** | ❌ No | ✅ Yes (inefficient) | ✅ Yes (inefficient) |
| **Static Routes** | N/A | ✅ Yes | ✅ Yes |
| **Dynamic Routes** | N/A | ❌ No | ✅ Yes (template-based) |
| **Multi-Block Routes** | N/A | ⚠️ Manual | ✅ Yes (built-in) |
| **Route Isolation** | N/A | ❌ No | ✅ Yes (DI scopes) |
| **Complex Routing Logic** | ❌ No | ✅ Yes | ✅ Yes |
| **Type Safety** | ✅ Compile-time | ✅ Compile-time | ✅ Compile-time |
| **Route Limits** | N/A | N/A | ✅ Yes (configurable) |

*Structured Routing is **production code only** (`/src/DataFlow/Builder/Graph/`), not part of POC.

### Architectural Characteristics

| Characteristic | CompetingEdgeStrategy | RouterBlock + Filter | Structured Routing |
|----------------|----------------------|---------------------|-------------------|
| **Separation of Concerns** | ✅ Excellent | ⚠️ Moderate | ✅ Excellent |
| **Testability** | ✅ High | ✅ High | ⚠️ Moderate |
| **Composability** | ✅ High | ✅ High | ✅ High |
| **Graph Visibility** | ✅ Full | ✅ Full | ⚠️ Routes hidden |
| **Learning Curve** | ✅ Low | ✅ Low | ⚠️ Moderate |
| **Code Maintainability** | ✅ High | ✅ High | ✅ High |
| **Error Handling** | ✅ Simple | ✅ Simple | ⚠️ Complex |

---

## Performance Characteristics

### Memory and CPU

| Metric | CompetingEdgeStrategy | RouterBlock + Filter | Structured Routing |
|--------|----------------------|---------------------|-------------------|
| **Allocations per Item** | 0 | 1 (RoutedItem record) | 1+ (RoutedItem + route overhead) |
| **GC Pressure** | ✅ None | ⚠️ Moderate (record alloc) | ⚠️ High (records + routes) |
| **CPU per Item** | ✅ Minimal (channel write) | ⚠️ Moderate (filter checks) | ⚠️ High (route lookup + checks) |
| **Channel Overhead** | ✅ 1 shared channel | ⚠️ N channels (N=filters) | ⚠️ N channels (N=routes) |
| **Boxing/Unboxing** | ✅ None (typed channels) | ✅ None (typed channels) | ✅ None (typed channels) |

### Throughput Estimates (Items/Second)

*Note: These are estimated ranges based on architectural analysis. Actual benchmarking is recommended.*

| Load Pattern | CompetingEdgeStrategy | RouterBlock + Filter | Structured Routing |
|--------------|----------------------|---------------------|-------------------|
| **Light (100 items/sec)** | ~100 (no bottleneck) | ~95 (filter overhead) | ~90 (route overhead) |
| **Medium (10K items/sec)** | ~10K (no bottleneck) | ~8K (GC starts) | ~7K (GC + route lookup) |
| **High (100K items/sec)** | ~100K (channel limit) | ~50K (GC pressure) | ~40K (GC + overhead) |
| **Very High (1M items/sec)** | ~500K (channel contention) | ⚠️ GC thrashing | ⚠️ GC thrashing + memory |

**Legend**:
- ✅ Green: Handles well
- ⚠️ Yellow: Performance degradation
- ❌ Red: Not recommended

### Backpressure Behavior

| Scenario | CompetingEdgeStrategy | RouterBlock + Filter | Structured Routing |
|----------|----------------------|---------------------|-------------------|
| **Slow Consumer** | ✅ Shared channel blocks | ✅ Per-filter backpressure | ✅ Per-route backpressure |
| **One Slow, Others Fast** | ⚠️ All affected (shared) | ✅ Independent | ✅ Independent |
| **Backpressure Propagation** | ✅ Immediate | ✅ Immediate | ✅ Immediate |
| **Memory Bounded** | ✅ Yes (channel capacity) | ✅ Yes (per-filter capacity) | ✅ Yes (per-route capacity) |

---

## Use Case Matrix

### When to Use Each Mechanism (POC)

**Note**: Only CompetingEdgeStrategy and RouterBlock+Filter are available in POC. Structured Routing is production code only.

| Use Case | Recommended Approach | Why? |
|----------|---------------------|------|
| **Load balancing identical workers** | CompetingEdgeStrategy | Zero overhead, natural load balancing |
| **Concurrent processing (N workers)** | CompetingEdgeStrategy | Simplest, most efficient |
| **Route by item property (2-5 routes)** | RouterBlock + Filter | Only option for content routing (inefficient) |
| **Route by complex business logic** | RouterBlock + Filter | Only option for content routing (inefficient) |
| **Dynamic routes (per customer/tenant)** | ⚠️ **Not available in POC** | Use production Structured Routing |
| **Routes with multi-step pipelines** | ⚠️ **Not available in POC** | Use production Structured Routing |
| **Routes needing DI scoping** | ⚠️ **Not available in POC** | Use production Structured Routing |
| **High-throughput content routing** | ⚠️ **Missing feature** | Would need selective content routing |
| **Fan-out to monitoring/logging** | BroadcastEdgeStrategy | All consumers need all items |

### Anti-Patterns

| ❌ Don't Do This | ✅ Do This Instead | Why? |
|------------------|-------------------|------|
| Use RouterBlock for load balancing | Use CompetingEdgeStrategy | Avoid unnecessary allocation |
| Use CompetingEdgeStrategy for content routing | Use RouterBlock | Can't inspect content at edge level |
| Create 100s of static routes | Use dynamic routing | Code bloat, maintenance nightmare |
| Use Structured Routing for simple 2-way split | Use RouterBlock | Over-engineering |
| Broadcast to filters that drop 99% of items | Redesign to use selective routing | Wasted CPU and memory |

---

## API Complexity

### Lines of Code Comparison

**Scenario**: Route integers by even/odd to different processors.

#### CompetingEdgeStrategy (N/A - doesn't support content-based routing)

```csharp
// Not applicable - CompetingEdgeStrategy doesn't inspect content
// Would need upstream block to separate even/odd first
```

#### RouterBlock + Filter (12 lines)

```csharp
var router = new RouterBlock<int>("router", i => i % 2 == 0 ? "even" : "odd");
var evenFilter = new RouteFilterBlock<int>("even-filter", "even");
var oddFilter = new RouteFilterBlock<int>("odd-filter", "odd");

var evenProc = /* processor */;
var oddProc = /* processor */;

builder.AddBlock(router)
    .AddBlock(evenFilter).AddBlock(oddFilter)
    .AddBlock(evenProc).AddBlock(oddProc)
    .Connect(router, evenFilter).Connect(router, oddFilter)
    .Connect(evenFilter, evenProc).Connect(oddFilter, oddProc);
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
- RouterBlock is most concise for simple routing
- Structured Routing pays off when routes are complex

---

## Delivery Semantics

### Item Delivery Guarantees

| Guarantee | CompetingEdgeStrategy | RouterBlock + Filter | Structured Routing |
|-----------|----------------------|---------------------|-------------------|
| **Each item delivered at least once** | ✅ Yes | ✅ Yes | ✅ Yes |
| **Each item delivered at most once** | ✅ Yes (per target) | ✅ Yes (per route) | ✅ Yes (per route) |
| **Exactly-once semantics** | ✅ Yes | ✅ Yes | ✅ Yes |
| **Order preservation** | ⚠️ No (competing) | ⚠️ No (per-filter yes) | ⚠️ No (per-route yes) |
| **All targets see all items** | ❌ No | ✅ Yes (then filtered) | ✅ Yes (then filtered) |

### Concurrency Model

| Aspect | CompetingEdgeStrategy | RouterBlock + Filter | Structured Routing |
|--------|----------------------|---------------------|-------------------|
| **Target Concurrency** | ✅ All targets run concurrently | ✅ All filters run concurrently | ✅ All routes run concurrently |
| **Item Distribution** | ✅ Dynamic (competing) | ❌ All get all | ❌ All get all |
| **Load Balancing** | ✅ Automatic | ❌ None | ❌ None |
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
