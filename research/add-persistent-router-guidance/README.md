# Research: AddPersistentRouter Guidance — Dynamic Routing in POC DataFlow

**Research Issue**: #55  
**Date**: 2026-03-17  
**Status**: Complete  
**Researcher**: GitHub Copilot (Research Duty)

---

## Executive Summary

The legacy `AddPersistentRouter` primitive dynamically creates a new sub-flow for every distinct
route key encountered in a stream. The POC DataFlow library has a **static graph topology** — all
blocks and connections are declared at DI registration time — so there is no direct equivalent.

However, the problem can be solved in several ways without adding new library primitives. This
research identifies **four approaches** and makes a clear recommendation based on the properties
of the use case.

### Recommendation

> **Start with Approach C (Generic Handler with DI polymorphism)** for the journal processing
> migration. If true per-system isolation is needed, combine it with a thin static routing layer
> (Approach A/C hybrid). Only reach for Approach B (Dispatcher Block) if new ERP systems must
> be added at runtime with zero restart.

---

## Problem Statement

The journal processing flow routes unsent journals to different ERP backends (SAP, Oracle, etc.)
based on a company-code → system mapping. The challenge:

1. The routing transformer pre-loads ~800–900 company-code → system records from the database.
2. Each distinct `System.Name` value triggers a separate sub-flow in the legacy router.
3. New ERP tenant instances can be added at runtime (the set of route keys is not known at
   startup).
4. The legacy router dynamically creates sub-flow builders for each new system name encountered.

In the POC DataFlow, edges are declared at registration time. **There is no built-in equivalent
to a "create sub-flow on demand" primitive**.

---

## Approaches Explored

### Approach A: Pre-Enumerate Routes at Startup

**See**: [`design/approach-a-startup-enumeration.md`](design/approach-a-startup-enumeration.md)

Load all known ERP systems at DI registration time and create a static route for each using
`SelectiveRoutingEdgeStrategy<T>`. Unknown keys fall through to an "unrouted" path.

| Aspect | Detail |
|--------|--------|
| **POC changes** | None |
| **Effort** | Low |
| **Dynamic routes** | ❌ Requires restart for new systems |
| **Observability** | ✅ Full graph-level visibility |
| **Best for** | Small, stable set of systems (< 20) |

**Key insight**: If adding a new ERP tenant is an infrequent administrative action, a restart is
acceptable — and the graph remains simple and fully observable.

---

### Approach B: Dispatcher Block with Internal Concurrency

**See**: [`design/approach-b-dispatcher-block.md`](design/approach-b-dispatcher-block.md)

Implement a single actor block that maintains an internal `ConcurrentDictionary` of per-system
`Channel<T>` pipelines. New systems create new channels and background tasks on first encounter.

| Aspect | Detail |
|--------|--------|
| **POC changes** | None (user-space actor code) |
| **Effort** | Medium |
| **Dynamic routes** | ✅ True runtime creation, zero restart |
| **Observability** | ⚠️ Internal pipelines not graph-visible |
| **Best for** | Unbounded or frequently changing system sets |

**Key tradeoff**: The internal pipelines are not visible to the graph visualization or metrics
subsystem. Observability must be implemented explicitly within the actor.

---

### Approach C: Generic Handler with DI Polymorphism ⭐ Recommended

**See**: [`design/approach-c-groupby-actor.md`](design/approach-c-groupby-actor.md)

Eliminate per-system routing entirely. A single actor block receives all items and resolves the
correct `IErpSystemHandler` by system type at call time, using DI-registered named handlers.
System-specific **configuration** (e.g., SAP subscription settings) is resolved via
`IOptionsSnapshot<T>` without needing separate routes.

| Aspect | Detail |
|--------|--------|
| **POC changes** | None |
| **Effort** | Low |
| **Dynamic routes** | ✅ New tenants handled via config, no restart |
| **Observability** | ✅ Full graph-level visibility |
| **Best for** | Most migration scenarios — recommended default |

**Key insight**: The legacy router created sub-flows per **tenant instance** (e.g., `SAP-Tenant1`,
`SAP-Tenant2`), but the underlying code was the same — just parameterized differently. In the POC
model, that parameterisation is handled by `IOptionsSnapshot<T>` or a named configuration
service, not by creating separate graph routes.

---

### Approach D: Sub-Graph Composition (Flow Factory)

**See**: [`design/approach-d-sub-graph-composition.md`](design/approach-d-sub-graph-composition.md)

Closest conceptual equivalent to `AddPersistentRouter`. Defines a reusable sub-graph template
and a factory actor that instantiates sub-graphs on demand. Requires significant new library
infrastructure (`ISubGraphTemplate`, `ISubGraphFactory`, lifecycle management).

| Aspect | Detail |
|--------|--------|
| **POC changes** | Yes — major new feature |
| **Effort** | High |
| **Dynamic routes** | ✅ Full dynamic sub-graph creation |
| **Observability** | ✅ Full (if properly integrated) |
| **Best for** | Complex per-route sub-graphs; future library investment |

**Not recommended now**: The problem can be solved without this. Consider filing a separate
research issue if sub-graph composition becomes a priority.

---

## Decision Guide

Use the following criteria to select the right approach:

```
Is the set of ERP system TYPES (SAP, Oracle, etc.) small and known at build time?
  └─ YES → Approach C: Generic Handler
       └─ Is per-type throughput isolation required?
            └─ YES → Combine with thin Approach A routing per TYPE (not per tenant)
            └─ NO  → Pure Approach C

  └─ NO → New integration types added dynamically too?
       └─ YES → Approach B: Dispatcher Block (with internal channels)
       └─ NO  → Re-examine: is this actually a TYPE vs TENANT distinction?
                If TENANT: still use Approach C (config-driven)
                If TYPE:   Approach C + restart policy

Can the application restart when a new system is added?
  └─ YES → Approach A or C (simpler, fully observable)
  └─ NO  → Approach B (or re-design to make restarts acceptable)
```

