# ADR: Delta incremental-read strategy for .NET (CDF-first vs version-diff fallback)

**Date**: 2026-04-20  
**Status**: Accepted (for POC / near-term implementation)

## Context

The Delta source/sink spike (`poc/DataFlow.DeltaSpike`) confirmed:

- local Delta append + restart-safe incremental reads are feasible,
- ABFSS URI access from containerized .NET runtime is feasible from a runtime dependency perspective (auth/env still required),
- current `DeltaLake.Net` API used in the spike does not expose an ergonomic CDF reader API equivalent to upstream `delta-rs` `load_cdf` semantics.

At the same time:

- upstream Delta and `delta-rs` support CDF semantics,
- we did not identify a better-documented .NET-native alternative with clearly exposed CDF APIs for our target usage.

This creates a practical design choice for DataFlow POC incremental consumption:

1. rely on version-diff now,
2. or invest early in a custom bridge (e.g., additional FFI surface) to expose CDF directly.

## Options Considered

### Option 1: Version-diff fallback only (no CDF-first intent)

**Pros:**
- Minimal implementation complexity
- No wrapper/API dependency on CDF exposure

**Cons:**
- Ignores CDF configuration and table capability
- Harder migration path to first-class CDF semantics later

### Option 2: CDF-first attempt with version-diff fallback (**chosen**)

**Pros:**
- Preserves forward compatibility with future .NET CDF API exposure
- Supports current append-heavy scenarios with reliable fallback
- Keeps runtime behavior explicit when CDF path is unavailable

**Cons:**
- Fallback behavior needs clear diagnostics and documentation
- Not full CDF semantics today for update/delete-heavy workloads

### Option 3: Build custom CDF bridge now (FFI-first)

**Pros:**
- Earliest access to true CDF semantics from .NET
- Better long-term fit for update/delete/merge-heavy streams

**Cons:**
- Additional maintenance burden outside upstream wrapper
- Higher implementation/testing complexity in current phase

## Decision

Adopt **Option 2** for the current POC and near-term implementation work:

- Keep **CDF-first intent** in adapter design.
- Use **version-diff fallback** as the operational path when CDF API is unavailable in current .NET wrapper surface.
- Record explicit diagnostic output when fallback is triggered.

This matches observed spike behavior and keeps us aligned with upstream capability while avoiding premature custom FFI work.

## Consequences

### Positive

- Immediate delivery path for append/incremental scenarios
- Stable restart semantics for source cursors in POC
- Reduced implementation risk while preserving extensibility

### Negative

- CDF semantics are partial in .NET path today
- Update/delete-heavy future use cases may need additional investment

### Neutral / Follow-up triggers

Revisit this ADR when one of the following occurs:

1. `DeltaLake.Net` exposes an ergonomic CDF API (`fromVersion` / `toVersion` semantics), or
2. workload requirements shift toward frequent updates/deletes/merges where fallback is insufficient.

## Related

- Spike implementation: `poc/DataFlow.DeltaSpike/`
- Spike findings: `research/delta-rs-tables/notes/delta-source-sink-spike-findings.md`
- Design: `research/delta-rs-tables/design/delta-rs-source-sink-architecture.md`
- Handover issue details: `research/delta-rs-tables/handover/github-issue-implement-delta-rs-source-sink-blocks.md`
