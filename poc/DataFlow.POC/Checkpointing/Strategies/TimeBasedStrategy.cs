namespace DataFlow.POC.Checkpointing.Strategies;

using DataFlow.POC.Core;

/// <summary>
/// Checkpoint strategy that creates checkpoints at specified time intervals.
/// </summary>
public sealed class TimeBasedStrategy : ICheckpointStrategy
{
    private readonly TimeSpan _interval;
    private DateTimeOffset _lastCheckpointTime;
    private readonly object _lock = new();

    public TimeBasedStrategy(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero");
        }

        _interval = interval;
        _lastCheckpointTime = DateTimeOffset.MinValue;
    }

    public bool ShouldCreateCheckpoint(EpochVector epochVector)
    {
        ArgumentNullException.ThrowIfNull(epochVector);

        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            var elapsed = now - _lastCheckpointTime;

            if (elapsed >= _interval)
            {
                _lastCheckpointTime = now;
                return true;
            }

            return false;
        }
    }
}
