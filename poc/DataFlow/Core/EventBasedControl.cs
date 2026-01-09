namespace DataFlow.POC.Core;

using System.Collections.Concurrent;
using System.Threading.Channels;

/// <summary>
/// Event-based control signal propagation - Option 3 from exploration.
/// Control signals are delivered via events/callbacks instead of flowing through channels.
/// </summary>

/// <summary>
/// Event args for control signal delivery.
/// </summary>
public class ControlSignalEventArgs : EventArgs
{
    public IControlSignal Signal { get; }
    public DateTime Timestamp { get; }

    public ControlSignalEventArgs(IControlSignal signal)
    {
        Signal = signal ?? throw new ArgumentNullException(nameof(signal));
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Interface for control signals (marker interface for identification).
/// </summary>
public interface IControlSignal
{
}

/// <summary>
/// Control signal implementations for event-based approach.
/// </summary>
public sealed record EventBasedCheckpointBarrier(Guid Id, DateTime CreatedAt) : IControlSignal;
public sealed record EventBasedHeartbeat(DateTime Timestamp) : IControlSignal;

/// <summary>
/// Publisher interface for blocks that emit control signals.
/// </summary>
public interface IControlSignalPublisher
{
    event EventHandler<ControlSignalEventArgs>? ControlSignalEmitted;
}

/// <summary>
/// Subscriber interface for blocks that handle control signals.
/// </summary>
public interface IControlSignalSubscriber
{
    ValueTask HandleControlSignalAsync(IControlSignal signal, CancellationToken cancellationToken);
}

/// <summary>
/// Manager for coordinating control signal delivery across the dataflow graph.
/// Implements out-of-band control plane pattern.
/// </summary>
public class EventBasedControlSignalManager : IDisposable
{
    private readonly ConcurrentDictionary<string, List<IControlSignalSubscriber>> _subscribers = new();
    private readonly ConcurrentDictionary<string, IControlSignalPublisher> _publishers = new();
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Registers a publisher block that can emit control signals.
    /// </summary>
    public void RegisterPublisher(string blockName, IControlSignalPublisher publisher)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EventBasedControlSignalManager));

        _publishers[blockName] = publisher;
        
