# [Implementation] DataFrame Analytics Pipeline Blocks

## Context and Objectives

### Problem Statement

DataFlow users need to build ETL pipelines that process structured tabular data. Today they
must define per-pipeline item types (POCOs) and write bespoke blocks for each pipeline.
This research has validated that `Microsoft.Data.Analysis.DataFrame` can serve as a standard
common contract type across a set of composable blocks, enabling configuration-driven ETL
pipelines without per-pipeline type definitions.

### Research Background

Research was conducted to validate the approach.

**Research Documentation**: See `/research/dataframe-analytics-pipeline/`

**Key Research Artifacts**:
- Main findings: `/research/dataframe-analytics-pipeline/README.md`
- Design document: `/research/dataframe-analytics-pipeline/design/dataframe-block-architecture.md`
- ADR: `/poc/docs/adr/2026-04-09-dataframe-pipeline-blocks.md`
- Prototype code: `/research/dataframe-analytics-pipeline/handover/prototype/`

**Key Findings**:

1. **DataFrame is suitable as a pipeline item** — it is a .NET reference type that flows
   through typed channels without any core library changes. `BlockBase<DataFrame, DataFrame>`
   is all that is needed.

2. **`Parquet.Net` + `Parquet.Net.Data.Analysis`** provide a native round-trip between
   Parquet files and DataFrame objects (one-line conversion). No custom mapping code is needed.

3. **Thread safety requires Clone() on broadcast** — `DataFrame` is NOT thread-safe for
   mutations. The existing `BroadcastEdgeStrategy(cloneFunc: ...)` mechanism already handles
   this; blocks must treat input DataFrames as immutable (no in-place mutation).

4. **Partitioned fan-out is preferred over clone-broadcast** — `DataFramePartitionBlock`
   splits one DataFrame into N non-overlapping row-range slices. This enables parallel
   processing without cloning overhead and without duplicate-row risk at fan-in.

5. **`EnvelopeEdgeStrategy` tests remain correctly obsolete** — the `IDataEnvelope` control-
   plane is unrelated to DataFrame pipelines. DataFrames flow as typed items, not envelopes.

### Objectives

What this implementation should achieve:

- [ ] Create a new `Uniun.DataFlow.Analytics` NuGet package (new project in `poc/`)
- [ ] Implement `IParquetSourceRepository` and `IParquetSinkRepository` abstractions
- [ ] Implement source blocks: `ParquetSourceBlock`, `CsvSourceBlock`
- [ ] Implement transform blocks: `DataFrameFilterBlock`, `DataFrameSelectBlock`,
      `DataFrameTransformBlock`, `DataFramePartitionBlock`, `DataFrameGroupByBlock`,
      `DataFrameJoinBlock`, `DataFrameAccumulatorBlock`, `DataFrameDeduplicateBlock`,
      `DataFrameColumnMergeBlock` (fan-in: SQL-join of two branch outputs on a key column),
      `DataFrameRowConcatBlock` (fan-in: vertical row concatenation of branch outputs)
- [ ] Implement sink: `ParquetWriterActor`, `DataFrameLogBlock`
- [ ] Implement `DataFrameEdgeStrategyExtensions.DataFrameBroadcast()` convenience method
- [ ] Provide DI registration extensions
- [ ] Write unit tests for all blocks
- [ ] Write integration tests (in-memory Parquet round-trip)
- [ ] Write documentation: README, usage guide, ETL scenario examples
- [ ] Update `poc/DataFlow.sln` to include the new project

---

## Implementation Guidance

### Recommended Approach

All blocks use the existing `BlockBase<TIn, TOut>` with `TIn = DataFrame` and `TOut = DataFrame`.
No changes to core library are needed. The new package adds value-add blocks as a composable toolkit.

**Key Principles**:
1. **Immutability convention** — all blocks must treat input DataFrames as read-only; every
   transformation yields a new DataFrame
2. **Row-group streaming** — `ParquetSourceBlock` reads one row group at a time for memory efficiency
3. **DI-first** — `IParquetSourceRepository` and `IParquetSinkRepository` are resolved from DI;
   blocks remain platform-agnostic
4. **Partitioned fan-out** — `DataFramePartitionBlock` is the recommended pattern for parallel
   processing; it avoids both cloning cost and duplicate-row risk

### Design References

- **Design Document**: `/research/dataframe-analytics-pipeline/design/dataframe-block-architecture.md`
- **ADR**: `/poc/docs/adr/2026-04-09-dataframe-pipeline-blocks.md`
- **Prototype Code** (reference): `/research/dataframe-analytics-pipeline/handover/prototype/DataFrameBlocks.cs`
- **Prototype Tests** (reference): `/research/dataframe-analytics-pipeline/handover/prototype/DataFrameBlockTests.cs`

