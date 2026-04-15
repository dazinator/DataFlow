# Design: Arrow Integration Architecture (POC)

**Status**: Draft — from Research  
**Date**: 2026-04-15  
**Related Research**: [../README.md](../README.md)

---

## Goal

Introduce Apache Arrow as an optional interoperability layer without replacing the current DataFrame-centric ETL path.

---

## Proposed Integration Boundary

```
Raw JSON -> Normalize -> RecordBatch -> (optional)
  A) Arrow IPC transport route
  B) DataFrame conversion route for transform-heavy ETL
```

### Package/Component sketch

- `ArrowJsonIngestBlock` (source/transform): parse hierarchical JSON to nested or normalized `RecordBatch`
- `ArrowRecordBatchSourceBlock` (source): read Arrow IPC stream/file and emit `RecordBatch`
- `ArrowIpcSinkBlock` (sink): write `RecordBatch` to Arrow IPC stream/file
- `ArrowToDataFrameAdapterBlock` (transform): bridge to DataFrame pipeline blocks where high-level operations are needed

---

## Design Principles

1. **No core DataFlow contract changes** for initial adoption
2. **Optional path**: Arrow-specific blocks live in a dedicated optional package
3. **Immutable batch semantics**: treat incoming `RecordBatch` as read-only
4. **Lineage retention**: always carry `batch_id`, `journal_id`, `entry_id` during normalization

---

## Risks

- Increased complexity from dual data contracts (Arrow + DataFrame)
- More conversion points if a pipeline frequently crosses the boundary
- Team learning curve for low-level Arrow APIs

---

## Mitigations

- Keep Arrow usage explicit and localized to boundaries
- Prefer one-way conversions where possible (Arrow -> DataFrame or DataFrame -> Arrow once)
- Provide sample pipelines and validation tests before broad rollout
