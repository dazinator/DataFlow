namespace DataFlow.POC.Core;

using System.Runtime.CompilerServices;
using System.Threading.Channels;

/// <summary>
/// Execution policy for epoch streams.
/// </summary>
public enum EpochExecutionPolicy
{
    /// <summary>
    /// Process one epoch at a time. Simplest and safest.
    /// Next epoch doesn't start until current epoch completes.
    /// </summary>
    Sequential,

    /// <summary>
    /// Process multiple epochs concurrently with bounded concurrency.
    /// Acts as a natural rate limiter while preserving throughput.
    /// </summary>
    Overlapped
}

/// <summary>
/// Configuration for epoch segmentation.
/// </summary>
public sealed class EpochSegmenterConfig
{
    /// <summary>
    /// Buffer capacity for each epoch stream.
    /// </summary>
    public int BufferCapacity { get; init; } = 256;

    /// <summary>
    /// Maximum number of concurrent epochs in flight (for Overlapped policy).
    /// </summary>
    public int MaxConcurrentEpochs { get; init; } = 4;

    /// <summary>
    /// Execution policy for epoch streams.
    /// </summary>
    public EpochExecutionPolicy ExecutionPolicy { get; init; } = EpochExecutionPolicy.Sequential;
}

/// <summary>
/// Converts a continuous data stream into epoch-scoped substreams.
/// Each substream represents data belonging to a specific epoch and completes
/// when all data for that epoch has been consumed.
/// </summary>
public static class EpochSegmenter
{
    /// <summary>
    /// Segments a continuous input stream into epoch streams based on an epoch clock.
    /// Note: This is a simplified implementation that collects items into lists per epoch.
    /// For production use, consider a more sophisticated buffering strategy.
    /// </summary>
    public static async IAsyncEnumerable<IEpochStream<T>> SegmentByEpoch<T>(
        IAsyncEnumerable<T> input,
        IEpochClock clock,
        EpochSegmenterConfig? config = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(clock);

        config ??= new EpochSegmenterConfig();

        var currentEpoch = clock.CurrentEpochVector;
        var currentItems = new List<T>();
        var firstItem = true;

        await foreach (var item in input.WithCancellation(cancellationToken))
        {
            var now = clock.CurrentEpochVector;
            
            // Epoch changed - yield previous epoch and start new one
            if (!firstItem && !now.Equals(currentEpoch))
            {
                var previousEpoch = currentEpoch;
                var previousItems = currentItems.ToList();
                
                yield return new EpochStream<T>(
                    previousEpoch,
                    YieldItems(previousItems, cancellationToken));

                currentEpoch = now;
                currentItems = new List<T>();
            }

            currentItems.Add(item);
            firstItem = false;
        }

        // Yield final epoch if we have items
        if (!firstItem && currentItems.Count > 0)
        {
            yield return new EpochStream<T>(
                currentEpoch,
                YieldItems(currentItems, cancellationToken));
        }
    }

    private static async IAsyncEnumerable<T> YieldItems<T>(
        List<T> items,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    /// <summary>
    /// Segments a stream using a key selector function to determine epoch boundaries.
    /// </summary>
    public static async IAsyncEnumerable<IEpochStream<T>> SegmentByKey<T, TKey>(
        IAsyncEnumerable<T> input,
        Func<T, TKey> epochKeySelector,
        string sourceId,
        EpochSegmenterConfig? config = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(epochKeySelector);
        ArgumentNullException.ThrowIfNull(sourceId);

        config ??= new EpochSegmenterConfig();

        TKey? currentKey = default;
        long sequence = 0;
        var currentItems = new List<T>();
        var firstItem = true;

        await foreach (var item in input.WithCancellation(cancellationToken))
        {
            var itemKey = epochKeySelector(item);

            // Check for epoch boundary
            if (!firstItem && !EqualityComparer<TKey>.Default.Equals(itemKey, currentKey))
            {
                // Yield previous epoch
                var previousSequence = sequence;
                var previousItems = currentItems.ToList();
                
                yield return new EpochStream<T>(
                    EpochVector.FromSingleSource(sourceId, previousSequence),
                    YieldItems(previousItems, cancellationToken));

                // Start new epoch
                sequence++;
                currentKey = itemKey;
                currentItems = new List<T>();
            }
            else if (firstItem)
            {
                sequence = 1;
                currentKey = itemKey;
                firstItem = false;
            }

            currentItems.Add(item);
        }

        // Yield final epoch
        if (!firstItem && currentItems.Count > 0)
        {
            yield return new EpochStream<T>(
                EpochVector.FromSingleSource(sourceId, sequence),
                YieldItems(currentItems, cancellationToken));
        }
    }
}
