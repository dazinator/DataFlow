namespace DataFlow.POC.Checkpointing.Strategies;

using DataFlow.POC.Core;

/// <summary>
/// Checkpoint strategy that creates a checkpoint every N epochs.
/// </summary>
public sealed class EveryNEpochsStrategy : ICheckpointStrategy
{
    private readonly int _n;
    private long _epochCount;

    public EveryNEpochsStrategy(int n)
    {
        if (n <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(n), "N must be greater than zero");
        }

        _n = n;
    }

    public bool ShouldCreateCheckpoint(EpochVector epochVector)
    {
        ArgumentNullException.ThrowIfNull(epochVector);

        // Increment epoch count and check if we should checkpoint
        var currentCount = Interlocked.Increment(ref _epochCount);
        return currentCount % _n == 0;
    }
}
