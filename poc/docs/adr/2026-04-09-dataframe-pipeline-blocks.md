# ADR: DataFrame Analytics Pipeline Blocks

**Date**: 2026-04-09  
**Status**: Proposed  
**Authors**: Research Duty  
**Related Research**: `/research/dataframe-analytics-pipeline/README.md`

---

## Context

DataFlow users need to build ETL pipelines that process structured tabular data (CSV, Parquet,
databases). Today, each pipeline defines its own POCO item types and bespoke blocks.

The question is whether `Microsoft.Data.Analysis.DataFrame` can serve as a standard item type
flowing through a set of composable, configurable DataFlow blocks — providing a ready-made ETL
toolkit without requiring per-pipeline type definitions.

---

## Decision

**We will create a new package `Uniun.DataFlow.Analytics`** that provides a set of DataFlow
blocks operating on `Microsoft.Data.Analysis.DataFrame` as their common item type, with
`Parquet.Net` as the Parquet I/O layer.

---

## Rationale

### Why DataFrame?

`DataFrame` is a general-purpose, in-memory tabular container that:

1. Supports the full ETL operation set: filter, select, sort, group + aggregate, join, append
2. Integrates natively with `Parquet.Net.Data.Analysis` for zero-boilerplate Parquet I/O
3. Is compatible with ML.NET (future use: feed processed data into ML pipelines)
4. Uses contiguous `Memory<T>` column storage — efficient for bulk processing
5. Provides `Clone()` for deep copy — necessary for broadcast mutation isolation

### Why Not POCO-per-pipeline?

POCO-per-pipeline requires defining a new type for every schema, bespoke blocks for every
operation, and no reuse of transformation logic. DataFrame-based blocks are schema-agnostic
and reusable across any tabular pipeline.

### Why Not Arrow / Apache Spark?

Arrow (`Apache.Arrow`) provides a more performance-oriented columnar format but has a steeper
API surface and no built-in Parquet round-trip for .NET. `Microsoft.Data.Analysis` is simpler
to use and is already part of the ML.NET ecosystem. Arrow can be a future extension if
performance requirements demand it.

---

## Consequences

### Positive

- ETL pipelines become configuration-driven (block types are reused, only configuration changes)
- Parquet read/write is one-line with `Parquet.Net.Data.Analysis`
- Row-group-at-a-time streaming fits naturally with DataFlow's backpressure model
- `BroadcastEdgeStrategy(cloneFunc: df => df.Clone())` provides mutation isolation without
  core library changes
- `DataFramePartitionBlock` enables parallel ETL with no duplication risk

### Negative / Trade-offs

- Adds new NuGet dependencies (`Microsoft.Data.Analysis`, `Parquet.Net`, `Parquet.Net.Data.Analysis`)
  — these are opt-in in the separate package
- `DataFrame` is mutable — must document and enforce immutability convention
- `DataFrame.Clone()` on broadcast is O(rows × columns) — not free
- Schema evolution across pipeline runs requires careful management (not handled by the blocks)

---

## Alternatives Considered

### Alternative 1: Raw POCO Pipelines (Status Quo)

**Why rejected**: Every pipeline requires per-schema code. No reuse. High maintenance cost.

### Alternative 2: `IDataEnvelope<DataFrame>` Wrapper

Wrapping `DataFrame` in `DataItem<DataFrame>` (the existing `IDataEnvelope` pattern).

**Why rejected**: The `IDataEnvelope` control-plane pattern is superseded by epoch streams and
is correctly marked obsolete. Adding DataFrame as a new "envelope payload type" would revive
an obsolete concept for no benefit. DataFrame flows as a first-class item type directly.

### Alternative 3: `Apache.Arrow` Table

Arrow's `RecordBatch` provides a more columnar-native representation with better interop for
distributed scenarios.

**Why deferred**: `Microsoft.Data.Analysis` has a simpler API, existing Parquet.Net integration,
and ML.NET compatibility. Arrow is a natural future extension if performance requirements grow.

---

## Thread Safety Ruling

`DataFrame` is NOT thread-safe for writes. The ruling for DataFlow blocks is:

