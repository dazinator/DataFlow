# Design: DataFrame Block Architecture

**Status**: Draft — from Research  
**Date**: 2026-04-09  
**Author**: Research Duty  
**Related Research**: [README.md](../README.md)

---

## Overview

This document describes the architecture for the `Uniun.DataFlow.Analytics` package — a set
of composable DataFlow blocks that use `Microsoft.Data.Analysis.DataFrame` as their common
item type, with `Parquet.Net` providing the Parquet I/O layer.

---

## Package Structure

```
Uniun.DataFlow.Analytics/
├── Abstractions/
│   ├── IParquetSourceRepository.cs
│   └── IParquetSinkRepository.cs
├── Blocks/
│   ├── Source/
│   │   ├── ParquetSourceBlock.cs
│   │   └── CsvSourceBlock.cs
│   ├── Transform/
│   │   ├── DataFrameFilterBlock.cs
│   │   ├── DataFrameSelectBlock.cs
│   │   ├── DataFrameTransformBlock.cs
│   │   ├── DataFramePartitionBlock.cs
│   │   ├── DataFrameGroupByBlock.cs
│   │   ├── DataFrameJoinBlock.cs
│   │   ├── DataFrameAccumulatorBlock.cs
│   │   └── DataFrameDeduplicateBlock.cs
│   └── Sink/
│       ├── ParquetWriterActor.cs
│       └── DataFrameLogBlock.cs
├── Configuration/
│   ├── FilterConfiguration.cs
│   ├── SelectConfiguration.cs
│   ├── PartitionConfiguration.cs
│   ├── GroupByConfiguration.cs
│   ├── AccumulatorConfiguration.cs
│   └── DeduplicateConfiguration.cs
└── DependencyInjection/
    └── DataFlowAnalyticsExtensions.cs
```

---

## Core Design Principles

### 1. DataFrame as a First-Class Item Type

`DataFrame` flows through the standard `BlockBase<TIn, TOut>` with `TIn = DataFrame` and
`TOut = DataFrame`. No envelope wrapping is needed. The item is just a typed object like any
other in the DataFlow graph.

```csharp
public sealed class DataFrameFilterBlock : BlockBase<DataFrame, DataFrame>
{
    private readonly Func<DataFrame, PrimitiveDataFrameColumn<bool>> _predicate;

    public DataFrameFilterBlock(IBlockContext context,
        Func<DataFrame, PrimitiveDataFrameColumn<bool>> predicate)
        : base(context)
    {
        _predicate = predicate;
    }

    public override async IAsyncEnumerable<DataFrame> ExecuteAsync(
        IAsyncEnumerable<DataFrame> input,
        IExecutionContext context)
    {
        await foreach (var frame in input.WithCancellation(context.CancellationToken))
        {
            // .Filter() creates a new DataFrame — does not mutate input
            yield return frame.Filter(_predicate(frame));
        }
    }
}
```

### 2. Immutability Convention

All blocks **must** treat the incoming `DataFrame` as immutable. Every operation that
produces output yields a **new** `DataFrame`. This is the safety contract that makes the
blocks safe to use in any topology, including broadcast routes.

Operations that comply naturally:
- `df.Filter(mask)` → returns new DataFrame
- `df.OrderBy("col")` → returns new DataFrame
- `df.GroupBy("col").Mean(...)` → returns new DataFrame
- `new DataFrame(df["A"], df["B"])` → projection (reference to columns, but columns are also treated as read-only)

Operations that **do not comply** (avoid in blocks):
- `df.Columns.Add(col)` — mutates in place → use `new DataFrame(existing cols + new col)` instead
- `df.Append(rows)` — mutates in place → use clone-then-append pattern

### 3. Source Block: Row Group Streaming

`ParquetSourceBlock` reads the Parquet file one row group at a time, yielding one `DataFrame`
per row group. This provides:

- **Natural backpressure** — the bounded channel between source and downstream fills up,
  causing the source to pause after each yield
- **Memory efficiency** — only one row group is in memory at any time (typically 50–500 MB)
- **Composability** — downstream blocks receive DataFrame chunks in sequence

