namespace DataFlow.POC.Core;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

/// <summary>
/// Out-of-Band Epoch Control Plane - Option 1 from next investigation phase.
/// Provides near-zero hot-path overhead by tracking control signals via metadata rather than in-band channels.
/// 
/// Architecture:
/// - Sources assign monotonic sequence numbers to control signals
/// - EpochManager broadcasts {sourceId → seqE} metadata to all blocks
/// - Blocks track lastSeen[sourceId] and acknowledge epochs once all inputs ≥ seqE
/// - Data flows normally through channels without control signal interference
/// </summary>

/// <summary>
/// Represents an epoch marker with source and sequence information.
/// </summary>
public sealed record EpochMarker(string SourceId, long Sequence, DateTime Timestamp)
{
    /// <summary>
    /// Optional metadata associated with this epoch (e.g., checkpoint ID, barrier type).
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Tracks the progress of epoch markers across the dataflow graph.
/// Blocks use this to track which epochs they have seen from each source.
/// </summary>
public class EpochProgress
{
    private readonly ConcurrentDictionary<string, long> _lastSeenSequence = new();

    /// <summary>
    /// Updates the last seen sequence for a source.
    /// Returns true if this is a new highest sequence for this source.
    /// </summary>
    public bool UpdateLastSeen(string sourceId, long sequence)
    {
        return _lastSeenSequence.AddOrUpdate(
            sourceId,
            sequence,
            (_, current) => Math.Max(current, sequence)) == sequence;
    }

    /// <summary>
    /// Gets the last seen sequence for a source, or -1 if not yet seen.
    /// </summary>
    public long GetLastSeen(string sourceId)
    {
        return _lastSeenSequence.TryGetValue(sourceId, out var seq) ? seq : -1;
    }

    /// <summary>
    /// Checks if this block has seen at least the specified sequence from all specified sources.
    /// Used to determine if an epoch can be acknowledged.
    /// </summary>
    public bool HasSeenAllSources(IEnumerable<string> sourceIds, long targetSequence)
    {
        foreach (var sourceId in sourceIds)
        {
            var lastSeen = GetLastSeen(sourceId);
            if (lastSeen < targetSequence)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Gets a snapshot of all last seen sequences.
    /// </summary>
    public Dictionary<string, long> GetSnapshot()
    {
        return new Dictionary<string, long>(_lastSeenSequence);
    }
}

/// <summary>
/// Event arguments for epoch notifications.
/// </summary>
public class EpochEventArgs : EventArgs
{
    public EpochMarker Marker { get; }

    public EpochEventArgs(EpochMarker marker)
    {
        Marker = marker ?? throw new ArgumentNullException(nameof(marker));
    }
}

/// <summary>
/// Event arguments for epoch acknowledgment.
/// </summary>
public class EpochAcknowledgmentEventArgs : EventArgs
{
    public string BlockName { get; }
    public EpochMarker Marker { get; }
    public TimeSpan ProcessingTime { get; }

    public EpochAcknowledgmentEventArgs(string blockName, EpochMarker marker, TimeSpan processingTime)
    {
        BlockName = blockName ?? throw new ArgumentNullException(nameof(blockName));
        Marker = marker ?? throw new ArgumentNullException(nameof(marker));
        ProcessingTime = processingTime;
    }
}

/// <summary>
/// Publisher interface for blocks that emit epochs.
/// Typically source blocks that generate control signals.
/// </summary>
public interface IEpochPublisher
{
    /// <summary>
    /// Event raised when a new epoch is emitted.
    /// </summary>
    event EventHandler<EpochEventArgs>? EpochEmitted;
}

/// <summary>
/// Subscriber interface for blocks that handle epochs.
/// All blocks in the dataflow graph should implement this to participate in epoch coordination.
/// </summary>
public interface IEpochSubscriber
{
    /// <summary>
    /// Notifies the block of a new epoch from a source.
    /// Returns true if the block has now seen this epoch from all its upstream sources.
    /// </summary>
    ValueTask<bool> NotifyEpochAsync(EpochMarker marker, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current epoch progress for this block.
    /// </summary>
    EpochProgress GetEpochProgress();
}

/// <summary>
/// Manages epoch propagation across the dataflow graph.
/// Provides out-of-band control plane for coordinated checkpointing and alignment.
/// </summary>
public class EpochManager : IDisposable
{
    private readonly ConcurrentDictionary<string, IEpochPublisher> _publishers = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<IEpochSubscriber>> _subscribers = new();
    private readonly ConcurrentDictionary<string, long> _nextSequence = new();
    private bool _disposed;

    /// <summary>
    /// Event raised when any block acknowledges an epoch.
    /// Useful for monitoring and debugging epoch propagation.
    /// </summary>
    public event EventHandler<EpochAcknowledgmentEventArgs>? EpochAcknowledged;

    /// <summary>
    /// Registers a publisher block that can emit epochs.
    /// </summary>
    public void RegisterPublisher(string sourceId, IEpochPublisher publisher)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EpochManager));
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(publisher);

        _publishers[sourceId] = publisher;
        _nextSequence.TryAdd(sourceId, 0);

        // Subscribe to publisher's events
        publisher.EpochEmitted += OnEpochEmitted;
    }

