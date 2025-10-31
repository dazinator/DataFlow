namespace DataFlow.POC.Core;

using System.Threading.Channels;

/// <summary>
/// Edge strategy for envelope-based delivery with control signal propagation.
/// Implements control signal broadcast semantics:
/// - Control signals are always broadcast to all downstream targets
/// - Data items follow the underlying strategy (broadcast, competing, or routed)
/// 
/// NOTE: For competing edges, this strategy provides "best-effort" control signal delivery.
/// Since competing edges share a single channel, control signals will be delivered to 
/// whichever consumer reads first. For guaranteed control signal delivery to all consumers,
/// use broadcast edges or implement a dedicated control signal channel pattern.
/// </summary>
public class EnvelopeEdgeStrategy : EdgeStrategy
{
    private readonly EdgeStrategy _underlyingStrategy;

    /// <summary>
    /// Creates an envelope edge strategy wrapping an underlying strategy.
    /// </summary>
    /// <param name="underlyingStrategy">The strategy to use for data items</param>
    public EnvelopeEdgeStrategy(EdgeStrategy underlyingStrategy)
        : base(underlyingStrategy.EdgeType, underlyingStrategy.BufferMode, underlyingStrategy.BufferCapacity)
    {
        _underlyingStrategy = underlyingStrategy ?? throw new ArgumentNullException(nameof(underlyingStrategy));
    }

    public override (Dictionary<IBlock, object> writers, Dictionary<IBlock, object> readers) CreateTypedChannels(
        Type dataType,
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks)
    {
        // Create channels for the envelope type (IDataEnvelope)
        // This allows us to pass both data and control signals through the same channels
        return _underlyingStrategy.CreateTypedChannels(typeof(IDataEnvelope), sourceBlock, targetBlocks);
    }

    public override async Task RouteTypedItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        // T should be IDataEnvelope in this case
        if (item is not IDataEnvelope envelope)
        {
            throw new InvalidOperationException(
                $"EnvelopeEdgeStrategy expects IDataEnvelope items but received {typeof(T).Name}");
        }

        // Control signals are always broadcast to all targets
        if (envelope.IsControlSignal())
        {
            await BroadcastControlSignalAsync(envelope, typedWriters, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Data items follow the underlying strategy
            await _underlyingStrategy.RouteTypedItemAsync(item, typedWriters, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Broadcasts a control signal to all target channels.
    /// Control signals must reach all downstream blocks for proper coordination.
    /// </summary>
    private static async Task BroadcastControlSignalAsync<T>(
        IDataEnvelope controlSignal,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        if (typedWriters.Count == 0)
        {
            return;
        }

        if (typedWriters.Count == 1)
        {
            // Optimization: single writer doesn't need Task.WhenAll overhead
            using var enumerator = typedWriters.Values.GetEnumerator();
            enumerator.MoveNext();
            var writer = enumerator.Current;
            await writer.WriteAsync((T)(object)controlSignal, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Multiple writers: broadcast to all concurrently
            var writeTasks = new Task[typedWriters.Count];
            int index = 0;
            
            foreach (var writer in typedWriters.Values.Distinct())
            {
                writeTasks[index++] = writer.WriteAsync((T)(object)controlSignal, cancellationToken).AsTask();
            }
            
            // Trim array if we had duplicate writers (competing strategy)
            if (index < writeTasks.Length)
            {
                Array.Resize(ref writeTasks, index);
            }
            
            await Task.WhenAll(writeTasks).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Factory methods for creating envelope-aware edge strategies.
/// </summary>
public static class EnvelopeEdgeStrategyFactory
{
    /// <summary>
    /// Creates a broadcast envelope edge strategy.
    /// Data items are broadcast to all targets, control signals are always broadcast.
    /// </summary>
    public static EnvelopeEdgeStrategy CreateBroadcast(
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100,
        Func<object, object>? cloneFunc = null)
    {
        var underlyingStrategy = cloneFunc != null
            ? new BroadcastEdgeStrategy(cloneFunc, bufferMode, bufferCapacity)
            : new BroadcastEdgeStrategy(bufferMode, bufferCapacity);
        return new EnvelopeEdgeStrategy(underlyingStrategy);
    }

    /// <summary>
    /// Creates a competing envelope edge strategy.
    /// Data items compete among targets, but control signals are broadcast to all targets.
    /// </summary>
    public static EnvelopeEdgeStrategy CreateCompeting(
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
    {
        var underlyingStrategy = new CompetingEdgeStrategy(bufferMode, bufferCapacity);
        return new EnvelopeEdgeStrategy(underlyingStrategy);
    }

    /// <summary>
    /// Creates a routed envelope edge strategy.
    /// Data items are routed based on route keys, control signals are broadcast to all targets.
    /// </summary>
    public static EnvelopeEdgeStrategy CreateRouted(
        Dictionary<string, IBlock> routeKeyToBlock,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
    {
        var underlyingStrategy = new RoutedItemEdgeStrategy(routeKeyToBlock, bufferMode, bufferCapacity);
        return new EnvelopeEdgeStrategy(underlyingStrategy);
    }
}
