# Research Plan: Delta-rs Tables as DataFlow Source/Sink

**Research Issue**: [Research] Delta-rs tables  
**Created**: 2026-04-17  
**Duty**: Research  
**Target Codebase**: POC

## Research Objective

Determine whether DataFlow POC can use Delta Lake tables (via delta-rs compatible .NET integrations) as:

1. **Source blocks** (intake ledger style)
2. **Persistent sink blocks** (durable output for downstream DataFlows)

while supporting multi-tenant isolation, Azure storage, and scalable execution.

## Research Questions

1. What .NET libraries are viable for Delta Lake access (especially delta-rs aligned)?
2. Can we model DataFlow source/sink blocks for Delta tables in the current POC architecture?
3. Which object storage backends are suitable for local development and Azure production?
4. What is the best multi-tenant topology (table-per-tenant vs shared table with TenantId)?
5. Is Delta Change Data Feed (CDF) practical from .NET for incremental source blocks?
6. What scale-out model works as tenant count grows?

## Validation Approach

- Review delta-rs and delta-dotnet public documentation for capabilities and storage support
- Review current POC source/actor block patterns (`EpochSourceBlock`, `EpochActorBlock`, source block guide)
- Define prototype-level block contracts and polling/checkpointing model
- Compare tenant-isolation options with operational and performance trade-offs
- Produce implementation-ready handover with phased plan and test matrix

## Success Metrics

- Feasible .NET integration options identified and ranked
- DataFlow source/sink architecture validated at design/prototype level
- Multi-tenant operating model selected with clear rationale
- CDF viability and fallback strategy defined
- Implementation handover contains concrete phases, tasks, and validation criteria

## Expected Outcomes

- Research findings: `/research/delta-rs-tables/README.md`
- Design specification: `/research/delta-rs-tables/design/delta-rs-source-sink-architecture.md`
- Working notes: `/research/delta-rs-tables/notes/exploration-notes.md`
- Implementation handover issue: `/research/delta-rs-tables/handover/github-issue-implement-delta-rs-source-sink-blocks.md`
- Prototype artifacts: `/research/delta-rs-tables/handover/prototype/`
- Self-improvement evaluation: `/research/delta-rs-tables/SELF_IMPROVEMENT.md`