```csharp
public sealed class ParquetSourceBlock : BlockBase<object, DataFrame>
{
    private readonly IParquetSourceRepository _repo;
    private readonly string _sourceKey;

    public ParquetSourceBlock(IBlockContext context, string sourceKey,
        IParquetSourceRepository repo)
        : base(context) { _repo = repo; _sourceKey = sourceKey; }

    public override async IAsyncEnumerable<DataFrame> ExecuteAsync(
        IAsyncEnumerable<object> _,
        IExecutionContext context)
    {
        await using var stream = await _repo.OpenReadAsync(_sourceKey, context.CancellationToken);
        using var reader = await ParquetReader.CreateAsync(stream);

        for (int i = 0; i < reader.RowGroupCount; i++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            using var groupReader = reader.OpenRowGroupReader(i);
            yield return await groupReader.ReadAsDataFrameAsync();
        }
    }
}
```

### 4. Sink Block: Repository Abstraction

`ParquetWriterActor` writes DataFrames to Parquet via an injected `IParquetSinkRepository`.
This keeps the block platform-agnostic — the repository implementation decides where to write
(local file, Azure Blob, S3, etc.).

Because the actor accumulates multiple chunks before writing, it implements `IStreamActor`
and uses an internal `ParquetWriter` across the actor's lifetime (the writer is opened in
`RunAsync` and closed when the stream completes).

### 5. Fan-Out Safety: Partitioned vs Cloned

#### Partitioned (Preferred)

`DataFramePartitionBlock` splits an incoming `DataFrame` into N row-range slices. Each slice
is yielded as a separate `DataFrame`. These are connected to N downstream blocks via
individual edges. The slices are disjoint — no row appears in more than one slice.

```
Incoming frame (1000 rows) ──► DataFramePartitionBlock(partitions: 4)
    → yields: rows[0..249], rows[250..499], rows[500..749], rows[750..999]
```

Each slice is a **new** `DataFrame` backed by the parent's column arrays with an offset
(similar to `Memory<T>.Slice`). This is allocation-efficient.

After fan-in via `EpochBufferBlock<DataFrame>`, each partition arrives exactly once — no
duplicate rows.

#### Cloned Broadcast

When the *same* data is needed on multiple routes (e.g., archive + analytics), use:

```csharp
new BroadcastEdgeStrategy(
    cloneFunc: item => ((DataFrame)item).Clone(),
    BufferMode.Bounded, 100)
```

This uses the existing `BroadcastEdgeStrategy.cloneFunc` hook — no library changes required.

**Rule**: Never merge cloned broadcast copies back into a single terminal block unless a
`DataFrameDeduplicateBlock` is inserted.

---

## Configuration Design

Each transform block accepts a strongly-typed configuration object. Blocks resolve their
configuration from DI, keeping them testable and composable.

```csharp
public record FilterConfiguration(
    string ColumnName,
    FilterOperator Operator,
    object Value);

public enum FilterOperator { GreaterThan, LessThan, Equals, NotEquals, Contains }
```

For more complex scenarios, blocks accept a `Func<DataFrame, ...>` delegate so the caller
retains full control:

```csharp
// Simple predicate API
var filter = new DataFrameFilterBlock(context,
    df => df["Amount"].ElementwiseGreaterThan(100));

// DI-configured approach
services.AddSingleton<FilterConfiguration>(
    new FilterConfiguration("Amount", FilterOperator.GreaterThan, 100));
// Block resolves from DI
```

---

## DI Registration

```csharp
services.AddDataFlows("etl-pipeline", df =>
{
    df.DisplayName("Parquet ETL Pipeline");

    df.AddBlock<ParquetSourceBlock>("source",
        sp => new ParquetSourceBlock(
            new BlockContext("source"),
            sourceKey: "input/2026-04.parquet",
            repo: sp.GetRequiredService<IParquetSourceRepository>()));

    df.AddActorBlock<DataFrame, DataFrame, DataFrameFilterActor>("filter");

    df.AddActorBlock<DataFrame, object, ParquetWriterActor>("writer");

    df.AddGraph("main", g =>
    {
        g.Connect("source", "filter");
        g.Connect("filter", "writer");
    });
});

// Repository implementations
services.AddScoped<IParquetSourceRepository, AzureBlobParquetSourceRepository>();
services.AddScoped<IParquetSinkRepository, AzureBlobParquetSinkRepository>();
```

