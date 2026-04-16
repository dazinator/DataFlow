# Research Plan: Apache Arrow Analytics Pipeline

**Research Topic**: Apache Arrow as an alternative to `Microsoft.Data.Analysis` for DataFlow ETL scenarios  
**Research Start Date**: 2026-04-15  
**Duty**: Research  
**Related Issue**: [Research] apache arrow

---

## Research Objective

Assess whether Apache Arrow (`Apache.Arrow`) is a practical alternative to the existing DataFrame + Parquet approach for DataFlow ETL scenarios in the POC, using the same decision criteria as prior research.

---

## Research Questions

### Q1: Hierarchical JSON ingest model
Given a raw JSON payload (`JournalBatch -> Journals -> JournalEntries`):

- Can Arrow represent nested hierarchy directly?
- When is denormalization still required (for Parquet/analytics)?
- What canonical in-memory shape should DataFlow blocks use?

### Q2: Arrow vs Microsoft.Data.Analysis

- What does Arrow provide in .NET (`RecordBatch`, `Table`, IPC, schema model)?
- What is missing versus DataFrame (high-level transforms, built-in ETL ergonomics)?
- What integration complexity does this create for DataFlow blocks?

### Q3: Performance contrast

- Where Arrow is expected to outperform (columnar scans, serialization/interchange, memory layout)
- Where DataFrame may remain better for implementation productivity in .NET
- What realistic performance claims are supportable for this repository context

### Q4: Dataset processing beyond single tables

- Does Arrow provide first-class dataset processing (multi-file, partitioned datasets, query planning)?
- What is available in the .NET implementation vs C++/Python ecosystem?
- What architectural options are feasible for DataFlow if dataset semantics are required?

---

## Validation Approach

1. Review official Arrow C# API surface and ecosystem capabilities (IPC, schema, arrays, record batches)
2. Compare to prior DataFrame/Parquet ADR and research outputs
3. Produce a prototype-level mapping for:
   - hierarchical JSON -> Arrow nested schema
   - hierarchical JSON -> denormalized Arrow record batches
4. Document feasibility, risks, and implementation guidance
5. Create implementation handover issue draft with clear scope

---

## Expected Deliverables

| Artefact | Path |
|---|---|
| Research plan | `research/apache-arrow-analytics-pipeline/research-plan.md` |
| Findings README | `research/apache-arrow-analytics-pipeline/README.md` |
| Design document | `research/apache-arrow-analytics-pipeline/design/arrow-integration-architecture.md` |
| Prototype notes/code | `research/apache-arrow-analytics-pipeline/handover/prototype/` |
| Implementation handover | `research/apache-arrow-analytics-pipeline/handover/github-issue-implement-arrow-adapters.md` |
| ADR | `poc/docs/adr/2026-04-15-apache-arrow-evaluation.md` |
