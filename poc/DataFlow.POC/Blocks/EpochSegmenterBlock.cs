namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;

/// <summary>
/// Block that applies epoch segmentation to continuous data streams.
/// This separates epoch concerns from source blocks, allowing sources to emit
/// plain IAsyncEnumerable&lt;T&gt; while segmentation is applied externally.
/// </summary>
/// <typeparam name="T">The type of items to segment</typeparam>
public sealed class EpochSegmenterBlock<T> : BlockBase<T, IEpochStream<T>>
{
    private readonly EpochSegmentationPolicy _policy;

    /// <summary>
    /// Legacy constructor for inline graph building.
    /// Prefer using the constructor with IBlockContext via DI registration.
    /// </summary>
    [Obsolete("Use the constructor with IBlockContext parameter via services.AddDataFlows(). This constructor will be removed in a future version.")]
    public EpochSegmenterBlock(string name, EpochSegmentationPolicy policy)
        : base(name)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
    /// All dependencies are passed via constructor.
    /// </summary>
    public EpochSegmenterBlock(IBlockContext context, EpochSegmentationPolicy policy)
        : base(context)
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
            SegmentationMode.Time => throw new NotImplementedException("Time-based segmentation is not yet implemented"),
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