1. **All blocks treat input DataFrame as immutable** — no in-place mutation allowed.
2. **Broadcast must use `BroadcastEdgeStrategy(cloneFunc: df => df.Clone())`** — enforced
   by design guideline documented in `DataFrameEdgeStrategyExtensions`.
3. **Preferred fan-out is partitioned** — `DataFramePartitionBlock` avoids both cloning cost
   and duplicate-row risk.

---

## Broadcast / Fan-In Ruling

### Problem

When a DataFrame is broadcast to N routes and then merged back via `EpochBufferBlock<DataFrame>`,
each row appears N times in the resulting stream (one per route). This is logically incorrect
for most ETL scenarios where each row should be processed exactly once.

### Resolution

Three patterns are sanctioned:

1. **Partitioned fan-out (preferred)** — `DataFramePartitionBlock` + `CompetingEdgeStrategy`
   + `EpochBufferBlock<DataFrame>`. No duplicates. No cloning. Each row processed exactly once.

2. **Broadcast without fan-in** — `BroadcastEdgeStrategy(cloneFunc)` where routes terminate
   independently (e.g., archive + analytics). Duplicates are expected and desired.

3. **Broadcast + fan-in + de-duplicate** — insert `DataFrameDeduplicateBlock` after
   `EpochBufferBlock<DataFrame>`. Use only when topological constraints prevent option 1.
   Requires stable row-identity columns.

### Merging Mutated Branches Back Together

When cloned DataFrames are mutated differently in each branch and must be recombined,
`Microsoft.Data.Analysis` provides two re-combination operations:

**Pattern A — Column-level merge (different columns added per branch)**

Each branch adds different columns to the same row set (same row count, same row-id column).
Use `DataFrame.Merge()` with the shared row-id key:

```csharp
// RouteA added a "Status" column; RouteB added a "Score" column.
// Both started from clones of the same frame and kept the "RowId" key.
var merged = routeAResult.Merge(
    routeBResult,
    leftJoinColumn: "RowId",
    rightJoinColumn: "RowId",
    joinAlgorithm: JoinAlgorithm.Inner);
// Result contains all original columns + Status + Score
```

`Merge` produces a **new** DataFrame (does not mutate either input). It works like an SQL JOIN
and requires the data to be sorted by the join key for large frames.

**Pattern B — Row-level concat (disjoint row sets from each branch)**

Each branch produced a subset of rows (e.g., after parallel processing of row partitions). Use
`Append` to vertically concatenate:

```csharp
// Collect all branch DataFrames and concatenate their rows
DataFrame combined = branches[0].Clone();
foreach (var branch in branches.Skip(1))
    combined.Append(branch.Rows, inPlace: true);
```

`Append` mutates in place (hence the `Clone()` on the first frame to avoid mutating a received
input). The `DataFrameAccumulatorBlock` prototype already demonstrates this pattern.

A dedicated `DataFrameRowConcatBlock` (fan-in block) should be added to the block catalogue
to encapsulate Pattern B cleanly. It accepts multiple incoming `DataFrame` streams (connected
as a fan-in via multiple edges to an `EpochBufferBlock<DataFrame>`) and yields the row-level
concatenated result.

### Envelope Tests Ruling

The `EnvelopeEdgeStrategyTests` tests (marked `Skip = "Envelope control plane superseded by
epoch streams"`) are **correctly classified as obsolete**. They cover the in-band control-signal
multiplexing approach that is superseded by epoch streams. No tests should be reinstated.
The `EnvelopeEdgeStrategy` class and supporting infrastructure are retained for reference only.

---

## Implementation Notes

- `DataFrameAccumulatorBlock` must clone the first frame before appending subsequent ones
  (since `DataFrame.Append()` mutates in place)
- `DataFramePartitionBlock` uses `DataFrameColumn.Filter(mask)` for slicing; a more
  allocation-efficient implementation using `Memory<T>` offsets can be explored later
- `ParquetWriterActor` collects all incoming chunks in memory and writes in one pass; for
  very large outputs, an incremental row-group writer should be considered
