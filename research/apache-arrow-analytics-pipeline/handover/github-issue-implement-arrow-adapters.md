# [Implementation] Optional Apache Arrow Adapters for DataFlow POC

## Context and Objectives

Research validated Apache Arrow as a feasible optional path for interoperability-focused ETL scenarios, while keeping DataFrame as the default transform contract.

**Research Documentation**: `/research/apache-arrow-analytics-pipeline/`

**Key artifacts**:
- Main findings: `/research/apache-arrow-analytics-pipeline/README.md`
- Design: `/research/apache-arrow-analytics-pipeline/design/arrow-integration-architecture.md`
- ADR: `/poc/docs/adr/2026-04-15-apache-arrow-evaluation.md`
- Prototype notes: `/research/apache-arrow-analytics-pipeline/handover/prototype/`

---

## Implementation Scope

- [ ] Create optional Arrow-focused package/project in `poc/` (no breaking core API changes)
- [ ] Add `ArrowRecordBatchSourceBlock`
- [ ] Add `ArrowIpcSinkBlock`
- [ ] Add `ArrowToDataFrameAdapterBlock` (one-way bridge)
- [ ] Add journal normalization utility for `JournalBatch -> JournalEntry` fact batches with lineage keys
- [ ] Add unit tests for schema correctness and lineage retention
- [ ] Add integration test for Arrow IPC round-trip
- [ ] Add docs with "when to use Arrow vs DataFrame" decision guidance

---

## Non-Goals

- Replacing existing DataFrame ETL blocks
- Introducing distributed dataset query engine into POC
- Broad migration of all existing pipelines to Arrow

---

## Acceptance Criteria

- Optional Arrow pipeline can ingest normalized journal entry data and write Arrow IPC output
- Adapter path can hand off to existing DataFrame blocks without losing lineage keys
- No regressions in existing DataFrame path
- Documentation explicitly defines recommended usage boundaries

---

## Workflow Labels

- Duty: `workflow:implementation`
- Type: Feature
