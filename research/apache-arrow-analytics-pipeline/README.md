# Research: Apache Arrow Analytics Pipeline

**Status**: ✅ Complete — Awaiting Reviewer Approval  
**Date**: 2026-04-15  
**Duty**: Research  
**Research Plan**: [research-plan.md](./research-plan.md)

---

## Executive Summary

Apache Arrow is **technically feasible** for DataFlow ETL scenarios, but in this repository's .NET-first context it is currently a better fit as an **interop/performance-focused extension** than as a full replacement for `Microsoft.Data.Analysis`.

### Decision Summary

1. **Hierarchy support**: Arrow can represent nested structures directly (`Struct`, `List`), so raw hierarchical JSON does not have to be denormalized immediately.
2. **ETL ergonomics in .NET**: Arrow C# is lower-level than DataFrame APIs; common ETL transforms require more custom code.
3. **Parquet and datasets**: Arrow C# lacks first-class dataset query/runtime capabilities available in Arrow C++/Python ecosystems; .NET usage requires additional orchestration.
4. **Performance**: Arrow should win for columnar interchange and memory-efficient transport, but pure .NET end-to-end ETL productivity remains stronger with DataFrame today.
5. **Recommendation**: Keep DataFrame as the default POC ETL contract. Introduce Arrow as an optional integration path (IPC + typed adapters) where interoperability or columnar transport is the primary driver.

---

## 1) Hierarchical JSON (`JournalBatch -> Journal -> JournalEntry`)

### Feasibility

Arrow supports nested schemas natively, so hierarchical payloads can be represented without immediate flattening:

- `JournalBatch`: top-level metadata + list of journals
- `Journal`: journal fields + list of entries
- `JournalEntry`: line-level facts

### Practical pipeline shapes

| Shape | Description | Pros | Cons |
|---|---|---|---|
| **Nested Arrow** | Keep hierarchy as `List<Struct<...>>` | Preserves payload fidelity, fewer mapping steps | Harder downstream aggregations in .NET-only transforms |
| **Denormalized Arrow** | Flatten to entry-level facts with keys (`batch_id`, `journal_id`) | Easier analytics and Parquet-like processing | Loses direct hierarchical shape unless keys are preserved |
| **Hybrid** | Ingest nested, then normalize before analytics stage | Best semantic fidelity + analytics readiness | More pipeline stages to maintain |

### Ruling for this use case

Use **hybrid** as default guidance:

1. Parse raw JSON into nested Arrow model (semantic fidelity)
2. Normalize into entry-fact batches before heavy analytics/aggregation
3. Preserve lineage keys (`batch_id`, `journal_id`, `entry_id`) for joins and reconstruction

---

## 2) Arrow vs `Microsoft.Data.Analysis`

| Capability | Apache Arrow (.NET) | Microsoft.Data.Analysis |
|---|---|---|
| Core in-memory model | Strong columnar model (`Array`, `RecordBatch`, `Table`) | DataFrame-centric table model |
| Nested types | Strong (first-class structs/lists) | Weaker for deeply nested hierarchies |
| High-level transforms (group/filter/join ergonomics) | Lower-level, more custom code in C# | Simpler, built-in DataFrame API patterns |
| Parquet workflow in current repo research context | Requires separate library boundaries and mapping patterns | Already validated with existing research + ADR |
| Cross-language/zero-copy interop | Excellent (major Arrow strength) | Limited compared to Arrow ecosystem |
| Team productivity for current POC | Medium (steeper learning curve) | High (already adopted in prior research) |

### Conclusion

Arrow is stronger as a **columnar interoperability substrate**; DataFrame is stronger as a **.NET ETL developer experience** in the current codebase.

---

## 3) Performance Contrast

### What Arrow is likely to improve

- Columnar memory scanning and projection
- Serialization/interchange (Arrow IPC)
- Cross-process/cross-language movement with reduced copy overhead

### What Arrow does not automatically improve in this repo

- High-level transform implementation effort in pure C#
- Dataset-level query planning/runtime in .NET
- Turnkey Parquet-oriented ETL workflow ergonomics already established with DataFrame research

### Performance ruling

For this repository's immediate .NET ETL needs, Arrow is unlikely to be a blanket performance win without additional compute/runtime components. It is most valuable where **data interchange and transport efficiency** dominate.

---

## 4) Dataset Processing (Beyond a Single Table)

Arrow ecosystem (especially C++/Python) includes mature dataset/query capabilities, but **Arrow C# does not currently provide the same end-to-end dataset runtime surface**.

### Implications for DataFlow POC

If multi-file partitioned dataset processing is required, feasible options are:

1. **Manual .NET orchestration**: enumerate files/partitions and compose batches in DataFlow
2. **Sidecar compute engine**: delegate dataset querying to a service/tool with stronger dataset runtime, return Arrow batches to DataFlow
3. **Scoped Arrow usage**: use Arrow for interchange and schema contracts, keep transform-heavy logic in DataFrame-oriented blocks

---

## Prototype Validation Summary

Prototype artifacts were created to validate schema mapping and normalization feasibility:

- `ArrowJournalSchemaSpike.cs`: nested schema mapping
- `ArrowJournalNormalizationSpike.cs`: entry-level denormalized record batch shape

See: `/research/apache-arrow-analytics-pipeline/handover/prototype/`

No production (`/poc` or `/src`) code changes were required for this research output.

---

## Recommendation

### Short-term (recommended)

- Keep current DataFrame + Parquet direction as primary path
- Add Arrow as optional adapter path where needed for interoperability/performance transport

### Medium-term (if Arrow adoption pressure increases)

- Implement minimal `ArrowRecordBatchSourceBlock` and `ArrowIpcSinkBlock`
- Add conversion adapters between `RecordBatch` and DataFrame-like contracts used by ETL blocks
- Re-benchmark with realistic journal workloads before broad migration decisions

---

## Research Outcome vs Success Criteria

- [x] Approach validated through prototyping
- [x] Research comprehensively documented
- [x] Implementation issue contains complete context
- [x] All supporting documentation created
- [x] Prototype code captured (reference-level)
- [x] Code changes reverted, only docs remain
- [x] Self-improvement evaluation completed (research retrospective included in handover)