    /// <summary>
    /// Registers a subscriber block that handles epochs.
    /// </summary>
    public void RegisterSubscriber(string blockName, IEpochSubscriber subscriber)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EpochManager));
        ArgumentNullException.ThrowIfNull(blockName);
        ArgumentNullException.ThrowIfNull(subscriber);

        _subscribers.AddOrUpdate(
            blockName,
            _ => new ConcurrentBag<IEpochSubscriber> { subscriber },
            (_, bag) =>
            {
                bag.Add(subscriber);
                return bag;
            });
    }

    /// <summary>
    /// Unregisters a subscriber.
    /// Note: Removing from ConcurrentBag is not supported, so this tracks removal intent only.
    /// In practice, subscribers should remain registered for the lifetime of the graph.
    /// </summary>
    public void UnregisterSubscriber(string blockName, IEpochSubscriber subscriber)
    {
        // Note: ConcurrentBag doesn't support removal
        // In practice, subscribers should remain registered for graph lifetime
        // If unregistration is critical, consider using ImmutableList or other collection
    }

    /// <summary>
    /// Broadcasts an epoch marker to all registered subscribers.
    /// This is the core of the out-of-band control plane - epochs flow via events, not channels.
    /// </summary>
    public async ValueTask BroadcastEpochAsync(EpochMarker marker, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EpochManager));
        ArgumentNullException.ThrowIfNull(marker);

        var tasks = new List<Task>();

        // Get snapshot of subscribers (ConcurrentBag iteration is thread-safe)
        foreach (var (blockName, subscribers) in _subscribers)
        {
            foreach (var subscriber in subscribers)
            {
                tasks.Add(NotifyAndAcknowledgeAsync(blockName, subscriber, marker, cancellationToken));
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    private async Task NotifyAndAcknowledgeAsync(
        string blockName,
        IEpochSubscriber subscriber,
        EpochMarker marker,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var acknowledged = await subscriber.NotifyEpochAsync(marker, cancellationToken).ConfigureAwait(false);
            
            if (acknowledged)
            {
                stopwatch.Stop();
                EpochAcknowledged?.Invoke(this, new EpochAcknowledgmentEventArgs(
                    blockName,
                    marker,
                    stopwatch.Elapsed));
            }
        }
        catch (Exception)
        {
            // Log error but don't fail the entire broadcast
            // In production, this should use proper logging
            throw;
        }
    }

    /// <summary>
    /// Creates a new epoch marker with a monotonically increasing sequence number for the source.
    /// </summary>
    public EpochMarker CreateEpochMarker(string sourceId, Dictionary<string, object>? metadata = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EpochManager));
        ArgumentNullException.ThrowIfNull(sourceId);

        var sequence = _nextSequence.AddOrUpdate(sourceId, 1, (_, current) => current + 1);
        
        return new EpochMarker(sourceId, sequence, DateTime.UtcNow)
        {
            Metadata = metadata
        };
    }

    /// <summary>
    /// Event handler for epoch emissions from publishers.
    /// Automatically broadcasts to all subscribers using a background task with proper error handling.
    /// </summary>
    private void OnEpochEmitted(object? sender, EpochEventArgs e)
    {
        // Use Task.Run with proper error handling to avoid unobserved exceptions
        _ = Task.Run(async () =>
        {
            try
            {
                await BroadcastEpochAsync(e.Marker, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // In production, log this error using ILogger
                // For POC, write to debug output
                System.Diagnostics.Debug.WriteLine($"Error broadcasting epoch: {ex.Message}");
                
                // Optionally: raise an event for error handling
                // EpochBroadcastError?.Invoke(this, new EpochErrorEventArgs(e.Marker, ex));
            }
        }, CancellationToken.None);
    }

    /// <summary>
    /// Gets statistics about epoch progression for debugging and monitoring.
    /// </summary>
    public EpochStatistics GetStatistics()
    {
        var sourceSequences = new Dictionary<string, long>(_nextSequence);
        var blockProgress = new Dictionary<string, Dictionary<string, long>>();

        // ConcurrentBag iteration is thread-safe
        foreach (var (blockName, subscribers) in _subscribers)
        {
            // Get progress from first subscriber (all should be similar)
            var firstSubscriber = subscribers.FirstOrDefault();
            if (firstSubscriber != null)
            {
                var progress = firstSubscriber.GetEpochProgress().GetSnapshot();
                blockProgress[blockName] = progress;
            }
        }

        return new EpochStatistics
        {
            SourceSequences = sourceSequences,
            BlockProgress = blockProgress,
            PublisherCount = _publishers.Count,
            SubscriberCount = _subscribers.Count
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Unsubscribe from all publishers
        foreach (var publisher in _publishers.Values)
        {
            publisher.EpochEmitted -= OnEpochEmitted;
        }

        _publishers.Clear();
        _subscribers.Clear();
        _nextSequence.Clear();
    }
}

/// <summary>
/// Statistics about epoch progression for monitoring and debugging.
/// </summary>
public class EpochStatistics
{
    /// <summary>
    /// Current sequence numbers for each source.
    /// </summary>
    public Dictionary<string, long> SourceSequences { get; init; } = new();

    /// <summary>
    /// Last seen sequences for each block from each source.
    /// </summary>
    public Dictionary<string, Dictionary<string, long>> BlockProgress { get; init; } = new();

    /// <summary>
    /// Number of registered publishers.
    /// </summary>
    public int PublisherCount { get; init; }

    /// <summary>
    /// Number of registered subscribers.
    /// </summary>
    public int SubscriberCount { get; init; }

    /// <summary>
    /// Calculates the minimum epoch progress (slowest block for each source).
    /// </summary>
    public Dictionary<string, long> GetMinimumProgress()
    {
        var result = new Dictionary<string, long>();

        foreach (var (source, sequence) in SourceSequences)
        {
            long minProgress = sequence;
            
            foreach (var blockProgress in BlockProgress.Values)
            {
                if (blockProgress.TryGetValue(source, out var progress))
                {
                    minProgress = Math.Min(minProgress, progress);
                }
            }
            
            result[source] = minProgress;
        }

        return result;
    }
}

/// <summary>
/// Edge strategy that uses out-of-band epoch control plane.
/// Data flows through channels normally, epochs propagate via EpochManager.
/// This provides zero hot-path overhead on data operations.
/// </summary>
public class EpochControlPlaneEdgeStrategy : EdgeStrategy
{
    private readonly EpochManager _epochManager;

    public EpochControlPlaneEdgeStrategy(
        EpochManager epochManager,
        EdgeType edgeType = EdgeType.Broadcast,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
        : base(edgeType, bufferMode, bufferCapacity)
    {
        _epochManager = epochManager ?? throw new ArgumentNullException(nameof(epochManager));
    }

    public override (Dictionary<IBlock, object> writers, Dictionary<IBlock, object> readers) CreateTypedChannels(
        Type dataType,
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks)
    {
        // Register epoch wiring
        if (sourceBlock is IEpochPublisher publisher)
        {
            _epochManager.RegisterPublisher(sourceBlock.Name, publisher);
        }

        foreach (var target in targetBlocks)
        {
            if (target is IEpochSubscriber subscriber)
            {
                _epochManager.RegisterSubscriber(target.Name, subscriber);
            }
        }

        // Create normal data channels (epochs bypass these entirely)
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
            throw new NotSupportedException($"EdgeType {EdgeType} not supported in EpochControlPlaneEdgeStrategy");
        }

        return (writers, readers);
    }

    public override async Task RouteTypedItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        // All data items route normally through channels
        // Epochs are handled via EpochManager, completely bypassing this path
        switch (EdgeType)
        {
            case EdgeType.Broadcast:
                await BroadcastItemAsync(item, typedWriters, cancellationToken).ConfigureAwait(false);
                break;
            case EdgeType.Competing:
                await CompeteItemAsync(item, typedWriters, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new NotSupportedException($"EdgeType {EdgeType} not supported");
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
/// Helper factory for creating epoch control plane strategies.
/// </summary>
public static class EpochControlPlaneFactory
{
    public static EpochControlPlaneEdgeStrategy CreateBroadcast(
        EpochManager epochManager,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
    {
        return new EpochControlPlaneEdgeStrategy(
            epochManager,
            EdgeType.Broadcast,
            bufferMode,
            bufferCapacity);
    }

    public static EpochControlPlaneEdgeStrategy CreateCompeting(
        EpochManager epochManager,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
    {
        return new EpochControlPlaneEdgeStrategy(
            epochManager,
            EdgeType.Competing,
            bufferMode,
            bufferCapacity);
    }
}
