namespace EpochAnchoringDemo.Core;

using DataFlow.POC.Core;

/// <summary>
/// Observer interface for epoch lifecycle events.
/// Allows components to react to epoch creation, completion, and alignment events.
/// </summary>
public interface IEpochLifecycleObserver
{
    /// <summary>
    /// Called when a new epoch is created/started.
    /// </summary>
    /// <param name="epoch">The epoch that was created</param>
    Task OnEpochCreatedAsync(EpochVector epoch);

    /// <summary>
    /// Called when an epoch completes (all data processed locally).
    /// </summary>
    /// <param name="epoch">The epoch that completed</param>
    Task OnEpochCompletedAsync(EpochVector epoch);

    /// <summary>
    /// Called when all blocks reach a shared completion watermark (global alignment).
    /// This represents a potential checkpoint boundary - when all blocks have completed
    /// processing through a specific epoch, a consistent graph-level snapshot could be captured.
    /// Note: This demo only handles source anchors; full checkpoint implementation would
    /// aggregate state from all blocks at this alignment point.
    /// </summary>
    /// <param name="watermark">The global completion watermark</param>
    Task OnGlobalEpochAlignedAsync(EpochVector watermark);
}

/// <summary>
/// Base class for epoch lifecycle observers with optional overrides.
/// </summary>
public abstract class EpochLifecycleObserverBase : IEpochLifecycleObserver
{
    public virtual Task OnEpochCreatedAsync(EpochVector epoch) => Task.CompletedTask;
    
    public virtual Task OnEpochCompletedAsync(EpochVector epoch) => Task.CompletedTask;
    
    public virtual Task OnGlobalEpochAlignedAsync(EpochVector watermark) => Task.CompletedTask;
}

/// <summary>
/// Manages registration and notification of epoch lifecycle observers.
/// </summary>
public sealed class EpochLifecycleNotifier
{
    private readonly List<IEpochLifecycleObserver> _observers = new();
    private readonly object _lock = new();

    /// <summary>
    /// Registers an observer for epoch lifecycle events.
    /// </summary>
    public void RegisterObserver(IEpochLifecycleObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (_lock)
        {
            _observers.Add(observer);
        }
    }

    /// <summary>
    /// Notifies all observers that an epoch was created.
    /// </summary>
    public async Task NotifyEpochCreatedAsync(EpochVector epoch)
    {
        ArgumentNullException.ThrowIfNull(epoch);
        IEpochLifecycleObserver[] observers;
        lock (_lock)
        {
            observers = _observers.ToArray();
        }

        foreach (var observer in observers)
        {
            await observer.OnEpochCreatedAsync(epoch);
        }
    }

    /// <summary>
    /// Notifies all observers that an epoch completed.
    /// </summary>
    public async Task NotifyEpochCompletedAsync(EpochVector epoch)
    {
        ArgumentNullException.ThrowIfNull(epoch);
        IEpochLifecycleObserver[] observers;
        lock (_lock)
        {
            observers = _observers.ToArray();
        }

        foreach (var observer in observers)
        {
            await observer.OnEpochCompletedAsync(epoch);
        }
    }

    /// <summary>
    /// Notifies all observers that global alignment was reached.
    /// </summary>
    public async Task NotifyGlobalEpochAlignedAsync(EpochVector watermark)
    {
        ArgumentNullException.ThrowIfNull(watermark);
        IEpochLifecycleObserver[] observers;
        lock (_lock)
        {
            observers = _observers.ToArray();
        }

        foreach (var observer in observers)
        {
            await observer.OnGlobalEpochAlignedAsync(watermark);
        }
    }
}
