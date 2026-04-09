# Research: DataFrame Analytics Pipeline

**Status**: ✅ Complete — Awaiting Reviewer Approval  
**Date**: 2026-04-09  
**Duty**: Research  
**Research Plan**: [research-plan.md](./research-plan.md)

---

## Executive Summary

✅ **Validated**: `Microsoft.Data.Analysis.DataFrame` is a suitable common item type for
composable DataFlow ETL pipelines, with `parquet-dotnet` (`Parquet.Net` + `Parquet.Net.Data.Analysis`)
providing the Parquet I/O layer.

Key conclusions:

1. **DataFrame works as a pipeline item** — it is a reference type that flows through typed
   channels without any library changes. Blocks are simply `BlockBase<DataFrame, DataFrame>`.
2. **`Parquet.Net.Data.Analysis` provides native round-trip** — one-line conversion between
   Parquet streams and `DataFrame` objects, including automatic column-type mapping.
3. **Thread safety requires `Clone()` on broadcast** — `DataFrame` is NOT thread-safe for
   mutations. The existing `BroadcastEdgeStrategy(cloneFunc: df => df.Clone())` mechanism
   already handles this; it must be used whenever a `DataFrame` is broadcast to multiple routes.
4. **Preferred fan-out pattern is partitioned, not cloned** — splitting a DataFrame into row-
   range partitions (one chunk per downstream route) eliminates both the copy cost and the
   duplicate-processing problem that arises when a cloned DataFrame is broadcast and then
   merged back. This is the recommended approach for fan-out ETL.
5. **`EnvelopeEdgeStrategy` / `IDataEnvelope` tests remain correctly obsolete** — the obsolete
   envelope control-plane mechanism is unrelated to DataFrame-as-data-type pipelines. DataFrames
   flow as first-class typed items; no envelope wrapping is needed.
6. **De-duplicate semantics require design guidance** — merging broadcast copies back into a
   single `EpochBufferBlock<DataFrame>` will produce duplicate rows. The recommended mitigations
   are: (a) avoid broadcast-then-merge, (b) use partitioned fan-out, or (c) add an explicit
   `DataFrameDeduplicateBlock` (keyed on a row-id column) when de-duplication is required.

---

## 1. Microsoft.Data.Analysis DataFrame Capabilities

### What Is It?

