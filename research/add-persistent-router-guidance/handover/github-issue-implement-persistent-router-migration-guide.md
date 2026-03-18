# Implementation Issue: AddPersistentRouter Migration Guidance Documentation

## Context and Objectives

### Problem Statement

Applications migrating from the legacy DataFlow library to the POC library encounter a challenge:
the legacy `AddPersistentRouter` primitive — which dynamically creates sub-flows at runtime per
distinct route key — has no direct equivalent in the POC library's static graph topology.

This issue tracks the delivery of:
1. User-facing documentation covering all viable migration approaches.
2. An update to the existing selective routing guide to address the dynamic routing question.
3. (Optional) A new guide specifically for `AddPersistentRouter` migration scenarios.

### Research Background

Research was conducted to investigate approaches and validate recommendations.

**Research Documentation**: `/research/add-persistent-router-guidance/`

**Key Research Artifacts**:
- Main findings: `/research/add-persistent-router-guidance/README.md`
- Design docs: `/research/add-persistent-router-guidance/design/`
- Prototype: `/research/add-persistent-router-guidance/handover/prototype/`

**Key Findings from Research**:

1. The static graph topology constraint is real and intentional in the POC library.
2. **Most migration cases can be solved without new library primitives** by re-framing the problem.
3. The root cause is that legacy `AddPersistentRouter` created sub-flows per **tenant instance**,
   but the underlying code was the same (just parameterized differently). In the POC model,
   per-tenant parameterisation belongs in `IOptionsSnapshot<T>`, not in graph topology.
4. Three viable approaches exist, each suited to different scenarios.

---

## Recommended Approach (Validated by Research)

### Approach C: Generic Handler with DI Polymorphism (Default Recommendation)

For the journal processing migration use case and the vast majority of `AddPersistentRouter`
migrations:

1. Implement `INamedErpSystemHandler` (or equivalent `INamedSystemHandler<T>`) with a
   `SystemName` property.
2. Implement one handler per supported ERP integration type (SAP, Oracle, etc.).
3. Register handlers with `services.AddScoped<INamedErpSystemHandler, SapBtpErpHandler>()`.
4. Implement a single dispatcher actor that resolves the handler by `item.System?.Name`.
5. Register the dispatcher actor as a single block in the graph.

This eliminates the per-route sub-flow entirely. New tenants are handled via configuration
(`IOptionsSnapshot<T>`), not new graph routes.

**Prototype Reference**: `/research/add-persistent-router-guidance/handover/prototype/`

---

## Deliverables

### 1. Update the Selective Routing Guide

**File**: `/poc/docs/guides/topology-selective-routing.md`

Add a section "When Your Routes Are Not Known at Build Time" covering:
- Re-frame the problem: distinguish between "new ERP type" vs "new ERP tenant"
- Explain why most dynamic routing needs are tenant-configuration problems, not routing problems
- Link to new migration guide

### 2. New Migration Guide: AddPersistentRouter Migration

**File**: `/poc/docs/guides/migrating-from-persistent-router.md`

Contents:
- What `AddPersistentRouter` did in the legacy library
- Why the POC has static topology
- Decision guide: which approach to use
- Complete worked example for each approach (A, B, C)
- Code samples from the prototype
- FAQ for common migration scenarios

### 3. (Optional) Update Getting Started / Migration Overview

If a migration overview document exists, add a reference to the new persistent router guide.

---

## Success Criteria

- [ ] New guide created at `/poc/docs/guides/migrating-from-persistent-router.md`
- [ ] Selective routing guide updated with dynamic routing section
- [ ] All code examples in guides are consistent with current POC API
- [ ] Guide covers all three approaches with clear decision criteria
- [ ] Guide validated against the journal processing migration scenario from research issue #55
- [ ] Migration guide cross-linked from other relevant guides

---

## Implementation Notes

### API Consistency

When writing code samples for the guides, use the builder API as it exists in the current POC.
The prototype in `/research/add-persistent-router-guidance/handover/prototype/` uses placeholder
interfaces — adapt to the real POC API in the actual guide.

Key files to reference for real API:
- `/poc/DataFlow/Builder/DataFlowGraphBuilder.cs`
- `/poc/DataFlow/Core/SelectiveRoutingEdgeStrategy.cs`
- `/poc/docs/guides/topology-selective-routing.md` (existing API examples)

### Approach A Notes (Pre-Enumerate at Startup)

If covering Approach A, note that `SelectiveRoutingEdgeStrategy<TItem>` already exists and can be
used directly. See `/poc/DataFlow/Core/SelectiveRoutingEdgeStrategy.cs`.

The guide should show how to enumerate systems at startup and create a static route for each,
using keyed DI services for per-system configuration if needed.

### Approach B Notes (Dispatcher Block with Internal Channels)

The prototype code in `DispatcherBlockPrototype.cs` shows `DynamicErpDispatcherActor`. This is
entirely user-space code — no library changes needed. The guide should show this as a pattern
available today, not a future library feature.

### Approach C Notes (Generic Handler)

This is the recommended default. The prototype in `ErpHandlerPrototype.cs` and
`DispatcherBlockPrototype.cs` (the `ErpPostingActor` class) shows the pattern. The guide should
make this the primary example.

---

## References

- **Research**: `/research/add-persistent-router-guidance/`
- **Research Issue**: #55 [Research] AddPersistentRouter guidance
- **Related Guide**: `/poc/docs/guides/topology-selective-routing.md`
- **Prototype**: `/research/add-persistent-router-guidance/handover/prototype/`
- **Design Docs**: `/research/add-persistent-router-guidance/design/`
