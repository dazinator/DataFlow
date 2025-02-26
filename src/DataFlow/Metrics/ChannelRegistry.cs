// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Metrics;

using System.Collections.Concurrent;

// Channel registry responsible for tracking and managing monitored channels
public class ChannelRegistry : IDisposable
{
    // Thread-safe collection of all monitored channels
    private ConcurrentBag<WeakReference<IMonitoredChannel>> _monitoredChannels =
        new ConcurrentBag<WeakReference<IMonitoredChannel>>();

    // ReaderWriterLock for registration operations
    private readonly ReaderWriterLockSlim _registrationLock =
        new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

    // Background timer for cleanup
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    public ChannelRegistry()
    {
        // Set up a timer to ensure cleanup happens even if metrics aren't collected
        _cleanupTimer = new Timer(
            _ => PerformFullCleanup(),
            null,
            _cleanupInterval,
            _cleanupInterval);
    }

    public void RegisterChannel(IMonitoredChannel channel)
    {
        _registrationLock.EnterReadLock();
        try
        {
            _monitoredChannels.Add(new WeakReference<IMonitoredChannel>(channel));
        }
        finally
        {
            _registrationLock.ExitReadLock();
        }
    }

    public IEnumerable<ChannelMetricSnapshot> GetChannelSnapshots()
    {
        // Use reader lock to safely iterate
        _registrationLock.EnterReadLock();
        try
        {
            var snapshots = new List<ChannelMetricSnapshot>();

            // Process all channels
            foreach (var weakRef in _monitoredChannels)
            {
                if (weakRef.TryGetTarget(out var channel))
                {
                    var snapshot = channel.GetMetricSnapshot();
                    if (snapshot != null)
                    {
                        snapshots.Add(snapshot);
                    }
                }
            }

            return snapshots;
        }
        finally
        {
            _registrationLock.ExitReadLock();
        }
    }

    public int GetActiveChannelCount()
    {
        _registrationLock.EnterReadLock();
        try
        {
            int count = 0;
            foreach (var weakRef in _monitoredChannels)
            {
                if (weakRef.TryGetTarget(out var channel) && channel.GetMetricSnapshot() != null)
                {
                    count++;
                }
            }
            return count;
        }
        finally
        {
            _registrationLock.ExitReadLock();
        }
    }

    internal void PerformFullCleanup()
    {
        // Use TryEnterWriteLock to avoid deadlocks
        if (_registrationLock.TryEnterWriteLock(1000))
        {
            try
            {
                var newCollection = new ConcurrentBag<WeakReference<IMonitoredChannel>>();

                foreach (var weakRef in _monitoredChannels)
                {
                    if (weakRef.TryGetTarget(out var channel) && channel.GetMetricSnapshot() != null)
                    {
                        newCollection.Add(weakRef);
                    }
                }

                Interlocked.Exchange(ref _monitoredChannels, newCollection);
            }
            finally
            {
                _registrationLock.ExitWriteLock();
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
        _registrationLock.Dispose();
    }
}