### For the Journal Processing Migration Specifically

1. **ERP system types** (SAP BTP, Oracle, etc.) are small in number and known at build time.
2. **New tenants** (new SAP environments for new customers) use the same SAP integration code,
   just with different subscription configuration.
3. The routing transformer **pre-loads** the company-code → system mapping at flow start — this
   is equivalent to startup enumeration.

Therefore: **Approach C** with `IOptionsSnapshot<T>` for per-tenant SAP configuration is the
correct architecture. The journal processing migration should not replicate the per-tenant routing
of the legacy library.

---

## Implementation Guidance

### Phase 1: Migrate with Approach C

1. Create `INamedErpSystemHandler` interface with a `SystemName` property.
2. Implement `SapBtpErpHandler : INamedErpSystemHandler` (extracts logic from
   `SapBtpConnectorErpJournalSender`).
3. Implement `UnroutedJournalHandler` (extracts logic from
   `UnroutedJournalPostingResultTransformer`).
4. Implement `ErpPostingActor` that resolves handler by `item.System?.Name`.
5. Register handlers in DI using `AddScoped<INamedErpSystemHandler, SapBtpErpHandler>()`.
6. Wire graph: `routing-transformer → erp-poster → tms-batcher → tms-sender`.

### Phase 2 (if concurrency isolation needed): Add Type-Level Routing

If different ERP types need different concurrency settings (e.g., SAP: 3 workers, Oracle: 1):

1. Add one static route per ERP **type** using `SelectiveRoutingEdgeStrategy`.
2. Each route has a dedicated `ErpPostingActor` with the correct `MaxConcurrency`.
3. Merge outputs via a shared `BufferBlock<JournalPostingResult>` before TMS blocks.

This creates a **small, static** routing layer (2–3 routes) rather than the legacy N-routes-per-tenant approach.

### Phase 3 (if truly dynamic routes required): Approach B

If it is confirmed that new ERP system **types** (not just tenants) can be added at runtime
without a deployment:

1. Implement `ErpDispatcherActor` with internal `ConcurrentDictionary<string, SystemPipeline>`.
2. Implement `IErpHandlerFactory` that resolves handlers from DI by system name.
3. Register system-specific handlers using keyed DI services.
4. Add internal metrics and logging to compensate for reduced graph observability.

---

## Notes on the Legacy Flow's Specific Patterns

### `systemName` Constructor Argument (Block 7)

The legacy `SapBtpConnectorErpJournalSender` was instantiated with `systemName` as a constructor
argument via `ActivatorUtilities.CreateInstance(sp, systemName)`. In the POC model:

- **Approach C**: The `systemName` is available on the `SystemJournalProcessingItems` item — the
  handler receives it as part of the item, not as a constructor argument.
- **Approach A**: Use keyed DI services (`AddKeyedScoped<INamedErpSystemHandler, SapBtpErpHandler>(systemName)`)
  to register and resolve per-instance configurations if needed.

### `JobTrackerService` Statefulness (Note 5)

The `JobTrackerService` is stateful within a flow execution (in-memory caches keyed by
batch+system). In the POC model with epoch-scoped actors, this service should be registered as
**scoped** so it is fresh for each epoch (flow execution). Do not register as singleton.

### TMS Blocks Shared Between Routes (Blocks 8–9)

In the legacy flow, `TmsStatusUpdateBatcher` and `TmsJournalStatusSender` were shared between
all routes via a helper method. In the POC migration:

- With Approach C: single actor produces all `JournalPostingResult` items → shared TMS path.
- With Approach A/B: merge outputs before the shared TMS path (buffer or merge block).

### Unrouted Journals Are Not Failures

The "unrouted" path produces `JournalPostingResult` records with a null `PostingResult` and these
still flow through TMS. The migration must preserve this: the generic handler should treat
"system not found" as an unrouted result, not an exception.

---

## Success Criteria Results

| Criterion | Status |
|-----------|--------|
| ≥ 3 approaches documented with pros/cons | ✅ 4 approaches |
| Clear recommendation with decision criteria | ✅ Approach C recommended |
| Implementation guidance concrete enough to act on | ✅ Phase 1–3 plan above |
| Prototype code (if needed) | ✅ In `/handover/prototype/` |
| No permanent POC code changes | ✅ Documentation-only |

---

## References

- **Related Research**: [`/research/selective-content-routing/`](../selective-content-routing/)
  — Existing research on `SelectiveRoutingEdgeStrategy<T>`
- **Existing Guide**: [`/poc/docs/guides/topology-selective-routing.md`](../../poc/docs/guides/topology-selective-routing.md)
  — How selective routing is currently documented
- **Legacy Flow Snapshot**: In research issue #55 description
- **Architectural Mismatch**: [`/research/architectural-mismatch-epoch-routing/`](../architectural-mismatch-epoch-routing/)
  — Related architectural considerations

---

## Design Documents

- [`design/approach-a-startup-enumeration.md`](design/approach-a-startup-enumeration.md)
- [`design/approach-b-dispatcher-block.md`](design/approach-b-dispatcher-block.md)
- [`design/approach-c-groupby-actor.md`](design/approach-c-groupby-actor.md)
- [`design/approach-d-sub-graph-composition.md`](design/approach-d-sub-graph-composition.md)
