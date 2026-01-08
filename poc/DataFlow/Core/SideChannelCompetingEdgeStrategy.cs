namespace DataFlow.POC.Core;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

/// <summary>
/// Competing edge strategy with dedicated side-channel for control signals.
/// Ensures reliable control signal delivery to all competing consumers while maintaining
/// competing semantics for data items.
/// 
/// Architecture:
/// - Data items: Single shared channel (competing semantics - one consumer per item)
/// - Control signals: Individual channels per consumer (broadcast semantics - all receive)
/// - Consumers: Receive merged stream combining their data + control channels
/// 
/// This solves the problem where control signals (e.g., CheckpointBarrier, Heartbeat) 
/// need to reach all consumers for coordinated processing, but data items should compete.
/// </summary>
public class SideChannelCompetingEdgeStrategy : EdgeStrategy
{
    /// <summary>
    /// Creates a side-channel competing edge strategy.
    /// </summary>
    public SideChannelCompetingEdgeStrategy(
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
        : base(EdgeType.Competing, bufferMode, bufferCapacity)
    {
    }

    public override (Dictionary<IBlock, object> writers, Dictionary<IBlock, object> readers) CreateTypedChannels(
        Type dataType,
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks)
    {
        // Data type must be IDataEnvelope for this strategy
        if (dataType != typeof(IDataEnvelope))
        {
            throw new InvalidOperationException(
                $"SideChannelCompetingEdgeStrategy requires IDataEnvelope but received {dataType.Name}");
        }

        var writers = new Dictionary<IBlock, object>();
        var readers = new Dictionary<IBlock, object>();

        // Create the shared data channel for competing data items
        var (sharedDataWriter, sharedDataReader) = TypedChannelFactory.CreateTypedChannel(
            typeof(IDataEnvelope),
            BufferMode,
            BufferCapacity,
            singleReader: false, // Multiple readers compete
            singleWriter: false);

        // Create individual control channels for each consumer (broadcast semantics)
        // Control channels have smaller capacity (5) as control signals are infrequent
        // and prioritized, with single reader optimization
        var controlChannels = new Dictionary<IBlock, (object writer, object reader)>();
        const int controlChannelCapacity = 5; // Small capacity for infrequent control signals
        
        foreach (var target in targetBlocks)
        {
            var (controlWriter, controlReader) = TypedChannelFactory.CreateTypedChannel(
                typeof(IDataEnvelope),
                BufferMode.Bounded, // Always bounded for control channels
                controlChannelCapacity,
                singleReader: true,  // Each consumer has their own control channel
                singleWriter: false); // Composite writer broadcasts to all control channels

            controlChannels[target] = (controlWriter, controlReader);
        }

        // Create a composite writer that routes items based on type (data vs control)
        var compositeWriter = new SideChannelCompositeWriter(
            sharedDataWriter,
            controlChannels.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.writer));

        // For each consumer, create a merged reader combining data + control channels
        foreach (var target in targetBlocks)
        {
            // All consumers share the same composite writer (routes internally)
            writers[target] = compositeWriter;

            // Each consumer gets a merged reader of shared data + their control channel
            var mergedReader = new SideChannelMergedReader(
                sharedDataReader,
                controlChannels[target].reader);
            
            readers[target] = mergedReader;
        }

        return (writers, readers);
    }

    public override async Task RouteTypedItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        // Get the composite writer (all consumers share the same composite writer)
        if (typedWriters.Count == 0)
        {
            return;
        }

        // All entries point to the same composite writer, so just get the first one
        var compositeWriter = typedWriters.Values.First();
        
