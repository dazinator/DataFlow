namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;
using System.Runtime.CompilerServices;

/// <summary>
/// PROTOTYPE: Configuration for epoch segmentation policies.
/// Part of the "decoupled epoch" research.
/// </summary>
public sealed class EpochSegmentationPolicy
{
    /// <summary>
    /// No segmentation - pass through items as-is without epochs.
    /// Useful for pipelines that don't need epoch boundaries.
    /// </summary>
    public static readonly EpochSegmentationPolicy None = new() { Mode = SegmentationMode.None };
    
    /// <summary>
    /// The segmentation mode.
    /// </summary>
    public SegmentationMode Mode { get; init; }
    
    /// <summary>
    /// For Count mode: number of items per epoch.
    /// </summary>
    public int? ItemsPerEpoch { get; init; }
    
    /// <summary>
    /// For Time mode: time window per epoch.
    /// </summary>
    public TimeSpan? TimeWindow { get; init; }
    
    /// <summary>
    /// For Key mode: key selector function.
    /// </summary>
    public Delegate? KeySelector { get; init; }
    
    /// <summary>
    /// For Clock mode: epoch clock.
    /// </summary>
    public IEpochClock? Clock { get; init; }
    
    /// <summary>
    /// For Custom mode: custom segmentation function.
    /// </summary>
    public Delegate? CustomSegmenter { get; init; }
    
    /// <summary>
    /// Source identifier for epoch vectors.
    /// </summary>
    public string SourceId { get; init; } = "default-source";
    
    /// <summary>
    /// Execution policy for epoch streams.
    /// </summary>
    public EpochExecutionPolicy ExecutionPolicy { get; init; } = EpochExecutionPolicy.Sequential;
    
    /// <summary>
    /// Maximum concurrent epochs (for Overlapped policy).
    /// </summary>
    public int MaxConcurrentEpochs { get; init; } = 4;

    /// <summary>
    /// Creates a count-based segmentation policy.
    /// </summary>
    public static EpochSegmentationPolicy ByCount(int itemsPerEpoch, string sourceId = "default-source")
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(itemsPerEpoch, 1);
        ArgumentNullException.ThrowIfNull(sourceId);
        
        return new EpochSegmentationPolicy
        {
            Mode = SegmentationMode.Count,
            ItemsPerEpoch = itemsPerEpoch,
            SourceId = sourceId
        };
    }
    
    /// <summary>
    /// Creates a key-based segmentation policy.
    /// </summary>
    public static EpochSegmentationPolicy ByKey<T, TKey>(
        Func<T, TKey> keySelector,
        string sourceId = "default-source")
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(sourceId);
        
        return new EpochSegmentationPolicy
        {
            Mode = SegmentationMode.Key,
            KeySelector = keySelector,
            SourceId = sourceId
        };
    }
    
    /// <summary>
    /// Creates a clock-based segmentation policy.
    /// </summary>
    public static EpochSegmentationPolicy ByClock(
        IEpochClock clock,
        string sourceId = "default-source")
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(sourceId);
        
        return new EpochSegmentationPolicy
        {
            Mode = SegmentationMode.Clock,
            Clock = clock,
            SourceId = sourceId
        };
    }
    
    /// <summary>
    /// Creates a custom segmentation policy.
    /// </summary>
    public static EpochSegmentationPolicy Custom<T>(
        Func<IAsyncEnumerable<T>, IAsyncEnumerable<IEpochStream<T>>> customSegmenter,
        string sourceId = "default-source")
    {
        ArgumentNullException.ThrowIfNull(customSegmenter);
        ArgumentNullException.ThrowIfNull(sourceId);
        
        return new EpochSegmentationPolicy
        {
            Mode = SegmentationMode.Custom,
            CustomSegmenter = customSegmenter,
            SourceId = sourceId
        };
    }
}

/// <summary>
/// Segmentation modes supported by EpochSegmenterBlock.
/// </summary>
public enum SegmentationMode
{
    /// <summary>
    /// No segmentation - pass through without epochs.
    /// </summary>
    None,
    
    /// <summary>
    /// Segment by item count.
    /// </summary>
    Count,
    
    /// <summary>
    /// Segment by time window.
    /// </summary>
    Time,
    
    /// <summary>
    /// Segment by key selector.
    /// </summary>
    Key,
    
    /// <summary>
    /// Segment by epoch clock.
    /// </summary>
    Clock,
    
    /// <summary>
    /// Custom segmentation logic.
    /// </summary>
    Custom
}

/// <summary>
/// PROTOTYPE: Block that applies epoch segmentation to continuous data streams.
/// This is part of the "decoupled epoch" research - it separates epoch concerns
/// from source blocks, allowing sources to emit plain IAsyncEnumerable&lt;T&gt;
/// while segmentation is applied externally.
/// </summary>
/// <typeparam name="T">The type of items to segment</typeparam>
public sealed class EpochSegmenterBlock<T> : BlockBase<T, IEpochStream<T>>
{
    private readonly EpochSegmentationPolicy _policy;

