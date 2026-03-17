# Exploration Notes: AddPersistentRouter Research

**Date**: 2026-03-17  
**Researcher**: GitHub Copilot (Research Duty)

---

## Codebase Exploration

### Existing Routing Infrastructure

**`SelectiveRoutingEdgeStrategy<TItem>`** exists at:
```
/poc/DataFlow/Core/SelectiveRoutingEdgeStrategy.cs
```

Key properties:
- Takes a `Dictionary<string, IBlock>` (route key → target block) at construction time.
- Takes a `Func<TItem, string>` selector function.
- Routes items via dictionary lookup (O(1)).
- Throws `InvalidOperationException` for unknown route keys.
- ⚠️ Routes are **immutable after construction** — cannot add new routes at runtime.

**`DataFlowGraphBuilder`** at:
```
/poc/DataFlow/Builder/DataFlowGraphBuilder.cs
```

Key observations:
- `Build()` is called once at startup.
- `_blocks`, `_edges` lists are populated during registration and finalized on `Build()`.
- No API for mutating the graph after `Build()` is called.
- No concept of "sub-graph" or "flow factory".

**Topology guide notes** (`/poc/docs/guides/topology-selective-routing.md` line 74):
> Routes must be **defined at build time** (not dynamic in POC)

This confirms the architectural constraint is documented and intentional.

---

## Analysis of the Journal Processing Requirements

The legacy flow uses `AddPersistentRouter` to create per-`System.Name` sub-flows. Looking at the
assessment document carefully:

### What "System" means in this context

The routing transformer (`JournalRoutingTransformer`) emits one `SystemJournalProcessingItems`
per **distinct system** per batch. The `System` property is a fully-resolved system entity.

In the legacy flow, the router keyed on `item.System.Name` which produced values like:
- `"unrouted"` — no system found for company code
- `"SAP"` or `"SapS4Hana"` — SAP BTP connector (all SAP tenants share this key!)
- Potentially other ERP type names

### Key Insight: The route key is the ERP TYPE, not the tenant instance

Looking at `Block 7` description:
> "SapJournalSender | Transform (route) | `SapBtpConnectorErpJournalSender` | Instantiated with
> systemName at route-creation time."

The `systemName` was passed as a constructor arg. But the route key was `"SAP"` or `"SapS4Hana"`.
This means: **one sub-flow per ERP type, not per tenant**.

The "dynamic" nature was about which ERP types appear in a given batch — but the possible set
was bounded by the ERP types supported by the application (SAP, maybe Oracle in future).

The claim that "target systems can be added at runtime" refers to adding new tenant instances of
an existing ERP type — which, in the legacy model, reuses the same sub-flow (same route key).

### Conclusion

The dynamic routing requirement is actually:
1. The route KEY SET is small (< 5 ERP types) and bounded by supported integrations.
2. New "routes" in the legacy sense = new tenant instances → same route key, different config.
3. The persistence of the router was to avoid recreating the sub-flow for each item in the stream.

This strongly supports **Approach C** as the correct migration.

---

## Investigation of `SapBtpConnectorErpJournalSender` Instantiation

From the assessment:
> "Instantiated with systemName at route-creation time via `ActivatorUtilities.CreateInstance(sp, systemName)`"

This pattern was used because the legacy router created one sub-flow per route key, and needed
a way to parameterize the block with the route key. In the POC model:

**Alternative**: The item already carries `item.System` — the handler can read `item.System.Name`
directly rather than having it injected at construction time. This eliminates the need for the
`ActivatorUtilities.CreateInstance` workaround entirely.

---

## Prototype Validation Plan

To validate Approach C, the prototype should demonstrate:
1. A single actor block that receives items with a `string SystemKey` property.
2. The actor resolves an `INamedErpSystemHandler` by that key.
3. An "unrouted" fallback is used when no handler is found.
4. All outputs flow to a single downstream block.

This pattern is already demonstrable with existing POC infrastructure — no new library code
required.

---

## Questions for Implementation Team

1. **Is `item.System.Name` a stable, bounded set of values?** Confirm with product team that
   ERP type names are defined in application code, not free-form user input.

2. **Is a restart acceptable when a new ERP integration type is deployed?** New ERP types
   require code changes anyway (new handler implementation), so restart is inevitable.

3. **What concurrency is needed per ERP type?** Legacy had MaxConcurrency=3 for SAP. Does Oracle
   need different? This determines whether a thin static routing layer is worth adding.

4. **Should the unrouted path still flow through TMS?** The assessment says yes. Confirm this
   business rule is preserved in the migration.