### API / Interface Design

See prototype code for full implementations. Key interfaces:

```csharp
/// <summary>Resolves the source stream for a Parquet file.</summary>
public interface IParquetSourceRepository
{
    Task<Stream> OpenReadAsync(string sourceKey, CancellationToken cancellationToken = default);
}

/// <summary>Accepts the output stream for a Parquet file to write.</summary>
public interface IParquetSinkRepository
{
    Task<Stream> OpenWriteAsync(string sinkKey, CancellationToken cancellationToken = default);
    Task CommitAsync(string sinkKey, CancellationToken cancellationToken = default);
}
```

Convenience extension for broadcast:
```csharp
public static class DataFrameEdgeStrategyExtensions
{
    public static BroadcastEdgeStrategy DataFrameBroadcast(
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
        => new BroadcastEdgeStrategy(
            cloneFunc: item => ((DataFrame)item).Clone(),
            bufferMode, bufferCapacity);
}
```

### Project Structure

Create a new project `poc/DataFlow.Analytics/` with the following NuGet references:
```xml
<PackageReference Include="Microsoft.Data.Analysis" Version="0.23.0" />
<PackageReference Include="Parquet.Net" Version="5.1.0" />
<PackageReference Include="Parquet.Net.Data.Analysis" Version="5.1.0" />
```
Plus a project reference to the core `poc/DataFlow/DataFlow.csproj`.

Create a corresponding test project `poc/DataFlow.Analytics.Tests/`.

---

## Test Scenarios

### Unit Tests

1. **`DataFrameFilterBlock`** — verify filtered row count, verify input not mutated
2. **`DataFrameSelectBlock`** — verify only selected columns present, row count preserved
3. **`DataFrameTransformBlock`** — verify transform applied, new DataFrame returned
4. **`DataFramePartitionBlock`** — verify N partitions, total row count preserved, no overlap
5. **`DataFrameGroupByBlock`** — verify aggregation output schema and values
6. **`DataFrameAccumulatorBlock`** — verify yields at target, yields remainder at end
7. **`DataFrameDeduplicateBlock`** — verify duplicates removed by key column
8. **`DataFrameLogBlock`** — verify log output schema contains column names and row counts
9. **`BroadcastEdgeStrategy(cloneFunc: df.Clone())`** — verify clone is deep copy (mutation isolation)

### Integration Tests

1. **Parquet round-trip** — read Parquet from MemoryStream, apply filter, write to MemoryStream,
   re-read and assert row count and values
2. **Partitioned parallel pipeline** — ParquetSourceBlock → DataFramePartitionBlock → 2x workers →
   EpochBufferBlock → terminal; verify all rows present exactly once
3. **Broadcast dual-route** — source → BroadcastEdgeStrategy(cloneFunc) → archive writer +
   analytics filter; verify both routes receive independent copies

### Edge Cases

- Empty Parquet file (0 row groups) → source yields nothing, sink writes nothing
- Single row group → no partition boundary issues
- DataFrame with 1 row partitioned into 4 → some partitions empty, no panic
- Filter that matches no rows → empty DataFrame propagated, not dropped
- Accumulator with exact target multiple → correct chunk boundaries

---

## Performance Requirements

| Metric | Target |
|---|---|
| Row throughput (single actor) | ≥ 1M rows/sec for simple filter/select |
| Memory per row group | ≤ row group size + 20% overhead |
| Clone overhead (for broadcast) | ≤ O(rows × columns) — documented trade-off |
| Parquet write throughput | ≥ 500K rows/sec (dominated by Parquet.Net) |

These are soft targets for reasonableness checking during implementation, not hard SLAs.

---

## Documentation Requirements

1. **README.md in `poc/DataFlow.Analytics/`** — package overview, quick start, ETL scenario
   examples (the 5 scenarios in the research README)
2. **XML doc on all public types**
3. **Code comments on broadcast safety rule** in `DataFrameEdgeStrategyExtensions`

---

## Notes from Research

- The `EnvelopeEdgeStrategyTests` tests (marked Skip) should remain as-is — they are
  correctly marked obsolete and are unrelated to DataFrame pipelines.
- `DataFrame.Append()` mutates in place — `DataFrameAccumulatorBlock` must clone the
  first frame before calling Append on subsequent ones.
- `DataFramePartitionBlock` uses `DataFrameColumn.Filter(mask)` for slicing. A more
  efficient implementation could use index arrays or `Memory<T>` slices if available
  in the DataFrame API.
- For very large fan-out scenarios, the `CompetingEdgeStrategy` + `DataFramePartitionBlock`
  combination is more efficient than `BroadcastEdgeStrategy` + clone, because partitioning
  avoids creating N copies of the full frame.

---

## Workflow Labels

- Duty: `workflow:implementation`
- Type: Feature
