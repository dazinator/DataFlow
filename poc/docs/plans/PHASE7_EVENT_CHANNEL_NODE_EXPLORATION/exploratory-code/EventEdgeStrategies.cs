namespace DataFlow.POC.Core;

using System.Threading.Channels;

/// <summary>
/// Edge strategy for sequential event delivery.
/// Events are delivered to subscribers one at a time in order, ensuring
/// ordered processing suitable for transaction boundaries.
/// </summary>
public class SequentialEventEdgeStrategy : EdgeStrategy
{
    public SequentialEventEdgeStrategy(BufferMode bufferMode = BufferMode.Bounded, int bufferCapacity = 100)
        : base(EdgeType.Event, bufferMode, bufferCapacity)
    {
    }

    public override (Dictionary<IBlock, object> writers, Dictionary<IBlock, object> readers) CreateTypedChannels(
        Type dataType,
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks)
    {
        var writers = new Dictionary<IBlock, object>();
        var readers = new Dictionary<IBlock, object>();

        foreach (var target in targetBlocks)
        {
            var (writer, reader) = TypedChannelFactory.CreateTypedChannel(
                dataType,
                BufferMode,
                BufferCapacity,
                singleReader: true,
                singleWriter: false);
            
            writers[target] = writer;
            readers[target] = reader;
        }

        return (writers, readers);
    }

    /// <summary>
    /// Routes an event to all target channels sequentially.
    /// Each subscriber receives the event in order, one at a time.
    /// This ensures ordered processing suitable for critical events like epoch lifecycle.
    /// </summary>
    public override async Task RouteTypedItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        // Sequential delivery - write to each channel one at a time
        foreach (var writer in typedWriters.Values)
        {
            await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Edge strategy for broadcast event delivery.
/// Events are delivered to all subscribers in parallel, enabling
/// concurrent processing suitable for metrics, logging, and observability.
/// </summary>
public class BroadcastEventEdgeStrategy : EdgeStrategy
{
    public BroadcastEventEdgeStrategy(BufferMode bufferMode = BufferMode.Bounded, int bufferCapacity = 100)
        : base(EdgeType.BroadcastEvent, bufferMode, bufferCapacity)
    {
    }

    public override (Dictionary<IBlock, object> writers, Dictionary<IBlock, object> readers) CreateTypedChannels(
        Type dataType,
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks)
    {
        var writers = new Dictionary<IBlock, object>();
        var readers = new Dictionary<IBlock, object>();

        foreach (var target in targetBlocks)
        {
            var (writer, reader) = TypedChannelFactory.CreateTypedChannel(
                dataType,
                BufferMode,
                BufferCapacity,
                singleReader: true,
                singleWriter: false);
            
            writers[target] = writer;
            readers[target] = reader;
        }

        return (writers, readers);
    }

    /// <summary>
    /// Routes an event to all target channels in parallel.
    /// All subscribers receive the event simultaneously, enabling concurrent processing.
    /// </summary>
    public override async Task RouteTypedItemAsync<T>(
        T item,
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
            await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Multiple writers: write concurrently for parallel delivery
            var writeTasks = new Task[typedWriters.Count];
            int index = 0;
            
            foreach (var writer in typedWriters.Values)
            {
                writeTasks[index++] = writer.WriteAsync(item, cancellationToken).AsTask();
            }
            
            await Task.WhenAll(writeTasks).ConfigureAwait(false);
        }
    }
}
