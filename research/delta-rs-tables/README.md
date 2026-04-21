# Research: Delta-rs Tables as DataFlow Sources and Sinks

**Status**: ✅ Complete — Ready for implementation handover  
**Date**: 2026-04-17  
**Duty**: Research  
**Research Plan**: [research-plan.md](./research-plan.md)

---

## Executive Summary

This research validates that DataFlow POC can use Delta Lake tables as both **durable source ledgers** and **persistent sink targets** to decouple flows while preserving stream-like processing.

### Final Recommendation

1. Use **DeltaLake.Net (delta-dotnet)** as the primary .NET integration path.
2. Implement a **table-per-tenant** topology by default for stronger isolation.
3. Run **long-lived hosted source workers** with lease-based tenant sharding.
4. Prefer **CDF-based incremental reads** when available; keep **version-diff fallback**.
5. Target **ADLS Gen2 (ABFSS)** in Azure; use local filesystem tables for local development.

---

## Findings by Research Question

### 1) Suitable .NET libraries

#### Primary candidate: DeltaLake.Net (delta-dotnet)

Why:
- C# wrapper over delta-rs/delta-kernel bridge
- Explicit async model suitable for DataFlow block execution
- Documented ABFSS example in README

#### Secondary/reference candidates

- Flowtide Delta connector: useful reference architecture, but less direct fit for “delta-rs from POC” goal.
- Parquet-only libraries are insufficient alone because they do not provide Delta transaction log semantics.

### 2) Can DataFlow model Delta source blocks?

Yes.

POC already has compatible patterns:
- `EpochSourceBlock<T, TActor>` for continuous source generation
- `EpochActorBlock<TIn, TOut, TActor>` for scoped processing in long-running streams

A Delta source actor can poll table versions, read increments, and emit records/epochs while honoring cancellation and backpressure.

### 3) Can DataFlow model Delta sink blocks?

Yes.

A Delta sink actor can batch stream items and append commits to Delta tables. Sink should emit commit metadata for observability and advance source checkpoints only after downstream success.

### 4) Object storage options

- **Local development**: local filesystem Delta tables (deterministic test setup)
- **Azure production**: ADLS Gen2 (ABFSS) preferred; Azure Blob also viable

### 5) Multi-tenant architecture

#### Recommended default: table-per-tenant

Rationale:
- Better isolation and noisy-neighbor control
- Easier tenant-specific governance and operations
- Clear failure domains

#### Alternative: shared table with TenantId

Possible but higher contention/coordination risk under skewed load. Use only if operations explicitly optimize for shared-table management.

### 6) Long-running source workers vs external scheduler

#### Recommended: long-running hosted workers

- Simpler correctness model for polling/cursors
- Better compatibility with backpressure and continuous ingestion
- Scales by sharding tenant ownership across worker instances via leases

#### Not recommended as default: separate scheduler dispatching short-lived flows

Requires robust overlap prevention, completion signaling, and dedupe protections; higher operational complexity.

### 7) Is CDF usable from .NET?

Research conclusion:
- delta-rs protocol support indicates CDF capability exists.
- .NET integration should still implement runtime feature detection and fallback because API surface/maturity can vary by wrapper version and table feature state.

Recommended implementation behavior:
1. Try CDF incremental read path
2. If unavailable/unsupported, fall back to version-based diff strategy

---

## Proposed Architecture

Detailed design: `/research/delta-rs-tables/design/delta-rs-source-sink-architecture.md`

Decision record: `/research/delta-rs-tables/adr/2026-04-20-cdf-strategy-for-dotnet.md`

High-level components:
- `DeltaTableSourceActor<T>`
- `DeltaTableSinkActor<T>`
- `IDeltaTableClientFactory`
- `IDeltaCursorStore`
- `IDeltaLeaseStore`
- `IDeltaTenantRoutingStrategy`

---

## Validation Outcome

- [x] Approach validated through prototyping (architecture/interface prototype)
- [x] Research comprehensively documented
- [x] Implementation issue contains complete context
- [x] All supporting documentation created
- [x] Prototype code captured (prototype notes in handover/prototype)
- [x] Code changes reverted, only docs remain (no production code modified)
- [x] Self-improvement evaluation completed

---

## References

- delta-dotnet README: https://github.com/delta-incubator/delta-dotnet
- delta-rs README: https://github.com/delta-io/delta-rs
- POC source actor pattern: `/home/runner/work/dataflow/dataflow/poc/DataFlow/Blocks/EpochSourceBlock.cs`
- POC epoch actor pattern: `/home/runner/work/dataflow/dataflow/poc/DataFlow/Blocks/EpochActorBlock.cs`
