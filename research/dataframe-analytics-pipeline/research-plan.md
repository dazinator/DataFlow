# Research Plan: DataFrame Analytics Pipeline

**Research Topic**: DataFrame as a Common Envelope Type for DataFlow ETL Pipelines
**Research Start Date**: 2026-04-09
**Duty**: Research
**Related Issue**: [Research] - Parquet and DataAnalysis.DataFrame

---

## Research Objective

Determine whether `Microsoft.Data.Analysis.DataFrame` can serve as a standard, composable
item type flowing through DataFlow pipelines, and whether `parquet-dotnet` can be used as
the read/write layer for an ETL-oriented set of DataFlow blocks.

---

## Research Questions

### Q1: DataFrame as a Common Contract Type
Can we design a set of DataFlow blocks that all operate on `Microsoft.Data.Analysis.DataFrame`
as their common input/output type, enabling configuration-driven ETL pipelines?

- What operations does DataFrame support?
- Is it suitable as a pipeline item (size, lifecycle, memory)?
- What block types would cover common ETL scenarios?

### Q2: Parquet Integration
Can `parquet-dotnet` (Parquet.Net) be used for Parquet I/O within DataFlow blocks?

- How does Parquet.Net integrate with `Microsoft.Data.Analysis.DataFrame`?
- How do we handle large files (streaming by row group)?
- What's the model for writing output artifacts (via injected repository)?

### Q3: Broadcast / Merge Safety with DataFrame
When a DataFrame item is broadcast to multiple downstream routes and potentially merged
back together, what safety guarantees are required?

- Is DataFrame thread-safe?
- Does broadcasting share a reference or clone the object?
- How do we prevent duplicate processing after fan-in?
- Are the existing `EnvelopeEdgeStrategy` tests still relevant?

---

## Validation Approach

1. Review `Microsoft.Data.Analysis` API surface and thread safety documentation
2. Review `parquet-dotnet` API and its DataFrame integration package
3. Analyze existing `BroadcastEdgeStrategy` clone mechanism
4. Analyze existing `EnvelopeEdgeStrategy` and its obsolete tests
5. Create prototype blocks as reference implementations in `/research/` folder
6. Verify prototype compiles (locally, not added to the main solution)
7. Document conclusions and propose implementation-ready design

---

## Out of Scope

- ML.NET pipeline integration (future research)
- Distributed / multi-node DataFlow
- Real-time/streaming Parquet files (Kafka + Parquet)
- Direct Arrow / IPC format handling

---

## Expected Deliverables

| Artefact | Path |
|---|---|
| Research plan | `research/dataframe-analytics-pipeline/research-plan.md` |
| Findings README | `research/dataframe-analytics-pipeline/README.md` |
| Design document | `research/dataframe-analytics-pipeline/design/dataframe-block-architecture.md` |
| Prototype code | `research/dataframe-analytics-pipeline/handover/prototype/` |
| Implementation handover | `research/dataframe-analytics-pipeline/handover/github-issue-implement-dataframe-blocks.md` |
| ADR | `poc/docs/adr/2026-04-09-dataframe-pipeline-blocks.md` |
