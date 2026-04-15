# ADR: Apache Arrow Evaluation for DataFlow ETL POC

**Date**: 2026-04-15  
**Status**: Proposed  
**Authors**: Research Duty  
**Related Research**: `/research/apache-arrow-analytics-pipeline/README.md`  
**Related ADR**: `/poc/docs/adr/2026-04-09-dataframe-pipeline-blocks.md`

---

## Context

A prior ADR selected `Microsoft.Data.Analysis.DataFrame` + Parquet for DataFlow ETL blocks. This follow-up evaluates Apache Arrow as an alternative for the same scenario set, including hierarchical journal batch ingestion and analytics-oriented processing.

---

## Decision

Retain DataFrame as the default ETL contract in the POC and treat Apache Arrow as an optional interoperability/transport extension.

---

## Rationale

1. Arrow has strong nested schema and columnar interchange capabilities.
2. Arrow .NET API is lower-level for common transform-heavy ETL authoring.
3. DataFrame path already has validated Parquet-centric architecture and implementation guidance in this repository.
4. Arrow adoption is most compelling for IPC/interchange boundaries and specialized performance paths, not as an immediate full replacement.

---

## Consequences

### Positive

- Preserves momentum on existing DataFrame implementation direction
- Enables targeted Arrow adoption where it provides clear value
- Avoids high-cost migration risk during POC phase

### Trade-offs

- Two potential data contracts (Arrow + DataFrame) if both are adopted
- Additional adapter/conversion code at boundaries

---

## Alternatives Considered

### Alternative 1: Full migration to Arrow

Rejected for current phase due to implementation complexity and lower-level transform ergonomics in .NET.

### Alternative 2: Keep Arrow entirely out of scope

Rejected because Arrow remains strategically useful for interoperability and transport-oriented optimization.
