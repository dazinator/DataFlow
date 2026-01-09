namespace DataFlow.POC.Core;

/// <summary>
/// Manages registration and notification of epoch lifecycle participants.
/// Coordinates lifecycle event broadcasting to components that need to react
/// to epoch creation, completion, and global alignment events.
/// </summary>
public sealed class EpochLifecycleCoordinator
{
    private readonly List<IEpochLifecycleParticipant> _participants = new();
    private readonly object _lock = new();

    /// <summary>
    /// Registers a participant for epoch lifecycle events.
    /// </summary>
    public void RegisterParticipant(IEpochLifecycleParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        lock (_lock)
        {
            _participants.Add(participant);
        }
    }

    /// <summary>
    /// Notifies all participants that an epoch was created.
    /// </summary>
    public async ValueTask NotifyEpochCreatedAsync(
        EpochVector epoch, 
        IBlockContext block, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(epoch);
        ArgumentNullException.ThrowIfNull(block);
        
        IEpochLifecycleParticipant[] participants;
        lock (_lock)
        {
            participants = _participants.ToArray();
        }

        foreach (var participant in participants)
        {
            await participant.OnEpochCreatedAsync(epoch, block, cancellationToken);
        }
    }

    /// <summary>
    /// Notifies all participants that an epoch completed.
    /// </summary>
    public async ValueTask NotifyEpochCompletedAsync(
        EpochVector epoch, 
        IBlockContext block, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(epoch);
        ArgumentNullException.ThrowIfNull(block);
        
        IEpochLifecycleParticipant[] participants;
        lock (_lock)
        {
            participants = _participants.ToArray();
        }

        foreach (var participant in participants)
        {
            await participant.OnEpochCompletedAsync(epoch, block, cancellationToken);
        }
    }

    /// <summary>
    /// Notifies all participants that global alignment was reached.
    /// </summary>
    public async ValueTask NotifyGlobalEpochAlignedAsync(
        EpochVector watermark, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(watermark);
        
        IEpochLifecycleParticipant[] participants;
        lock (_lock)
        {
            participants = _participants.ToArray();
        }

        foreach (var participant in participants)
        {
            await participant.OnGlobalEpochAlignedAsync(watermark, cancellationToken);
        }
    }
}
