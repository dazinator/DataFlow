# Exploration Notes: DataFrame Analytics Pipeline

**Date**: 2026-04-09  
**Author**: Research Duty

---

## Codebase Survey

### Existing Infrastructure Relevant to This Research

| Component | File | Relevance |
|---|---|---|
| `BlockBase<TIn, TOut>` | `poc/DataFlow/Core/BlockBase.cs` | All new blocks extend this |
| `IStreamActor<TIn, TOut>` | `poc/DataFlow/Core/IStreamActor.cs` | `ParquetWriterActor` implements this |
| `BroadcastEdgeStrategy` | `poc/DataFlow/Core/EdgeStrategy.cs` | Already has `cloneFunc` — perfect for DataFrame broadcast |
| `CompetingEdgeStrategy` | `poc/DataFlow/Core/EdgeStrategy.cs` | Used with `DataFramePartitionBlock` |
| `EpochBufferBlock<T>` | `poc/DataFlow/Blocks/EpochBufferBlock.cs` | Fan-in buffer for partitioned routes |
| `IBlockContext` | `poc/DataFlow/Core/IBlockContext.cs` | Constructor injection pattern |
| `IExecutionContext` | `poc/DataFlow/Core/IExecutionContext.cs` | Cancellation + DI scope |
| `EnvelopeEdgeStrategy` | `poc/DataFlow/Core/EnvelopeEdgeStrategy.cs` | **Obsolete** — not needed |
| `IDataEnvelope` | `poc/DataFlow/Core/IDataEnvelope.cs` | **Obsolete** — not needed for DataFrame |
| `EnvelopeEdgeStrategyTests` | `poc/DataFlow.Tests/EnvelopeEdgeStrategyTests.cs` | All tests `[Fact(Skip="...")]` |

### Key Finding: No Core Library Changes Required

`DataFrame` is a .NET reference type. It flows through `Channel<DataFrame>` like any other type.
`BlockBase<DataFrame, DataFrame>` compiles without any changes to the core library.

### BroadcastEdgeStrategy Already Has Clone Support

```csharp
// From poc/DataFlow/Core/EdgeStrategy.cs
public class BroadcastEdgeStrategy : EdgeStrategy
{
    private readonly Func<object, object>? _cloneFunc;

    public BroadcastEdgeStrategy(
        Func<object, object>? cloneFunc,   // <-- this is what we need
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
```

So `DataFrame.Clone()` drops in as:
```csharp
new BroadcastEdgeStrategy(
    cloneFunc: item => ((DataFrame)item).Clone(),
    BufferMode.Bounded, 100)
```

No library changes, no new strategy class needed — only a convenience factory method.

---

## Parquet.Net Research

- Latest stable version: **5.1.0**
- Separate package for DataFrame integration: `Parquet.Net.Data.Analysis` (version matches)
- Extension method: `stream.ReadParquetAsDataFrameAsync()`
- Extension method: `dataFrame.WriteAsync(stream)`
- Row group API: `ParquetReader.CreateAsync(stream)` → `reader.RowGroupCount` → `reader.OpenRowGroupReader(i)`

The row-group-at-a-time pattern is the standard for large files:
- One row group ≈ 50–500 MB compressed
- Typical row group = 50K–1M rows (depending on column width)
- Each row group yields one `DataFrame` chunk
- Natural backpressure via bounded channel

---

## DataFrame Operations Survey

Confirmed available via API docs:

```csharp
// Filtering (returns new DataFrame)
var mask = df["Amount"].ElementwiseGreaterThan(100);
var filtered = df.Filter(mask);

// Column projection (returns new DataFrame)
var projected = new DataFrame(df["A"], df["B"]);

// Sorting (returns new DataFrame)
var sorted = df.OrderBy("Amount");

// GroupBy + aggregate (returns new DataFrame)
var grouped = df.GroupBy("Category").Sum("Amount");

// Join (returns new DataFrame)
var joined = df.Join(other, leftJoinColumn: "Id", rightJoinColumn: "RefId");

// Append rows (MUTATES df — use Clone first!)
df.Append(otherRows);

// Deep copy
var clone = df.Clone();

// Statistics
var stats = df.Description();
```

---

## Thread Safety Research

`DataFrame` documentation and community analysis confirms:
- NOT thread-safe for concurrent writes
- Safe for concurrent reads (if no concurrent writes happening)
- `Clone()` produces an independent deep copy — each thread gets its own data

**Decision**: Design guideline = treat input DataFrame as immutable (never mutate in place).
All transform blocks create new DataFrames. This is consistent with how the built-in operations
(`Filter`, `OrderBy`, etc.) work anyway.

---

## Envelope Tests Analysis

All tests in `EnvelopeEdgeStrategyTests` are marked:
```csharp
[Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
```

Class summary says:
> These tests cover the envelope-based control plane approach where CheckpointBarrier and
> Heartbeat signals were multiplexed through the same channel as data. This design is
> superseded by the epoch stream model: epoch boundaries serve as barriers natively, and
> envelope-style message workflows are an application-level concern that requires no special
> library infrastructure. The production code is retained for reference.

This is exactly correct and should remain as-is. The `IDataEnvelope` approach:
- Was designed for in-band control signals
- Is NOT related to using DataFrame as a data type
- Epoch streams handle barriers natively now

**Verdict**: Tests remain obsolete. No reinstatement needed.

---

## Duplicate Processing Problem Analysis

When `BroadcastEdgeStrategy` clones DataFrame to 2 routes, and those routes both feed into
`EpochBufferBlock<DataFrame>`, the buffer receives 2 copies of the original data (each a clone).
If downstream processes all incoming DataFrames, each row is processed twice.

**This is by design for broadcast semantics** (both routes need the full data), but problematic
for fan-out → fan-in where each row should be processed exactly once.

Solutions ranked by preference:
1. **DataFramePartitionBlock + CompetingEdge**: Disjoint slices, no duplication. Best approach.
2. **No fan-in**: Broadcast routes terminate independently. Simple.
3. **DataFrameDeduplicateBlock**: Requires row-id column. Escape valve only.

---

## Open Questions for Implementation

1. Should `DataFramePartitionBlock` guarantee equal-size partitions or use round-robin
   row assignment? Equal-size is simpler and leads to better load balancing.

2. Should `ParquetWriterActor` write row-group by row-group (streaming) or collect all
   chunks in memory? The prototype collects in memory for simplicity; incremental writing
   needs careful schema handling.

3. Should `DataFrameJoinBlock` inject the right-hand DataFrame from DI, or accept it as a
   separate input stream? DI injection is simpler for static enrichment tables.

4. Is there value in a `DataFrameStatisticsBlock` that computes `df.Description()` and
   emits an observability event? Could feed into the visualization layer.
