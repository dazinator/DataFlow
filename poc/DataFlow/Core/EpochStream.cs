namespace DataFlow.POC.Core;

using System.Runtime.CompilerServices;

/// <summary>
/// Represents a stream of items belonging to a specific epoch.
/// Each epoch stream carries its own epoch vector metadata and completes naturally
/// when all data for that epoch has been consumed.
/// </summary>
public interface IEpochStream<out T> : IAsyncDisposable
{
    /// <summary>
    /// The epoch vector identifying this stream's position in the dataflow.
    /// </summary>
    EpochVector Epoch { get; }

    /// <summary>
    /// The data items belonging to this epoch.
    /// The stream completes when all epoch data has been yielded.
    /// </summary>
    IAsyncEnumerable<T> Items { get; }

    /// <summary>
    /// The epoch object with DI scope for this stream.
    /// When using epoch coordinator, multiple streams from different sources may share the SAME epoch object.
    /// Null for streams not created via epoch coordinator.
    /// </summary>
    IEpoch? EpochScope { get; }
}

/// <summary>
/// Concrete implementation of an epoch stream.
/// </summary>
internal sealed class EpochStream<T> : IEpochStream<T>
{
    public EpochVector Epoch { get; }
    public IAsyncEnumerable<T> Items { get; }
    public IEpoch? EpochScope { get; }

    public EpochStream(EpochVector epoch, IAsyncEnumerable<T> items)
    {
        Epoch = epoch ?? throw new ArgumentNullException(nameof(epoch));
        Items = items ?? throw new ArgumentNullException(nameof(items));
        EpochScope = null;
    }

    public EpochStream(IEpoch epochScope, IAsyncEnumerable<T> items)
    {
        EpochScope = epochScope ?? throw new ArgumentNullException(nameof(epochScope));
        Epoch = epochScope.Vector;
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public ValueTask DisposeAsync()
    {
        // Don't dispose epoch scope here - coordinator manages epoch disposal
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Clock interface for tracking current epoch state.
/// </summary>
public interface IEpochClock
{
    /// <summary>
    /// Gets the current epoch vector.
    /// </summary>
    EpochVector CurrentEpochVector { get; }

    /// <summary>
    /// Event raised when the epoch changes.
    /// </summary>
    event EventHandler<EpochChangedEventArgs>? EpochChanged;
}

/// <summary>
/// Event arguments for epoch change notifications.
/// </summary>
public class EpochChangedEventArgs : EventArgs
{
    public EpochVector PreviousEpoch { get; }
    public EpochVector NewEpoch { get; }

    public EpochChangedEventArgs(EpochVector previousEpoch, EpochVector newEpoch)
    {
        PreviousEpoch = previousEpoch ?? throw new ArgumentNullException(nameof(previousEpoch));
        NewEpoch = newEpoch ?? throw new ArgumentNullException(nameof(newEpoch));
    }
}

/// <summary>
/// Simple implementation of an epoch clock that can be manually advanced.
/// </summary>
public sealed class ManualEpochClock : IEpochClock
{
    private EpochVector _currentEpoch = EpochVector.None;
    private readonly object _lock = new();

    public EpochVector CurrentEpochVector
    {
        get
        {
            lock (_lock)
            {
                return _currentEpoch;
            }
        }
    }

    public event EventHandler<EpochChangedEventArgs>? EpochChanged;

    /// <summary>
    /// Advances the epoch for a specific source.
    /// </summary>
    public void AdvanceEpoch(string sourceId)
    {
        ArgumentNullException.ThrowIfNull(sourceId);

        EpochVector previousEpoch;
        EpochVector newEpoch;

        lock (_lock)
        {
            previousEpoch = _currentEpoch;
            newEpoch = _currentEpoch.IncrementSource(sourceId);
            _currentEpoch = newEpoch;
        }

        EpochChanged?.Invoke(this, new EpochChangedEventArgs(previousEpoch, newEpoch));
    }

    /// <summary>
    /// Sets the epoch to a specific vector.
    /// </summary>
    public void SetEpoch(EpochVector epoch)
    {
        ArgumentNullException.ThrowIfNull(epoch);

        EpochVector previousEpoch;

        lock (_lock)
        {
            previousEpoch = _currentEpoch;
            _currentEpoch = epoch;
        }

        EpochChanged?.Invoke(this, new EpochChangedEventArgs(previousEpoch, epoch));
    }
}