---

## Broadcast Safety Design Decision

The question of whether to enforce clone-on-broadcast at the infrastructure level (e.g., a
dedicated `DataFrameBroadcastEdgeStrategy`) or to document it as a design guideline was
considered.

**Decision: Document as a guideline, not enforce.**

Rationale:
- The existing `BroadcastEdgeStrategy(cloneFunc)` already provides the mechanism.
- Enforcement at the library level would require introspecting item types at graph build time.
- Many broadcast scenarios involve immutable items where cloning is unnecessary overhead.
- A `DataFrameEdgeStrategyExtensions` helper class can provide a convenience factory:

```csharp
public static class DataFrameEdgeStrategyExtensions
{
    /// <summary>
    /// Creates a broadcast edge strategy with automatic DataFrame cloning.
    /// Use this whenever broadcasting a DataFrame to multiple routes that may mutate their input.
    /// </summary>
    public static BroadcastEdgeStrategy DataFrameBroadcast(
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
        => new BroadcastEdgeStrategy(
            cloneFunc: item => ((DataFrame)item).Clone(),
            bufferMode, bufferCapacity);
}
```

---

## Testing Strategy

### Unit Tests

Each block is tested in isolation with small in-memory DataFrames:

```csharp
[Fact]
public async Task FilterBlock_FiltersRows()
{
    // Arrange
    var df = CreateTestDataFrame(rows: 10); // rows with Amount 1..10
    var block = new DataFrameFilterBlock(
        new BlockContext("filter"),
        df => df["Amount"].ElementwiseGreaterThan(5));

    // Act
    var results = await block.ExecuteAsync(
        AsyncEnumerable.FromItems(df), ExecutionContext.Empty).ToListAsync();

    // Assert
    results.Single().Rows.Count.ShouldBe(5);
}
```

### Integration Tests

End-to-end flow tests use a `MemoryStream` as the Parquet source/sink:

```csharp
[Fact]
public async Task ParquetRoundTrip_ReadsAndWritesCorrectly()
{
    var sourceStream = CreateParquetStream(rows: 100);
    var sinkStream = new MemoryStream();

    var sourceRepo = new InMemoryParquetSourceRepository(sourceStream);
    var sinkRepo = new InMemoryParquetSinkRepository(sinkStream);

    // Build and execute graph...
    // Assert sinkStream contains expected Parquet data
}
```

---

## 6. Domain Converter Pattern: DataFrameToPocoBlock

### Motivation

DataFrame blocks operate on a flat tabular schema. Many real-world domain entities are
hierarchical (e.g., a Journal Batch → Journals → Journal Entries). The
`DataFrameToPocoBlock<TOut>` pattern provides a clean boundary between the generic ETL zone
(flat `DataFrame`) and the typed domain zone (POCOs).

### Design

```csharp
/// <summary>
/// Base class for blocks that convert a denormalised flat DataFrame into a
/// strongly-typed domain entity.
/// </summary>
/// <typeparam name="TOut">The domain entity type to produce.</typeparam>
public abstract class DataFrameToPocoBlock<TOut> : BlockBase<DataFrame, TOut>
{
    protected DataFrameToPocoBlock(IBlockContext context) : base(context) { }

    /// <summary>
    /// Converts a single denormalised DataFrame into one or more domain entities.
    /// Each DataFrame typically represents one batch of related entities.
    /// </summary>
    protected abstract IEnumerable<TOut> Convert(DataFrame frame);

    public override async IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<DataFrame> input,
        IExecutionContext context)
    {
        await foreach (var frame in input.WithCancellation(context.CancellationToken))
        {
            foreach (var item in Convert(frame))
                yield return item;
        }
    }
}
```

### Usage Pattern