    public EpochSegmenterBlock(string name, EpochSegmentationPolicy policy)
        : base(name)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public override async IAsyncEnumerable<IEpochStream<T>> ExecuteAsync(
        IAsyncEnumerable<T> input,
        IExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Route to appropriate segmentation strategy
        var segmented = _policy.Mode switch
        {
            SegmentationMode.None => SegmentNone(input, context),
            SegmentationMode.Count => SegmentByCount(input, context),
            SegmentationMode.Key => SegmentByKey(input, context),
            SegmentationMode.Clock => SegmentByClock(input, context),
            SegmentationMode.Custom => SegmentCustom(input, context),
            _ => throw new InvalidOperationException($"Unknown segmentation mode: {_policy.Mode}")
        };

        await foreach (var epochStream in segmented.WithCancellation(context.CancellationToken))
        {
            yield return epochStream;
        }
    }

    private IAsyncEnumerable<IEpochStream<T>> SegmentNone(
        IAsyncEnumerable<T> input,
        IExecutionContext context)
    {
        return SegmentNoneImpl(input);
        
        async IAsyncEnumerable<IEpochStream<T>> SegmentNoneImpl(IAsyncEnumerable<T> input)
        {
            // Pass-through mode: wrap entire input in a single epoch
            yield return new EpochStream<T>(
                EpochVector.FromSingleSource(_policy.SourceId, 1),
                input);
        }
    }

    private async IAsyncEnumerable<IEpochStream<T>> SegmentByCount(
        IAsyncEnumerable<T> input,
        IExecutionContext context)
    {
        if (_policy.ItemsPerEpoch is not { } itemsPerEpoch)
            throw new InvalidOperationException("ItemsPerEpoch must be set for Count mode");

        long sequence = 1;
        await using var enumerator = input.GetAsyncEnumerator(context.CancellationToken);
        
        var hasMore = await enumerator.MoveNextAsync();
        
        while (hasMore)
        {
            var epochSequence = sequence++;
            var itemsInEpoch = 0;
            
            yield return new EpochStream<T>(
                EpochVector.FromSingleSource(_policy.SourceId, epochSequence),
                StreamEpochItems());

            async IAsyncEnumerable<T> StreamEpochItems()
            {
                // Yield current item
                yield return enumerator.Current;
                itemsInEpoch++;

                // Continue until we reach the count limit
                while (itemsInEpoch < itemsPerEpoch && await enumerator.MoveNextAsync())
                {
                    yield return enumerator.Current;
                    itemsInEpoch++;
                }

                // Check if there's more data for next epoch
                if (itemsInEpoch >= itemsPerEpoch)
                {
                    hasMore = await enumerator.MoveNextAsync();
                }
                else
                {
                    hasMore = false;
                }
            }
        }
    }

    private IAsyncEnumerable<IEpochStream<T>> SegmentByKey(
        IAsyncEnumerable<T> input,
        IExecutionContext context)
    {
        if (_policy.KeySelector is not Func<T, object> keySelector)
            throw new InvalidOperationException("KeySelector must be set for Key mode");

        // Use existing EpochSegmenter utility
        // Note: This requires casting through object which isn't ideal
        // but demonstrates integration with existing code
        return EpochSegmenter.SegmentByKey(
            input,
            item => keySelector(item),
            _policy.SourceId,
            new EpochSegmenterConfig
            {
                ExecutionPolicy = _policy.ExecutionPolicy,
                MaxConcurrentEpochs = _policy.MaxConcurrentEpochs
            },
            context.CancellationToken);
    }

    private IAsyncEnumerable<IEpochStream<T>> SegmentByClock(
        IAsyncEnumerable<T> input,
        IExecutionContext context)
    {
        if (_policy.Clock is not { } clock)
            throw new InvalidOperationException("Clock must be set for Clock mode");

        // Use existing EpochSegmenter utility
        return EpochSegmenter.SegmentByEpoch(
            input,
            clock,
            new EpochSegmenterConfig
            {
                ExecutionPolicy = _policy.ExecutionPolicy,
                MaxConcurrentEpochs = _policy.MaxConcurrentEpochs
            },
            context.CancellationToken);
    }

    private IAsyncEnumerable<IEpochStream<T>> SegmentCustom(
        IAsyncEnumerable<T> input,
        IExecutionContext context)
    {
        if (_policy.CustomSegmenter is not Func<IAsyncEnumerable<T>, IAsyncEnumerable<IEpochStream<T>>> customSegmenter)
            throw new InvalidOperationException("CustomSegmenter must be set for Custom mode");

        return customSegmenter(input);
    }
}