        // The composite writer will handle routing internally
        await compositeWriter.WriteAsync(item, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Composite channel writer that routes data items to shared channel
/// and control signals to all individual control channels.
/// </summary>
internal class SideChannelCompositeWriter : ChannelWriter<IDataEnvelope>
{
    private readonly object _sharedDataWriter;
    private readonly Dictionary<IBlock, object> _controlWriters;
    private readonly ChannelWriter<IDataEnvelope> _typedSharedDataWriter;
    private readonly Dictionary<IBlock, ChannelWriter<IDataEnvelope>> _typedControlWriters;

    public SideChannelCompositeWriter(
        object sharedDataWriter,
        Dictionary<IBlock, object> controlWriters)
    {
        _sharedDataWriter = sharedDataWriter ?? throw new ArgumentNullException(nameof(sharedDataWriter));
        _controlWriters = controlWriters ?? throw new ArgumentNullException(nameof(controlWriters));

        // Cast to typed writers for efficient writing
        _typedSharedDataWriter = (ChannelWriter<IDataEnvelope>)sharedDataWriter;
        _typedControlWriters = controlWriters.ToDictionary(
            kvp => kvp.Key,
            kvp => (ChannelWriter<IDataEnvelope>)kvp.Value);
    }

    public override bool TryWrite(IDataEnvelope item)
    {
        if (item.IsControlSignal())
        {
            // Control signal: write to all control channels
            // Use all-or-nothing semantics for consistency
            var results = _typedControlWriters.Values.Select(w => w.TryWrite(item)).ToList();
            return results.All(r => r);
        }
        else
        {
            // Data item: write to shared channel
            return _typedSharedDataWriter.TryWrite(item);
        }
    }

    public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
    {
        // For simplicity, wait on the shared data writer
        // In practice, control channels should have sufficient capacity
        return _typedSharedDataWriter.WaitToWriteAsync(cancellationToken);
    }

    public override async ValueTask WriteAsync(IDataEnvelope item, CancellationToken cancellationToken = default)
    {
        if (item.IsControlSignal())
        {
            // Control signal: broadcast to all control channels concurrently
            var writeTasks = _typedControlWriters.Values
                .Select(w => w.WriteAsync(item, cancellationToken).AsTask())
                .ToArray();
            
            await Task.WhenAll(writeTasks).ConfigureAwait(false);
        }
        else
        {
            // Data item: write to shared channel
            await _typedSharedDataWriter.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        }
    }

    public override bool TryComplete(Exception? error = null)
    {
        // Complete both data and control channels
        ReflectionHelper.CompleteTypedWriter(_sharedDataWriter, error);
        
        foreach (var controlWriter in _controlWriters.Values)
        {
            ReflectionHelper.CompleteTypedWriter(controlWriter, error);
        }
        
        return true;
    }
}

/// <summary>
/// Merged channel reader that combines data from shared data channel
/// and control signals from individual control channel.
/// Implements IAsyncEnumerable adapter pattern for transparent consumption.
/// </summary>
internal class SideChannelMergedReader : ChannelReader<IDataEnvelope>
{
    private readonly object _sharedDataReader;
    private readonly object _controlReader;
    private readonly ChannelReader<IDataEnvelope> _typedSharedDataReader;
    private readonly ChannelReader<IDataEnvelope> _typedControlReader;

    public SideChannelMergedReader(object sharedDataReader, object controlReader)
    {
        _sharedDataReader = sharedDataReader ?? throw new ArgumentNullException(nameof(sharedDataReader));
        _controlReader = controlReader ?? throw new ArgumentNullException(nameof(controlReader));

        _typedSharedDataReader = (ChannelReader<IDataEnvelope>)sharedDataReader;
        _typedControlReader = (ChannelReader<IDataEnvelope>)controlReader;
    }

    public override bool TryRead(out IDataEnvelope item)
    {
        // Try control channel first (priority for control signals)
        if (_typedControlReader.TryRead(out var controlItem))
        {
            item = controlItem;
            return true;
        }

        // Then try data channel
        if (_typedSharedDataReader.TryRead(out var dataItem))
        {
            item = dataItem;
            return true;
        }

        item = default!;
        return false;
    }

    public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
    {
        // Wait for either channel to have data
        // This is a simplified implementation; a more sophisticated version could use WhenAny
        var controlTask = _typedControlReader.WaitToReadAsync(cancellationToken);
        var dataTask = _typedSharedDataReader.WaitToReadAsync(cancellationToken);
        
        // Return the first one that completes successfully
        return new ValueTask<bool>(WaitForEitherAsync(controlTask, dataTask, cancellationToken));
    }

    private static async Task<bool> WaitForEitherAsync(
        ValueTask<bool> task1,
        ValueTask<bool> task2,
        CancellationToken cancellationToken)
    {
        var t1 = task1.AsTask();
        var t2 = task2.AsTask();
        
        var completed = await Task.WhenAny(t1, t2).ConfigureAwait(false);
        
        // If the completed task returned true, return true
        if (completed.IsCompletedSuccessfully && await completed.ConfigureAwait(false))
        {
            return true;
        }
        
        // Otherwise wait for the other task
        var other = completed == t1 ? t2 : t1;
        return await other.ConfigureAwait(false);
    }

    public override async ValueTask<IDataEnvelope> ReadAsync(CancellationToken cancellationToken = default)
    {
        // Check control channel first (priority)
        if (_typedControlReader.TryRead(out var controlItem))
        {
            return controlItem;
        }

        // Check data channel
        if (_typedSharedDataReader.TryRead(out var dataItem))
        {
            return dataItem;
        }

        // Wait for either channel to have data
        await WaitToReadAsync(cancellationToken).ConfigureAwait(false);

        // Try again in priority order
        if (_typedControlReader.TryRead(out controlItem))
        {
            return controlItem;
        }

        return await _typedSharedDataReader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Provides a merged async enumerable that interleaves control and data items.
    /// Control signals are given priority to ensure timely delivery.
    /// </summary>
    public override IAsyncEnumerable<IDataEnvelope> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return ReadAllAsyncImpl(cancellationToken);
    }

    private async IAsyncEnumerable<IDataEnvelope> ReadAllAsyncImpl(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Use bounded channel for merge to maintain backpressure
        // Capacity should be sufficient to avoid blocking while forwarding
        var mergeChannel = Channel.CreateBounded<IDataEnvelope>(new BoundedChannelOptions(100)
        {
            SingleReader = true,  // Only this async enumerable reads from merge channel
            SingleWriter = false, // Both control and data tasks write to it
            FullMode = BoundedChannelFullMode.Wait // Wait if full to maintain backpressure
        });
        var mergeWriter = mergeChannel.Writer;

        // Forward control signals (priority channel) - no Task.Run needed for async operations
        var controlTask = ForwardStreamToChannelAsync(
            _typedControlReader.ReadAllAsync(cancellationToken),
            mergeWriter,
            cancellationToken);

        // Forward data items - no Task.Run needed for async operations
        var dataTask = ForwardStreamToChannelAsync(
            _typedSharedDataReader.ReadAllAsync(cancellationToken),
            mergeWriter,
            cancellationToken);

        // Complete writer when both tasks finish
        // Note: This runs in the background and completes after the enumeration finishes
        _ = CompleteWriterWhenBothTasksFinishAsync(controlTask, dataTask, mergeWriter);

        // Read from the merged channel
        await foreach (var item in mergeChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Helper method to forward items from a stream to a channel writer.
    /// </summary>
    private static async Task ForwardStreamToChannelAsync(
        IAsyncEnumerable<IDataEnvelope> stream,
        ChannelWriter<IDataEnvelope> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
    }

    /// <summary>
    /// Helper method to complete a channel writer when both forwarding tasks finish.
    /// </summary>
    private static async Task CompleteWriterWhenBothTasksFinishAsync(
        Task task1,
        Task task2,
        ChannelWriter<IDataEnvelope> writer)
    {
        try
        {
            await Task.WhenAll(task1, task2).ConfigureAwait(false);
            writer.Complete();
        }
        catch (Exception ex)
        {
            writer.Complete(ex);
        }
    }

    public override bool CanCount => false;
    public override bool CanPeek => false;
    public override int Count => throw new NotSupportedException();
    public override bool TryPeek(out IDataEnvelope item)
    {
        item = default!;
        return false;
    }
}
