# Research Plan: AddPersistentRouter Guidance — Dynamic Routing in POC DataFlow

**Research Issue**: #55 (AddPersistentRouter guidance)  
**Created**: 2026-03-17  
**Status**: In Progress  
**Researcher**: GitHub Copilot (Research Duty)

---

## Research Objective

The POC DataFlow library does not have an equivalent of `AddPersistentRouter`, which dynamically
creates sub-flows at runtime based on data attributes encountered during processing.

This research must identify viable approaches so that applications migrating from the legacy
dataflow library can implement equivalent dynamic-routing behaviour without rewriting their
business logic.

### Context: The Journal Processing Problem

The application processes unsent journals and routes them to different ERP backends (SAP, Oracle,
etc.) based on a company-code → system mapping. The key difficulty is:

- Routing target systems are registered explicitly in the application database.
- New tenant ERP instances **can be added at runtime** — so the full set of route keys is not
  known at DI registration time.
- The legacy library's `AddPersistentRouter` handled this by dynamically creating a new sub-flow
  whenever a previously-unseen route key was encountered in the stream.

The POC library's graph topology is **static** — all blocks and connections must be declared at
`IServiceCollection` registration time.

---

## Research Questions

1. **What approaches allow "dynamic routing" in a static-topology graph?**
   — Each approach must be evaluated for feasibility, complexity, and trade-offs.

2. **Can the problem be re-framed to fit the static model?**
   — Is there a way to express the business requirement without truly dynamic routes?

3. **When is each approach the right choice?**
   — What properties of the use case determine which pattern to apply?

4. **What does an implementation look like?**
   — For each viable approach: what code changes, block types, and DI registrations are needed?

5. **What are the observability and lifecycle implications?**
   — How do metrics, logging, and shutdown semantics work for each approach?

---

## Success Metrics

- **Qualitative**: At least three distinct approaches documented with pros, cons, and code sketches
- **Qualitative**: Clear recommendation with decision criteria
- **Qualitative**: Implementation guidance concrete enough for an engineer to act on without
  returning to the researcher
- **Quantitative**: No POC code changes that need to be reverted (documentation-only outcome,
  or exploratory prototype if needed)
- **Validation**: At least one approach validated through a working prototype or integration with
  the existing test suite

---

## Validation Approach

- Review current `SelectiveRoutingEdgeStrategy<T>` to understand what is already available
- Write minimal prototype code in `/research/add-persistent-router-guidance/handover/prototype/`
  to validate the recommended approach
- Reference existing `DataFlow.Tests` test patterns to ensure approaches are testable

---

## Expected Outcomes

- `research-plan.md` — this document
- `README.md` — final research findings and recommendations
- `design/approach-a-startup-enumeration.md` — approach details
- `design/approach-b-dispatcher-block.md` — approach details
- `design/approach-c-groupby-actor.md` — approach details
- `design/approach-d-sub-graph-composition.md` — approach details
- `handover/github-issue-implement-dynamic-routing-guidance.md` — implementation issue template
- `handover/prototype/` — prototype code for recommended approach

---

## Timeline

Estimated research duration: 1 day (documentation-focused, no large prototype required)
