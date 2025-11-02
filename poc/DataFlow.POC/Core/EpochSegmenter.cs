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
    /// Items are streamed directly to consumers without intermediate buffering.
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

        await using var enumerator = input.GetAsyncEnumerator(cancellationToken);
        var hasMore = await enumerator.MoveNextAsync();
        
        if (!hasMore)
            yield break;

        var currentEpoch = clock.CurrentEpochVector;

        while (hasMore)
        {
            var epochToYield = currentEpoch;
            
            // Create a streaming epoch that reads from the shared enumerator
            // until the epoch changes
            yield return new EpochStream<T>(
                epochToYield,
                StreamEpochItems());

            async IAsyncEnumerable<T> StreamEpochItems()
            {
                // Yield the current item
                yield return enumerator.Current;

                // Continue yielding items while they belong to the same epoch
                while (await enumerator.MoveNextAsync())
                {
                    var now = clock.CurrentEpochVector;
                    
                    if (!now.Equals(epochToYield))
                    {
                        // Epoch changed - update tracking and break
                        currentEpoch = now;
                        hasMore = true;
                        yield break;
                    }

                    yield return enumerator.Current;
                }

                // No more items
                hasMore = false;
            }
        }
    }

    /// <summary>
    /// Segments a stream using a key selector function to determine epoch boundaries.
    /// Items are streamed directly to consumers without intermediate buffering.
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

        await using var enumerator = input.GetAsyncEnumerator(cancellationToken);
        var hasMore = await enumerator.MoveNextAsync();
        
        if (!hasMore)
            yield break;

        long sequence = 1;
        var currentKey = epochKeySelector(enumerator.Current);

        while (hasMore)
        {
            var epochSequence = sequence;
            var epochKey = currentKey;
            
            // Create a streaming epoch that reads from the shared enumerator
            // until the key changes
            yield return new EpochStream<T>(
                EpochVector.FromSingleSource(sourceId, epochSequence),
                StreamEpochItems());

            async IAsyncEnumerable<T> StreamEpochItems()
            {
                // Yield the current item
                yield return enumerator.Current;

                // Continue yielding items while they have the same key
                while (await enumerator.MoveNextAsync())
                {
                    var itemKey = epochKeySelector(enumerator.Current);
                    
                    if (!EqualityComparer<TKey>.Default.Equals(itemKey, epochKey))
                    {
                        // Key changed - update tracking and break
                        sequence++;
                        currentKey = itemKey;
                        hasMore = true;
                        yield break;
                    }

                    yield return enumerator.Current;
                }

                // No more items
                hasMore = false;
            }
        }
    }
}
