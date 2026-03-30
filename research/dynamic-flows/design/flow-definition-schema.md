# Design: Flow Definition Schema

**Version**: 1.0  
**Date**: 2026-03-30  
**Status**: Proposed

---

## Overview

A flow definition is a serialized, versioned description of a DataFlow pipeline topology.
It captures **which blocks** participate and **how they are connected**, plus an optional
snapshot of per-block configuration at the time the version was saved.

---

## JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "FlowDefinition",
  "description": "A versioned, serializable DataFlow pipeline definition",
  "type": "object",
  "required": ["flowId", "version", "name", "blocks", "connections"],
  "properties": {
    "flowId": {
      "type": "string",
      "description": "Stable identifier for this flow (survives version bumps). Use kebab-case.",
      "examples": ["invoice-processing-pipeline", "data-ingestion-v2"]
    },
    "version": {
      "type": "integer",
      "minimum": 1,
      "description": "Monotonically increasing version number. Increment when topology or config changes."
    },
    "name": {
      "type": "string",
      "description": "Human-readable display name shown in the UI."
    },
    "blocks": {
      "type": "array",
      "description": "The blocks that participate in this flow.",
      "items": {
        "type": "object",
        "required": ["instanceId", "blockKey"],
        "properties": {
          "instanceId": {
            "type": "string",
            "description": "Unique ID for this block within the flow. Used as source/target in connections."
          },
          "blockKey": {
            "type": "string",
            "description": "The registered key of the block type. Must exist in IBlockTypeRegistry.",
            "examples": ["global:producer", "global:transform", "invoices:validator"]
          }
        }
      }
    },
    "connections": {
      "type": "array",
      "description": "Directed edges connecting blocks.",
      "items": {
        "type": "object",
        "required": ["from", "to"],
        "properties": {
          "from": {
            "type": "string",
            "description": "Source block instanceId."
          },
          "to": {
            "type": "string",
            "description": "Target block instanceId."
          },
          "bufferCapacity": {
            "type": "integer",
            "minimum": 1,
            "default": 100,
            "description": "Bounded channel capacity. Controls backpressure."
          }
        }
      }
    },
    "blockConfig": {
      "type": "object",
      "description": "Per-block configuration snapshots, keyed by instanceId. Each value is block-specific JSON.",
      "additionalProperties": {
        "type": "object",
        "description": "Block-specific options. Schema defined by each block type."
      }
    }
  }
}
```

---

## Format Decision: JSON over YAML

| Factor | JSON | YAML |
|--------|------|------|
| .NET built-in support | ✅ `System.Text.Json` | ❌ requires `YamlDotNet` |
| Machine-generated | ✅ natural | ⚠️ possible but verbose |
| Human readability | ✅ adequate | ✅ slightly better |
| Schema validation | ✅ JSON Schema | ✅ JSON Schema (after conversion) |
| Designer output | ✅ trivially serializable | ✅ but extra step |
| API transport | ✅ native JSON API payload | ❌ needs conversion |

**Decision**: JSON for v1. YAML can be added as an optional deserialization layer later.

---

## Version Strategy

A `FlowDefinition` is **immutable once published**.  
Changes (topology or config) create a new version.

```
flowId: "invoice-processor"  version: 1  →  Running in production
flowId: "invoice-processor"  version: 2  →  Draft / testing
```

**Why immutable versions?**
- Auditing: know exactly what ran for each invocation
- Rollback: revert to v1 without losing v2
- Debugging: historical runs always reference the exact config they used

**Recommendation**: Store definitions as append-only records.

---

## Aliasing (Multiple Instances of Same Block Type)

For v1, `instanceId == blockKey` is the simplest approach.

For v2+, if users want two transform blocks in one flow:
```json
{
  "blocks": [
    { "instanceId": "step1-transform", "blockKey": "global:transform" },
    { "instanceId": "step2-transform", "blockKey": "global:transform" }
  ]
}
```

This requires the `DynamicFlowBuilder` to create distinct `IBlock` wrappers with different `Name` properties (since `DataFlowGraph` requires unique block names). The implementation should:
1. Resolve the block from DI by `blockKey`
2. Wrap it in a `NamedBlockWrapper(innerBlock, instanceId)` that overrides `.Name`

This is a v2 feature — tracked in the implementation issue.

---

## Graph Topology Constraints

The following constraints apply (same as hardcoded flows):
- No cycles (execution would deadlock)
- Exactly one source block (no incoming edges) per connected component  
- At least one sink block (no outgoing edges)
- Type compatibility: `source.OutputType` must be assignable to `target.InputType`

The `DynamicFlowBuilder.Validate()` method enforces the type compatibility constraint.
Cycle detection and source/sink validation are deferred to the `DataFlowGraphBuilder.Build()` 
call which throws on invalid topologies.
