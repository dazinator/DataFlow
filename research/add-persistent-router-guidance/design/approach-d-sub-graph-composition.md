# Approach D: Sub-Graph Composition (Flow Factory)

**Category**: Advanced composability, future library extension  
**Effort**: High  
**POC library changes required**: Yes — requires new `ISubGraphFactory` abstraction

---

## Overview

Define a reusable "sub-graph template" that represents a complete per-system pipeline. At runtime,
a **flow factory actor** instantiates new sub-graph instances on demand when a new route key is
encountered.

This is the closest conceptual equivalent to the legacy `AddPersistentRouter`, where a full
sub-flow builder was invoked recursively for each distinct route key.

---

## Conceptual Design

```
[Routing Transformer]
      ↓
[FlowFactoryActor]
  ┌────────────────────────────────────────────────────────────┐
  │  On "SAP-Tenant1" first seen:                              │
  │    Instantiate SubGraph("SAP-Tenant1"):                    │
  │      [SapSender(SAP-Tenant1)] → [TmsBatcher] → [TmsSender]│
  │                                                            │
  │  On "Oracle-T1" first seen:                               │
  │    Instantiate SubGraph("Oracle-T1"):                      │
  │      [OracleSender(Oracle-T1)] → [TmsBatcher] → [TmsSender]│
  └────────────────────────────────────────────────────────────┘
```

---

## Why This Approach Is Not Recommended Now

### Significant Library Investment Required

This approach requires:

1. A `ISubGraphTemplate<TIn, TOut>` interface for describing reusable sub-graph shapes.
2. A `ISubGraphFactory` that can instantiate a sub-graph from a template + a key.
3. Graph lifecycle management: starting, stopping, and draining sub-graphs created at runtime.
4. Backpressure propagation across sub-graph boundaries.
5. Metrics and visualization integration for dynamic sub-graphs.

None of these exist in the current POC. The investment is equivalent to building a significant
new library feature.

### The Problem Can Be Solved Without It

Approaches A, B, and C address the vast majority of migration scenarios without requiring new
library primitives. The sub-graph composition approach would only be preferred when:

- The sub-graph is complex (many blocks, multiple branches).
- The sub-graph must be independently observable and visualizable.
- Sub-graphs need independent lifecycle management (start/stop independently).

For the journal processing migration use case, Approach C covers the requirement.

---

## Future Design Direction

If this feature becomes a priority, a possible design:

```csharp
/// <summary>
/// Template for a reusable sub-graph structure.
/// </summary>
public interface ISubGraphTemplate<TIn, TOut>
{
    /// <summary>
    /// Build a sub-graph instance for the given route key.
    /// The key allows per-instance configuration (e.g., SAP system name).
    /// </summary>
    DataFlowGraph Build(string routeKey, IServiceProvider serviceProvider);
}

/// <summary>
/// Actor that instantiates and dispatches to sub-graphs on demand.
/// </summary>
public class FlowFactoryActor<TIn, TOut> : IStreamActor<TIn, TOut>
{
    private readonly ISubGraphTemplate<TIn, TOut> _template;
    private readonly Func<TIn, string> _routeKeySelector;
    private readonly ConcurrentDictionary<string, DataFlowGraph> _subGraphs = new();

    // ... instantiate sub-graph on GetOrAdd, route item to input channel
}
```

This would be a **new research topic** if prioritized.

---

## When to Consider Approach D

- Approaches A, B, C have been tried and found insufficient for a specific use case.
- The per-route sub-graph is complex (> 3 blocks, multiple branches).
- Sub-graphs need independent lifecycle management.
- The team is ready to invest in extending the POC library.

**Recommendation**: File a separate research issue to design this feature if needed.

---

## References

- Legacy `AddPersistentRouter` in the existing codebase — this approach is its direct equivalent.
- `/research/flow-composability-unification/` — related research on composing epoch and non-epoch streams.