        // Subscribe to publisher's events
        publisher.ControlSignalEmitted += OnControlSignalEmitted;
    }

    /// <summary>
    /// Registers a subscriber block that handles control signals.
    /// </summary>
    public void RegisterSubscriber(string blockName, IControlSignalSubscriber subscriber)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EventBasedControlSignalManager));

        _subscribers.AddOrUpdate(
            blockName,
            _ => new List<IControlSignalSubscriber> { subscriber },
            (_, list) =>
            {
                lock (_lock)
                {
                    list.Add(subscriber);
                    return list;
                }
            });
    }

    /// <summary>
    /// Unregisters a subscriber.
    /// </summary>
    public void UnregisterSubscriber(string blockName, IControlSignalSubscriber subscriber)
    {
        if (_subscribers.TryGetValue(blockName, out var list))
        {
            lock (_lock)
            {
                list.Remove(subscriber);
            }
        }
    }

    /// <summary>
    /// Broadcasts a control signal to all registered subscribers.
    /// </summary>
    public async ValueTask BroadcastAsync(IControlSignal signal, CancellationToken cancellationToken)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EventBasedControlSignalManager));

        var tasks = new List<Task>();

        foreach (var subscriberList in _subscribers.Values)
        {
            IControlSignalSubscriber[] subscribers;
            lock (_lock)
            {
                subscribers = subscriberList.ToArray();
            }

            foreach (var subscriber in subscribers)
            {
                tasks.Add(subscriber.HandleControlSignalAsync(signal, cancellationToken).AsTask());
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Event handler for control signal emissions from publishers.
    /// </summary>
    private void OnControlSignalEmitted(object? sender, ControlSignalEventArgs e)
    {
        // Fire and forget - async event handling
        _ = BroadcastAsync(e.Signal, CancellationToken.None);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Unsubscribe from all publishers
        foreach (var publisher in _publishers.Values)
        {
            publisher.ControlSignalEmitted -= OnControlSignalEmitted;
        }

        _publishers.Clear();
        _subscribers.Clear();
    }
}

/// <summary>
/// Edge strategy that uses event-based control signal delivery.
/// Data flows through channels normally, control signals via events.
/// </summary>
public class EventBasedControlEdgeStrategy : EdgeStrategy
{
    private readonly EventBasedControlSignalManager _controlManager;

    public EventBasedControlEdgeStrategy(
        EventBasedControlSignalManager controlManager,
        EdgeStrategy dataStrategy)
        : base(dataStrategy.EdgeType, dataStrategy.BufferMode, dataStrategy.BufferCapacity)
    {
        _controlManager = controlManager ?? throw new ArgumentNullException(nameof(controlManager));
    }

    public override (Dictionary<IBlock, object> writers, Dictionary<IBlock, object> readers) CreateTypedChannels(
        Type dataType,
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks)
    {
        // Register control signal wiring
        if (sourceBlock is IControlSignalPublisher publisher)
        {
            _controlManager.RegisterPublisher(sourceBlock.Name, publisher);
        }

        foreach (var target in targetBlocks)
        {
            if (target is IControlSignalSubscriber subscriber)
            {
                _controlManager.RegisterSubscriber(target.Name, subscriber);
            }
        }

        // Create normal data channels (control signals bypass this)
        // Use the same logic as the underlying strategy
        var writers = new Dictionary<IBlock, object>();
        var readers = new Dictionary<IBlock, object>();

        if (EdgeType == EdgeType.Broadcast)
        {
            // Each target gets its own channel
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
        }
        else if (EdgeType == EdgeType.Competing)
        {
            // All targets share a single channel
            var (sharedWriter, sharedReader) = TypedChannelFactory.CreateTypedChannel(
                dataType,
                BufferMode,
                BufferCapacity,
                singleReader: false,
                singleWriter: false);
            
            foreach (var target in targetBlocks)
            {
                writers[target] = sharedWriter;
                readers[target] = sharedReader;
            }
        }
        else
        {
            throw new NotSupportedException($"EdgeType {EdgeType} not supported in EventBasedControlEdgeStrategy");
        }

        return (writers, readers);
    }

    public override async Task RouteTypedItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        // All data items route normally through channels
        // Control signals are handled via events, not channels
        switch (EdgeType)
        {
            case EdgeType.Broadcast:
                await BroadcastItemAsync(item, typedWriters, cancellationToken).ConfigureAwait(false);
                break;
            case EdgeType.Competing:
                await CompeteItemAsync(item, typedWriters, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new NotSupportedException($"EdgeType {EdgeType} not supported in EventBasedControlEdgeStrategy");
        }
    }

    private static async Task BroadcastItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        var tasks = typedWriters.Values
            .Select(w => w.WriteAsync(item, cancellationToken).AsTask())
            .ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task CompeteItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        // Get the first writer (all point to same channel in competing)
        var writer = typedWriters.Values.First();
        await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Helper factory for creating event-based control strategies.
/// </summary>
public static class EventBasedControlStrategyFactory
{
    public static EventBasedControlEdgeStrategy CreateBroadcast(
        EventBasedControlSignalManager controlManager,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
    {
        var dataStrategy = new BroadcastEdgeStrategy(bufferMode, bufferCapacity);
        return new EventBasedControlEdgeStrategy(controlManager, dataStrategy);
    }

    public static EventBasedControlEdgeStrategy CreateCompeting(
        EventBasedControlSignalManager controlManager,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
    {
        var dataStrategy = new CompetingEdgeStrategy(bufferMode, bufferCapacity);
        return new EventBasedControlEdgeStrategy(controlManager, dataStrategy);
    }
}