`Microsoft.Data.Analysis.DataFrame` is a general-purpose, in-memory tabular data container
modelled after Python's Pandas DataFrame. Available on NuGet as
[`Microsoft.Data.Analysis`](https://www.nuget.org/packages/Microsoft.Data.Analysis/).
Latest stable version: **0.23.0**, targeting .NET 8.

### Core Operations

| Operation | API | ETL Use |
|---|---|---|
| Load from CSV | `DataFrame.LoadCsv(path)` | Initial ingest |
| Filter rows | `df.Filter(boolMask)` / `df[boolMask]` | Row selection |
| Select columns | `new DataFrame(df["A"], df["B"])` | Projection |
| Add / remove columns | `df.Columns.Add(col)` / `df.Columns.Remove("col")` | Schema manipulation |
| Sort | `df.OrderBy("col")` / `df.OrderByDescending("col")` | Ordering |
| Group + aggregate | `df.GroupBy("col").Mean("value")` | Roll-up |
| Join two frames | `df.Join(other, "key", "key")` | Enrichment |
| Append rows | `df.Append(rows)` | Accumulate batches |
| Merge (concat rows) | `DataFrame.Merge(a, b, ...)` | Fan-in |
| Describe | `df.Description()` | Statistics / profiling |
| Element-wise math | `df["A"] + df["B"]` | Derived columns |
| Boolean mask | `df["Age"].ElementwiseGreaterThan(25)` | Predicate construction |

### Memory Model

- Each column is stored as a `DataFrameColumn<T>` backed by contiguous `Memory<T>` blocks.
- Operations like `.Filter()`, `.OrderBy()`, `.GroupBy().Mean()` return **new** DataFrames;
  they do not mutate the original.
- `.Append()` and `.Columns.Add()` **do** mutate in place.
- `df.Clone()` produces a **deep copy** with independent backing arrays.

### Thread Safety

| Scenario | Safe? | Recommendation |
|---|---|---|
| Concurrent reads (no mutation) | ✅ Safe | Read-only access from multiple actors |
| Concurrent reads + any write | ❌ Unsafe | Clone before broadcast |
| Single actor processing | ✅ Safe | Normal usage |
| Actor writes new columns in place | ⚠️ Risky | Prefer creating new DataFrame |

**Design Rule for DataFlow blocks**: Treat the input `DataFrame` as **immutable**. Every
transformation block creates and yields a new `DataFrame` rather than mutating the input.
This eliminates the class of race condition where a cloned reference is also mutated.

---

## 2. Parquet Integration (parquet-dotnet)

### Package Setup

```xml
<!-- Core Parquet reader/writer -->
<PackageReference Include="Parquet.Net" Version="5.1.0" />

<!-- DataFrame ↔ Parquet bridge (separate package since v5+) -->
<PackageReference Include="Parquet.Net.Data.Analysis" Version="5.1.0" />
```

No vulnerabilities found in either package (advisory DB check: 2026-04-09).

### Reading Parquet into DataFrame

```csharp
using Parquet.Data.Analysis;

// Read entire file as one DataFrame
await using var stream = File.OpenRead("data.parquet");
DataFrame df = await stream.ReadParquetAsDataFrameAsync();

// Read row-group by row-group (memory-efficient, streaming)
using var reader = await ParquetReader.CreateAsync(stream);
for (int i = 0; i < reader.RowGroupCount; i++)
{
    using var groupReader = reader.OpenRowGroupReader(i);
    DataFrame chunk = await groupReader.ReadAsDataFrameAsync();
    // yield chunk downstream
}
```

### Writing DataFrame to Parquet

```csharp
using Parquet.Data.Analysis;

// Write to a stream (caller controls destination: file, blob, etc.)
await df.WriteAsync(outputStream);
```

### Type Mapping

Parquet.Net.Data.Analysis handles automatic column-type mapping:

| Parquet Type | DataFrame Column |
|---|---|
| `int32` | `PrimitiveDataFrameColumn<int>` |
| `int64` | `PrimitiveDataFrameColumn<long>` |
| `float` / `double` | `PrimitiveDataFrameColumn<float/double>` |
| `byte_array` (UTF-8) | `StringDataFrameColumn` |
| `boolean` | `PrimitiveDataFrameColumn<bool>` |
| `timestamp` | `PrimitiveDataFrameColumn<DateTimeOffset>` |
| Nullable types | Nullable column variants |

### Row Group as the Natural Streaming Unit

Parquet files are divided into **row groups** — the ideal unit of work for a streaming
DataFlow source:

- Each row group fits comfortably in memory (typical: 50 MB–500 MB compressed).
- Row groups can be read independently in sequence.
- A `ParquetSourceBlock` reads one row group → yields one `DataFrame` → yields next.
- This gives natural backpressure: the block pauses after yielding each chunk until
  downstream consumes it.

---

## 3. Broadcast / Merge Safety Analysis

### Problem Statement

When a DataFlow graph broadcasts a `DataFrame` to multiple downstream routes and those
routes converge back into a single block (fan-in), two problems can arise:

1. **Mutation race**: If two actors receive the **same reference** and both try to mutate it
   (even concurrently in separate threads), the result is undefined.
2. **Duplicate items**: When broadcast produces two copies of the frame and both arrive at a
   fan-in buffer block, downstream sees the data twice.

### Existing Infrastructure: BroadcastEdgeStrategy Clone Mechanism

The `BroadcastEdgeStrategy` already supports a `cloneFunc` parameter introduced precisely
for mutation isolation:

```csharp
var broadcastEdge = new Edge(
    source,
    new[] { routeA, routeB },
    new BroadcastEdgeStrategy(
        cloneFunc: item => ((DataFrame)item).Clone(),   // deep copy for each route
        bufferMode: BufferMode.Bounded,
        bufferCapacity: 100));
```

This solves the **mutation race** problem. However it does **not** solve the
**duplicate-rows problem** — after fan-in, downstream would receive two identical DataFrames.

### Recommended Patterns

#### Pattern A: Partitioned Fan-Out (Preferred)

Split the DataFrame into non-overlapping row-range partitions before dispatching to each
route. Each route receives a distinct, independent slice of the data. No duplication.
After fan-in, rows from each partition are unique.

```
Source ──[DataFrame]──► PartitionBlock ──[DataFrame(rows 0-N)]──► RouteA
                                       ──[DataFrame(rows N+1-M)]──► RouteB
                         ──────────────────────────────────────────► EpochBufferBlock
```

A `DataFramePartitionBlock` yields one `DataFrame` per partition. Its configuration is
simply the number of partitions (or a partition key column).

**Advantages**: No cloning overhead, no duplicates, natural parallelism.

#### Pattern B: Broadcast + Clone (When Different Processing Per Copy Is Needed)

When both routes need the *same* data (e.g., persist copy A and analyse copy B), broadcast
with clone. **Do not merge back** — routes must terminate independently.

```
Source ──[DataFrame]──► BroadcastEdgeStrategy(cloneFunc)
                              ├──► PersistBlock (writes to storage)
                              └──► AnalyticsBlock (computes statistics)
```

**Do not fan-in after clone-broadcast unless you explicitly want duplicate rows.**

#### Pattern C: De-duplicate Block (When Fan-In After Broadcast Is Unavoidable)

If the topology requires broadcast-then-merge and duplicate rows must be removed,
insert a `DataFrameDeduplicateBlock` before the terminal block. This requires a
row-identity column (e.g., a `row_id` or composite key).

```
Source ──[DataFrame]──► BroadcastEdgeStrategy(cloneFunc)
                              ├──► ProcessA
                              └──► ProcessB
                         ──────────────────► EpochBufferBlock ──► DataFrameDeduplicateBlock ──► Terminal
```

**Caveat**: Deduplication adds complexity and requires stable row identifiers. Prefer
Pattern A wherever possible.

---

## 4. Envelope Edge Strategy: Are the Tests Still Relevant?

### Background

The `EnvelopeEdgeStrategy` / `IDataEnvelope` infrastructure was designed to multiplex
**control signals** (`CheckpointBarrier`, `Heartbeat`) in-band alongside data items through
the same channel. All tests in `EnvelopeEdgeStrategyTests` are marked:

```csharp
[Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
```

### Analysis

The epoch stream model supersedes the in-band control signal approach — epoch boundaries
are now expressed through the `IEpochStream<T>` / `EpochBufferBlock` infrastructure, not
through multiplexed `IDataEnvelope` messages.

**For DataFrame pipelines**: DataFrames flow as first-class typed items. There is no need
for `IDataEnvelope` wrapping. The relevant edge strategy for isolation is
`BroadcastEdgeStrategy(cloneFunc)`, not `EnvelopeEdgeStrategy`.

**Conclusion**: The `EnvelopeEdgeStrategyTests` tests remain **correctly marked obsolete**.
The `EnvelopeEdgeStrategy` class and supporting infrastructure (`EnvelopeBlocks.cs`,
`EnvelopeStreamMerger.cs`, `EnvelopeAdapter.cs`) are retained for reference but are not
required for DataFrame pipelines. No tests should be reinstated.

### What *Is* Still Relevant: Clone-on-Broadcast Semantics

The underlying concern — ensuring that broadcast items are isolated before concurrent
actors mutate them — is real and still applies. The solution (cloneFunc in
`BroadcastEdgeStrategy`) already exists. For DataFrames specifically, the recommended
`cloneFunc` is `df => ((DataFrame)df).Clone()`.

This should be documented as a **design guideline** rather than enforced by library
infrastructure.

---

## 5. Proposed Block Catalogue

The following blocks form a composable ETL toolkit operating on `DataFrame`:

### Source Blocks

| Block | Input | Output | Description |
|---|---|---|---|
| `ParquetSourceBlock` | `object` (source) | `DataFrame` | Reads a Parquet file row-group by row-group; injects `IParquetSourceRepository` to abstract path/stream resolution |
| `CsvSourceBlock` | `object` | `DataFrame` | Reads a CSV file; wraps `DataFrame.LoadCsv` |

### Transform Blocks

| Block | Input | Output | Description |
|---|---|---|---|
| `DataFrameFilterBlock` | `DataFrame` | `DataFrame` | Applies a configurable row-filter predicate |
| `DataFrameSelectBlock` | `DataFrame` | `DataFrame` | Projects a configurable set of columns |
| `DataFrameTransformBlock<TConfig>` | `DataFrame` | `DataFrame` | General-purpose transform; receives typed config from DI |
| `DataFramePartitionBlock` | `DataFrame` | `DataFrame` | Splits one DataFrame into N row-range slices; enables fan-out without cloning |
| `DataFrameGroupByBlock` | `DataFrame` | `DataFrame` | Groups and aggregates (configurable key + aggregation) |
| `DataFrameJoinBlock` | `DataFrame` | `DataFrame` | Joins with a second DataFrame (injected via DI repository) |
| `DataFrameAccumulatorBlock` | `DataFrame` | `DataFrame` | Accumulates incoming chunks until a target row count, then yields |
| `DataFrameDeduplicateBlock` | `DataFrame` | `DataFrame` | Removes duplicate rows by identity column |

### Sink Blocks

| Block | Input | Output | Description |
|---|---|---|---|
| `ParquetWriterActor` | `DataFrame` | `object` | Writes DataFrames to Parquet via injected `IParquetSinkRepository` |
| `DataFrameLogBlock` | `DataFrame` | `object` | Logs schema + row count for debugging / observability |

### Abstractions for Artifact Storage

```csharp
/// <summary>
/// Resolves the source stream for a Parquet file to read.
/// Implementations might resolve from local filesystem, Azure Blob, S3, etc.
/// </summary>
public interface IParquetSourceRepository
{
    Task<Stream> OpenReadAsync(string sourceKey, CancellationToken cancellationToken);
}

/// <summary>
/// Receives the output stream for a Parquet file to write.
/// Implementations might write to local filesystem, Azure Blob, S3, etc.
/// </summary>
public interface IParquetSinkRepository
{
    Task<Stream> OpenWriteAsync(string sinkKey, CancellationToken cancellationToken);
    Task CommitAsync(string sinkKey, CancellationToken cancellationToken);
}
```

Both interfaces are resolved from DI, keeping the blocks platform-agnostic.

---

## 6. Compelling ETL Scenarios

### Scenario 1: Simple Parquet ETL (Filter + Select + Write)

```
ParquetSourceBlock ──► DataFrameFilterBlock ──► DataFrameSelectBlock ──► ParquetWriterActor
```

- Read a large Parquet file, row-group by row-group
- Filter rows by a date range or predicate
- Select a subset of columns
- Write filtered output to a new Parquet file (via blob storage)

**Configuration** is entirely block-level — no code changes for different filters or projections.

### Scenario 2: Partitioned Parallel Processing

```
ParquetSourceBlock ──► DataFramePartitionBlock ──[chunk A]──► ProcessA ──► EpochBufferBlock ──► ParquetWriterActor
                                                ──[chunk B]──► ProcessB ──►
```

- Split each row group into N partitions for concurrent processing by N actors
- EpochBufferBlock re-serialises the streams
- No duplication risk — each partition carries distinct rows

### Scenario 3: Enrichment via Join

```
ParquetSourceBlock ──► DataFrameJoinBlock ──► DataFrameSelectBlock ──► ParquetWriterActor
                             ↑
                     IEnrichmentRepository (DI)
```

- Load enrichment data (e.g., lookup table) from DI-injected repository at block startup
- Join each incoming DataFrame chunk with the enrichment data
- Output enriched + projected rows to Parquet

### Scenario 4: Aggregation Roll-Up

```
ParquetSourceBlock ──► DataFrameAccumulatorBlock ──► DataFrameGroupByBlock ──► ParquetWriterActor
```

- Accumulate chunks until N rows (e.g., full month of data)
- Group by date + category → sum amounts
- Write aggregate report to Parquet

### Scenario 5: Dual-Route Broadcast (No Fan-In)

```
ParquetSourceBlock ──► BroadcastEdgeStrategy(df.Clone())
                              ├──► ParquetWriterActor (archive copy)
                              └──► DataFrameFilterBlock ──► DataFrameLogBlock (analytics)
```

- Both routes see the same data
- Archive route writes unmodified copy
- Analytics route filters + logs statistics
- Routes are independent (no fan-in) — no duplication issue

---

## 7. Validation: Prototype Code

Prototype blocks are available in:
- [`handover/prototype/DataFrameBlocks.cs`](./handover/prototype/DataFrameBlocks.cs) — full block implementations
- [`handover/prototype/DataFrameBlockTests.cs`](./handover/prototype/DataFrameBlockTests.cs) — unit tests

The prototype demonstrates:
- DataFrame flowing through BlockBase-derived blocks
- ParquetSourceBlock with row-group streaming
- DataFrameFilterBlock with configurable predicate
- DataFramePartitionBlock for fan-out without cloning
- ParquetWriterActor using injected repository
- BroadcastEdgeStrategy with clone for mutation isolation

---

## 8. Outstanding Decisions for Implementation

| Decision | Recommended | Rationale |
|---|---|---|
| Separate NuGet package? | Yes — `Uniun.DataFlow.Analytics` | Avoids forcing Parquet/DataFrame deps on all users |
| Parquet.Net version | 5.1.0 | Latest stable, includes Data.Analysis integration |
| Microsoft.Data.Analysis version | 0.23.0 | Latest stable, targets .NET 8 |
| Block-level config style | Constructor injection via `IBlockContext` + DI | Consistent with existing pattern |
| Immutability convention | Documented guideline, not enforced | Enforcement would require wrapper type |
| Clone-on-broadcast enforcement | Documented guideline | `BroadcastEdgeStrategy(cloneFunc)` is already available |
| Partitioned fan-out block | Yes — `DataFramePartitionBlock` | Enables parallel ETL without duplicate risk |
| De-duplicate block | Yes — `DataFrameDeduplicateBlock` | Escape valve when broadcast-then-merge is unavoidable |

---

## 9. References

- [Microsoft.Data.Analysis API docs](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.analysis.dataframe?view=ml-dotnet-preview)
- [parquet-dotnet GitHub](https://github.com/aloneguid/parquet-dotnet)
- [Parquet.Net Data.Analysis integration](https://deepwiki.com/aloneguid/parquet-dotnet/5-data-analysis-integration)
- [DataFrame thread safety discussion](https://deepwiki.com/luisquintanilla/mlnet-custom-transformer-guide/6.3-thread-safety)
- [Row-group streaming pattern (Stack Overflow)](https://stackoverflow.com/questions/69433763/read-first-100-rows-from-parquet-file-in-c-sharp)
- [Existing BroadcastEdgeStrategy](../../poc/DataFlow/Core/EdgeStrategy.cs)
- [Existing EnvelopeEdgeStrategy (obsolete)](../../poc/DataFlow/Core/EnvelopeEdgeStrategy.cs)
- [EnvelopeEdgeStrategyTests (obsolete)](../../poc/DataFlow.Tests/EnvelopeEdgeStrategyTests.cs)
