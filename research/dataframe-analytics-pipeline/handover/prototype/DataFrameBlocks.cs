// =============================================================================
// Research Prototype: DataFrame Analytics Pipeline Blocks
// =============================================================================
// NOTE: This is REFERENCE CODE only. It lives in /research/ and is NOT part
// of the compiled solution. It is provided as a guide for implementation.
//
// Target package: Uniun.DataFlow.Analytics (new, separate from core)
// Required NuGet packages:
//   <PackageReference Include="Parquet.Net" Version="5.1.0" />
//   <PackageReference Include="Parquet.Net.Data.Analysis" Version="5.1.0" />
//   <PackageReference Include="Microsoft.Data.Analysis" Version="0.23.0" />
//   (plus the existing DataFlow.POC core project reference)
// =============================================================================

using System.Runtime.CompilerServices;
using Microsoft.Data.Analysis;
using Parquet;
using Parquet.Data.Analysis;
using DataFlow.POC.Core;

// ---------------------------------------------------------------------------
// Abstractions for platform-agnostic artifact storage
// ---------------------------------------------------------------------------

namespace DataFlow.Analytics.Abstractions;

/// <summary>
/// Resolves the source stream for a Parquet file.
/// Implementations can read from local filesystem, Azure Blob, S3, etc.
/// </summary>
public interface IParquetSourceRepository
{
    /// <summary>Opens a readable stream for the given source key.</summary>
    Task<Stream> OpenReadAsync(string sourceKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// Accepts the output stream for a Parquet file to write.
/// Implementations can write to local filesystem, Azure Blob, S3, etc.
/// </summary>
public interface IParquetSinkRepository
{
    /// <summary>Opens a writable stream for the given sink key.</summary>
    Task<Stream> OpenWriteAsync(string sinkKey, CancellationToken cancellationToken = default);

    /// <summary>Commits (flushes/closes) the written data for the given sink key.</summary>
    Task CommitAsync(string sinkKey, CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Source blocks
// ---------------------------------------------------------------------------

namespace DataFlow.Analytics.Blocks.Source;

using DataFlow.Analytics.Abstractions;

/// <summary>
/// Source block that reads a Parquet file row-group by row-group, yielding one
/// <see cref="DataFrame"/> per row group.
///
/// <para>
/// This is the recommended pattern for large Parquet files: each row group
/// is loaded individually, giving natural backpressure via the bounded channel
/// between this block and its downstream consumers.
/// </para>
///
/// <example>
/// Build a simple Parquet ETL pipeline:
/// <code>
/// var source = new ParquetSourceBlock(new BlockContext("source"), "input.parquet", repo);
/// var filter = new DataFrameFilterBlock(
///     new BlockContext("filter"), df => df["Amount"].ElementwiseGreaterThan(100));
/// var writer = new ParquetWriterActor("output.parquet", sinkRepo);
///
/// var graph = new DataFlowGraph("etl", logger);
/// graph.AddBlock(source);
/// graph.AddBlock(filter);
/// graph.AddBlock(writer);  // wrapped in EpochActorBlock in production
/// graph.AddEdge(new Edge(source, filter));
/// graph.AddEdge(new Edge(filter, writer));
/// await graph.ExecuteAsync(context);
/// </code>
/// </example>
/// </summary>
public sealed class ParquetSourceBlock : BlockBase<object, DataFrame>
{
    private readonly IParquetSourceRepository _repository;
    private readonly string _sourceKey;

    public ParquetSourceBlock(
        IBlockContext context,
        string sourceKey,
        IParquetSourceRepository repository)
        : base(context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _sourceKey = sourceKey;
    }

    public override async IAsyncEnumerable<DataFrame> ExecuteAsync(
        IAsyncEnumerable<object> _,
        IExecutionContext context)
    {
        await using var stream = await _repository.OpenReadAsync(_sourceKey, context.CancellationToken)
            .ConfigureAwait(false);
        using var reader = await ParquetReader.CreateAsync(stream).ConfigureAwait(false);

        for (int i = 0; i < reader.RowGroupCount; i++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            using var groupReader = reader.OpenRowGroupReader(i);
            yield return await groupReader.ReadAsDataFrameAsync().ConfigureAwait(false);
        }
    }
}

// ---------------------------------------------------------------------------
// Transform blocks
// ---------------------------------------------------------------------------

namespace DataFlow.Analytics.Blocks.Transform;

/// <summary>
/// Filters rows in each incoming <see cref="DataFrame"/> using a configurable predicate.
/// The input frame is treated as immutable; the filtered result is a new DataFrame.
/// </summary>
public sealed class DataFrameFilterBlock : BlockBase<DataFrame, DataFrame>
{
    private readonly Func<DataFrame, PrimitiveDataFrameColumn<bool>> _predicate;

    /// <param name="context">Block context.</param>
    /// <param name="predicate">
    /// A function that, given a DataFrame, returns a boolean column mask used to filter rows.
    /// Example: <c>df =&gt; df["Amount"].ElementwiseGreaterThan(100)</c>
    /// </param>
    public DataFrameFilterBlock(
        IBlockContext context,
        Func<DataFrame, PrimitiveDataFrameColumn<bool>> predicate)
        : base(context)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public override async IAsyncEnumerable<DataFrame> ExecuteAsync(
        IAsyncEnumerable<DataFrame> input,
        IExecutionContext context)
    {
        await foreach (var frame in input.WithCancellation(context.CancellationToken).ConfigureAwait(false))
        {
            // Filter creates a new DataFrame — input is not mutated
            yield return frame.Filter(_predicate(frame));
        }
    }
}

/// <summary>
/// Projects a subset of columns from each incoming <see cref="DataFrame"/>.
/// Produces a new DataFrame containing only the selected columns.
/// </summary>
public sealed class DataFrameSelectBlock : BlockBase<DataFrame, DataFrame>
{
    private readonly string[] _columns;

    public DataFrameSelectBlock(IBlockContext context, params string[] columns)
        : base(context)
    {
        if (columns == null || columns.Length == 0)
            throw new ArgumentException("At least one column must be specified.", nameof(columns));
        _columns = columns;
    }

    public override async IAsyncEnumerable<DataFrame> ExecuteAsync(
        IAsyncEnumerable<DataFrame> input,
        IExecutionContext context)
    {
        await foreach (var frame in input.WithCancellation(context.CancellationToken).ConfigureAwait(false))
        {
            // Build a new DataFrame from the selected columns (does not mutate input)
            var selectedColumns = _columns.Select(c => frame[c]).ToArray();
            yield return new DataFrame(selectedColumns);
        }
    }
}

/// <summary>
/// General-purpose transform block. Applies an arbitrary function to each incoming
/// <see cref="DataFrame"/>. The function must return a new DataFrame (not mutate the input).
/// </summary>
public sealed class DataFrameTransformBlock : BlockBase<DataFrame, DataFrame>
{
    private readonly Func<DataFrame, CancellationToken, Task<DataFrame>> _transform;

    public DataFrameTransformBlock(
        IBlockContext context,
        Func<DataFrame, CancellationToken, Task<DataFrame>> transform)
        : base(context)
    {
        _transform = transform ?? throw new ArgumentNullException(nameof(transform));
    }

    public override async IAsyncEnumerable<DataFrame> ExecuteAsync(
        IAsyncEnumerable<DataFrame> input,
        IExecutionContext context)
    {
        await foreach (var frame in input.WithCancellation(context.CancellationToken).ConfigureAwait(false))
        {
            yield return await _transform(frame, context.CancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Splits each incoming <see cref="DataFrame"/> into N row-range partitions and yields
/// each partition as a separate DataFrame.
///
/// <para>
/// Use this block for parallel fan-out ETL without cloning. Each partition contains a
/// distinct, non-overlapping row range. When partitions are merged back via
/// <c>EpochBufferBlock&lt;DataFrame&gt;</c>, no rows are duplicated.
/// </para>
///
/// <example>
/// 4-way parallel processing of each row group:
/// <code>
/// var source    = new ParquetSourceBlock(...);
/// var partition = new DataFramePartitionBlock(new BlockContext("partition"), partitions: 4);
/// var worker1   = /* actor block */;
/// var worker2   = /* actor block */;
/// var worker3   = /* actor block */;
/// var worker4   = /* actor block */;
/// var buffer    = new EpochBufferBlock&lt;DataFrame&gt;(...);
///
/// graph.AddEdge(new Edge(source, partition));
/// // Use CompetingEdgeStrategy so each partition chunk goes to exactly one worker
/// graph.AddEdge(new Edge(partition,
///     new[] { worker1, worker2, worker3, worker4 },
///     new CompetingEdgeStrategy(BufferMode.Bounded, 4)));
/// graph.AddEdge(new Edge(worker1, buffer));
/// graph.AddEdge(new Edge(worker2, buffer));
/// graph.AddEdge(new Edge(worker3, buffer));
/// graph.AddEdge(new Edge(worker4, buffer));
/// </code>
/// </example>
/// </summary>
public sealed class DataFramePartitionBlock : BlockBase<DataFrame, DataFrame>
{
    private readonly int _partitions;

    public DataFramePartitionBlock(IBlockContext context, int partitions)
        : base(context)
    {
        if (partitions < 2)
            throw new ArgumentOutOfRangeException(nameof(partitions), "Must have at least 2 partitions.");
        _partitions = partitions;
    }

    public override async IAsyncEnumerable<DataFrame> ExecuteAsync(
        IAsyncEnumerable<DataFrame> input,
        IExecutionContext context)
    {
        await foreach (var frame in input.WithCancellation(context.CancellationToken).ConfigureAwait(false))
        {
            foreach (var partition in Partition(frame))
                yield return partition;
        }
    }

    private IEnumerable<DataFrame> Partition(DataFrame frame)
    {
        long rowCount = frame.Rows.Count;
        long partitionSize = (rowCount + _partitions - 1) / _partitions; // ceiling division

        for (int i = 0; i < _partitions; i++)
        {
            long start = (long)i * partitionSize;
            if (start >= rowCount) break;

            long end = Math.Min(start + partitionSize, rowCount);
            long count = end - start;

            // Build a new DataFrame with sliced columns
            var slicedColumns = frame.Columns
                .Select(col => SliceColumn(col, start, count))
                .ToArray();
            yield return new DataFrame(slicedColumns);
        }
    }

    private static DataFrameColumn SliceColumn(DataFrameColumn col, long start, long count)
    {
        // Clone the column slice. The implementation varies by column type;
        // the simplest portable approach is:
        var newCol = col.Clone();
        // Trim to the slice window. Note: DataFrame API doesn't expose a direct slice,
        // so we build a boolean filter mask.
        // In practice, implementors can use specialized column methods for performance.
        // This prototype uses the Filter approach for clarity.

        // Create a mask: true for rows in [start, start+count)
        var mask = new PrimitiveDataFrameColumn<bool>("_mask", col.Length);
        for (long r = 0; r < col.Length; r++)
            mask[r] = r >= start && r < start + count;
        return col.Filter(mask);
    }
}

/// <summary>
/// Groups each incoming <see cref="DataFrame"/> by a key column and applies an aggregation,
/// yielding the aggregated result.
/// </summary>
public sealed class DataFrameGroupByBlock : BlockBase<DataFrame, DataFrame>
{
    private readonly string _keyColumn;
    private readonly Func<GroupBy, DataFrame> _aggregate;

    /// <param name="context">Block context.</param>
    /// <param name="keyColumn">Column name to group by.</param>
    /// <param name="aggregate">
    /// Aggregation function, e.g. <c>g =&gt; g.Sum("Amount")</c>.
    /// </param>
    public DataFrameGroupByBlock(
        IBlockContext context,
        string keyColumn,
        Func<GroupBy, DataFrame> aggregate)
        : base(context)
    {
        _keyColumn = keyColumn;
        _aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
    }

    public override async IAsyncEnumerable<DataFrame> ExecuteAsync(
        IAsyncEnumerable<DataFrame> input,
        IExecutionContext context)
    {
        await foreach (var frame in input.WithCancellation(context.CancellationToken).ConfigureAwait(false))
        {
            yield return _aggregate(frame.GroupBy(_keyColumn));
        }
    }
}

/// <summary>
/// Accumulates incoming <see cref="DataFrame"/> chunks until a target row count is reached,
/// then yields the accumulated DataFrame. Any remaining rows are yielded on completion.
///
/// <para>
/// Useful for consolidating small row groups from a Parquet file before a heavy aggregation,
/// or for producing fixed-size batches for downstream processing.
/// </para>
/// </summary>
public sealed class DataFrameAccumulatorBlock : BlockBase<DataFrame, DataFrame>
{
    private readonly long _targetRowCount;

    public DataFrameAccumulatorBlock(IBlockContext context, long targetRowCount)
        : base(context)
    {
        if (targetRowCount < 1)
            throw new ArgumentOutOfRangeException(nameof(targetRowCount));
        _targetRowCount = targetRowCount;
    }

    public override async IAsyncEnumerable<DataFrame> ExecuteAsync(
        IAsyncEnumerable<DataFrame> input,
        IExecutionContext context)
    {
        DataFrame? accumulated = null;

        await foreach (var frame in input.WithCancellation(context.CancellationToken).ConfigureAwait(false))
        {
            if (accumulated == null)
            {
                accumulated = frame.Clone();
            }
            else
            {
                // Append new rows to accumulated (mutating accumulated — which we own)
                accumulated.Append(frame.Rows);
            }

            if (accumulated.Rows.Count >= _targetRowCount)
            {
                yield return accumulated;
                accumulated = null;
            }
        }

        // Yield any remaining rows
        if (accumulated != null && accumulated.Rows.Count > 0)
            yield return accumulated;
    }
}

/// <summary>
/// Removes duplicate rows from each incoming <see cref="DataFrame"/> using an identity
/// column (or set of columns). Returns a new deduplicated DataFrame.
///
/// <para>
/// Use this block after a broadcast-then-fan-in topology when duplicate rows may arise.
/// Prefer <see cref="DataFramePartitionBlock"/> (partitioned fan-out) over this pattern
/// where possible, to avoid duplicates entirely.
/// </para>
/// </summary>
public sealed class DataFrameDeduplicateBlock : BlockBase<DataFrame, DataFrame>
{
    private readonly string[] _keyColumns;

    public DataFrameDeduplicateBlock(IBlockContext context, params string[] keyColumns)
        : base(context)
    {
        if (keyColumns == null || keyColumns.Length == 0)
            throw new ArgumentException("At least one key column is required.", nameof(keyColumns));
        _keyColumns = keyColumns;
    }

    public override async IAsyncEnumerable<DataFrame> ExecuteAsync(
        IAsyncEnumerable<DataFrame> input,
        IExecutionContext context)
    {
        await foreach (var frame in input.WithCancellation(context.CancellationToken).ConfigureAwait(false))
        {
            yield return Deduplicate(frame);
        }
    }

    private DataFrame Deduplicate(DataFrame frame)
    {
        var seen = new HashSet<string>();
        var keepRows = new PrimitiveDataFrameColumn<bool>("_keep", frame.Rows.Count);
        long keepCount = 0;

        for (long r = 0; r < frame.Rows.Count; r++)
        {
            var key = string.Join("|", _keyColumns.Select(c => frame[c][r]?.ToString() ?? ""));
            if (seen.Add(key))
            {
                keepRows[r] = true;
                keepCount++;
            }
            else
            {
                keepRows[r] = false;
            }
        }

        return frame.Filter(keepRows);
    }
}

// ---------------------------------------------------------------------------
// Sink actor
// ---------------------------------------------------------------------------

namespace DataFlow.Analytics.Blocks.Sink;

using DataFlow.Analytics.Abstractions;
using Parquet.Data.Analysis;

/// <summary>
/// Actor that collects incoming <see cref="DataFrame"/> chunks and writes them to a single
/// Parquet file via an injected <see cref="IParquetSinkRepository"/>.
///
/// <para>
/// The actor opens the output stream on its first item and keeps the Parquet writer open
/// across the actor's lifetime, flushing on completion. This allows the schema to be
/// determined from the first DataFrame received.
/// </para>
/// </summary>
public sealed class ParquetWriterActor : IStreamActor<DataFrame, object>
{
    private readonly IParquetSinkRepository _repository;
    private readonly string _sinkKey;

    public ParquetWriterActor(string sinkKey, IParquetSinkRepository repository)
    {
        _sinkKey = sinkKey;
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<DataFrame> input,
        IActorExecutionContext context)
    {
        Stream? outputStream = null;
        var frames = new List<DataFrame>();

        try
        {
            await foreach (var frame in input.WithCancellation(context.CancellationToken).ConfigureAwait(false))
            {
                // Collect all chunks; write in a single pass for schema consistency.
                // For very large outputs, an incremental writer could be used instead.
                frames.Add(frame);
            }

            if (frames.Count > 0)
            {
                // Merge all chunks into one DataFrame for writing
                // (for large flows, write row-group by row-group to manage memory)
                var merged = frames.Count == 1 ? frames[0] : MergeFrames(frames);

                outputStream = await _repository.OpenWriteAsync(_sinkKey, context.CancellationToken)
                    .ConfigureAwait(false);
                await merged.WriteAsync(outputStream).ConfigureAwait(false);
                await _repository.CommitAsync(_sinkKey, context.CancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (outputStream != null)
                await outputStream.DisposeAsync().ConfigureAwait(false);
        }

        yield break;
    }

    private static DataFrame MergeFrames(List<DataFrame> frames)
    {
        var result = frames[0].Clone();
        for (int i = 1; i < frames.Count; i++)
            result.Append(frames[i].Rows);
        return result;
    }
}

/// <summary>
/// Diagnostic sink block that logs the schema and row count of each incoming DataFrame.
/// Useful during development and testing of DataFrame pipelines.
/// </summary>
public sealed class DataFrameLogBlock : BlockBase<DataFrame, object>
{
    private readonly Action<string> _log;

    public DataFrameLogBlock(IBlockContext context, Action<string> log)
        : base(context)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public override async IAsyncEnumerable<object> ExecuteAsync(
        IAsyncEnumerable<DataFrame> input,
        IExecutionContext context)
    {
        int chunkIndex = 0;
        await foreach (var frame in input.WithCancellation(context.CancellationToken).ConfigureAwait(false))
        {
            var schema = string.Join(", ", frame.Columns.Select(c => $"{c.Name}:{c.DataType.Name}"));
            _log($"[{Name}] chunk={chunkIndex++} rows={frame.Rows.Count} schema=({schema})");
        }
        yield break;
    }
}

// ---------------------------------------------------------------------------
// Edge strategy helper
// ---------------------------------------------------------------------------

namespace DataFlow.Analytics.EdgeStrategies;

/// <summary>
/// Convenience factory for broadcast edge strategies that automatically clone DataFrames,
/// ensuring mutation isolation when multiple downstream routes process the same data.
///
/// <para>
/// <b>Important</b>: Prefer <see cref="DataFramePartitionBlock"/> for fan-out ETL patterns.
/// Only use clone-broadcast when both routes genuinely need the same full DataFrame and will
/// NOT be merged back into a single downstream block.
/// </para>
/// </summary>
public static class DataFrameEdgeStrategyExtensions
{
    /// <summary>
    /// Creates a broadcast edge strategy that deep-clones each DataFrame before writing to
    /// each target's channel. This ensures full mutation isolation across concurrent routes.
    /// </summary>
    public static BroadcastEdgeStrategy DataFrameBroadcast(
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
        => new BroadcastEdgeStrategy(
            cloneFunc: item => ((DataFrame)item).Clone(),
            bufferMode,
            bufferCapacity);
}