```csharp
public sealed class JournalBatchConverterBlock : DataFrameToPocoBlock<JournalBatch>
{
    public JournalBatchConverterBlock(IBlockContext context) : base(context) { }

    protected override IEnumerable<JournalBatch> Convert(DataFrame frame)
    {
        // Batch header from first row
        var batchRef  = (string)frame["BatchRef"][0]!;
        var batchDate = (DateTimeOffset)frame["BatchDate"][0]!;

        // Group rows by JournalRef to reconstruct the hierarchy
        var journals = new Dictionary<string, Journal>();
        for (long r = 0; r < frame.Rows.Count; r++)
        {
            var journalRef = (string)frame["JournalRef"][r]!;
            if (!journals.TryGetValue(journalRef, out var journal))
            {
                journal = new Journal(journalRef, (string)frame["JournalType"][r]!);
                journals[journalRef] = journal;
            }
            journal.Entries.Add(new JournalEntry(
                seq:         (int)frame["EntrySeq"][r]!,
                accountCode: (string)frame["AccountCode"][r]!,
                amount:      (decimal)frame["Amount"][r]!));
        }

        yield return new JournalBatch(batchRef, batchDate, journals.Values.ToList());
    }
}
```

### Pipeline Topology

```
ParquetSourceBlock          (1 row group = 1 batch = 1 DataFrame)
    ↓
DataFrameFilterBlock        (remove invalid entries, e.g. zero-amount rows)
    ↓
DataFrameTransformBlock     (normalise amounts, standardise account codes)
    ↓
DataFrameSelectBlock        (rename columns to match converter's expected schema)
    ↓
JournalBatchConverterBlock  ← ETL/domain boundary
    ↓
PostJournalToErpBlock       (domain-aware, typed on JournalBatch)
```

### Row Group Alignment Strategy

To make one `ParquetSourceBlock` emission equal to one domain entity batch:

- During ingestion/conversion to Parquet, write each batch as its own **row group**:

```csharp
// Writing: one row group per batch
await using var writer = await ParquetWriter.CreateAsync(schema, outputStream);
foreach (var batch in batches)
{
    var frame = DenormaliseJournalBatch(batch);
    using var groupWriter = writer.CreateRowGroup();
    await frame.WriteToRowGroupAsync(groupWriter);  // one row group per batch
}
```

- `ParquetSourceBlock` then yields **one `DataFrame` per row group**, which is exactly
  one batch — no accumulation or splitting is needed downstream.

### Pre-Converter Column Normalisation

A `DataFrameSelectBlock` preceding the converter acts as an **explicit schema contract**
between the ETL zone and the domain zone:

```csharp
// Rename source columns to match the converter's expected schema
var normalise = new DataFrameSelectBlock(context, new SelectConfiguration
{
    Columns = new[]
    {
        new ColumnMapping("batch_ref",    "BatchRef"),
        new ColumnMapping("batch_date",   "BatchDate"),
        new ColumnMapping("journal_ref",  "JournalRef"),
        new ColumnMapping("journal_type", "JournalType"),
        new ColumnMapping("entry_seq",    "EntrySeq"),
        new ColumnMapping("acct_code",    "AccountCode"),
        new ColumnMapping("amount",       "Amount"),
    }
});
```

This means:
- The upstream data source can use any column naming convention
- The `DataFrameSelectBlock` translates to the canonical names the converter expects
- The converter block itself never needs to change for different source systems

---

## Known Limitations and Trade-offs

| Area | Limitation | Mitigation |
|---|---|---|
| DataFrame in-memory only | Large files require multiple row groups; whole-file operations not supported streaming | Row-group-at-a-time pattern is the standard approach |
| Parquet schema evolution | Adding/removing columns in mid-stream breaks schema consistency | Schema should be consistent within a single pipeline run; schema migration is a separate concern |
| Clone cost on broadcast | `df.Clone()` is O(rows * columns) | Partitioned fan-out avoids cloning entirely |
| Join scalability | `df.Join()` loads both sides in memory | Right-hand side should be a lookup table (small), not a second large stream |
| Column naming collisions | `df.Join()` produces duplicate column names with a suffix | `DataFrameSelectBlock` can rename/deselect after join |
| Denormalisation memory overhead | Hierarchy columns (batch ref, journal ref) duplicated on every entry row — O(rows × hierarchy_depth) | Acceptable for typical ERP batch sizes; Parquet Snappy compression reduces storage cost; row-group-per-batch keeps only one batch in memory at a time |
| Converter sort dependency | `DataFrameToPocoBlock` may assume rows are sorted by entity key (e.g. `JournalRef`) | Write Parquet sorted by `(BatchRef, JournalRef, EntrySeq)` during ingestion, or use a `Dictionary`-based grouping inside the converter |
