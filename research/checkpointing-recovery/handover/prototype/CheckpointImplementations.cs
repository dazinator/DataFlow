namespace DataFlow.POC.Checkpointing;

/// <summary>
/// Concrete implementation of a checkpoint.
/// </summary>
internal sealed class Checkpoint : ICheckpoint
{
    public required string CheckpointId { get; init; }
    public required Core.EpochVector EpochVector { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required IReadOnlyDictionary<string, byte[]> BlockStates { get; init; }
}

/// <summary>
/// Strategy that creates a checkpoint every N epochs.
/// </summary>
public sealed class EveryNEpochsStrategy : ICheckpointStrategy
{
    private readonly int _n;
    private int _counter = 0;
    private readonly object _lock = new();
    
    public EveryNEpochsStrategy(int n)
    {
        if (n <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(n), "N must be greater than 0");
        }
        _n = n;
    }
    
    public bool ShouldCreateCheckpoint(Core.EpochVector epochVector)
    {
        lock (_lock)
        {
            _counter++;
            if (_counter >= _n)
            {
                _counter = 0;
                return true;
            }
            return false;
        }
    }
}

/// <summary>
/// Strategy that creates checkpoints at specified time intervals.
/// </summary>
public sealed class TimeBasedStrategy : ICheckpointStrategy
{
    private readonly TimeSpan _interval;
    private DateTimeOffset _lastCheckpoint = DateTimeOffset.MinValue;
    private readonly object _lock = new();
    
    public TimeBasedStrategy(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero");
        }
        _interval = interval;
    }
    
    public bool ShouldCreateCheckpoint(Core.EpochVector epochVector)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastCheckpoint >= _interval)
            {
                _lastCheckpoint = now;
                return true;
            }
            return false;
        }
    }
}

/// <summary>
/// In-memory checkpoint store for testing and development.
/// Not suitable for production use as checkpoints are lost on process restart.
/// </summary>
public sealed class InMemoryCheckpointStore : ICheckpointStore
{
    private readonly Dictionary<string, ICheckpoint> _checkpoints = new();
    private readonly object _lock = new();
    
    public Task SaveCheckpointAsync(ICheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        
        lock (_lock)
        {
            _checkpoints[checkpoint.CheckpointId] = checkpoint;
        }
        
        return Task.CompletedTask;
    }
    
    public Task<ICheckpoint?> GetLatestCheckpointAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_checkpoints.Count == 0)
            {
                return Task.FromResult<ICheckpoint?>(null);
            }
            
            var latest = _checkpoints.Values
                .OrderByDescending(c => c.Timestamp)
                .First();
            
            return Task.FromResult<ICheckpoint?>(latest);
        }
    }
    
    public Task<ICheckpoint?> GetCheckpointAsync(string checkpointId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpointId);
        
        lock (_lock)
        {
            _checkpoints.TryGetValue(checkpointId, out var checkpoint);
            return Task.FromResult(checkpoint);
        }
    }
    
    public Task DeleteCheckpointAsync(string checkpointId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpointId);
        
        lock (_lock)
        {
            _checkpoints.Remove(checkpointId);
        }
        
        return Task.CompletedTask;
    }
    
    public Task<IReadOnlyList<string>> ListCheckpointsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var ids = _checkpoints.Values
                .OrderByDescending(c => c.Timestamp)
                .Select(c => c.CheckpointId)
                .ToList();
            
            return Task.FromResult<IReadOnlyList<string>>(ids);
        }
    }
    
    /// <summary>
    /// Clears all checkpoints (for testing).
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _checkpoints.Clear();
        }
    }
}
