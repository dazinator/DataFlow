namespace DataFlow.POC.Core;

using System.Collections.Concurrent;

/// <summary>
/// Tracks epoch completion based on data stream completion rather than broadcast timing.
/// Epochs are marked complete only after all data has been drained from the stream,
/// ensuring accurate progress tracking and safe checkpoint boundaries.
/// </summary>
public sealed class CompletionBasedEpochProgress
{
    private readonly ConcurrentDictionary<EpochVector, EpochState> _epochStates = new();
    private readonly object _lock = new();

    /// <summary>
    /// Registers that an epoch stream has been started.
    /// </summary>
    public void RegisterEpochStarted(EpochVector epoch)
    {
        ArgumentNullException.ThrowIfNull(epoch);
        _epochStates.TryAdd(epoch, new EpochState(IsStarted: true, IsCompleted: false, CompletedAt: null));
    }

    /// <summary>
    /// Marks an epoch stream as completed (all data drained).
    /// </summary>
    public void RegisterEpochCompleted(EpochVector epoch)
    {
        ArgumentNullException.ThrowIfNull(epoch);
        
        _epochStates.AddOrUpdate(
            epoch,
            _ => new EpochState(IsStarted: true, IsCompleted: true, CompletedAt: DateTime.UtcNow),
            (_, state) => new EpochState(IsStarted: true, IsCompleted: true, CompletedAt: DateTime.UtcNow));
    }

    /// <summary>
    /// Checks if an epoch has been completed (all data processed).
    /// </summary>
    public bool IsEpochCompleted(EpochVector epoch)
    {
        ArgumentNullException.ThrowIfNull(epoch);
        return _epochStates.TryGetValue(epoch, out var state) && state.IsCompleted;
    }

    /// <summary>
    /// Gets all completed epochs.
    /// </summary>
    public IReadOnlyList<EpochVector> GetCompletedEpochs()
    {
        return _epochStates
            .Where(kvp => kvp.Value.IsCompleted)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>
    /// Gets all in-progress epochs (started but not completed).
    /// </summary>
    public IReadOnlyList<EpochVector> GetInProgressEpochs()
    {
        return _epochStates
            .Where(kvp => kvp.Value.IsStarted && !kvp.Value.IsCompleted)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>
    /// Gets the highest completed epoch vector (element-wise maximum across all sources for single block).
    /// Returns None if no epochs have been completed.
    /// For a single block tracking its own progress, this returns the highest epoch sequence completed.
    /// </summary>
    public EpochVector GetHighestCompletedEpoch()
    {
        var completed = GetCompletedEpochs();
        if (completed.Count == 0)
        {
            return EpochVector.None;
        }

        // Find the maximum sequence for each source across all completed epochs
        var allSources = completed
            .SelectMany(e => e.Sequences.Keys)
            .Distinct()
            .ToList();

        var maxSequences = new Dictionary<string, long>();
        
        foreach (var source in allSources)
        {
            var sequences = completed
                .Select(e => e.GetSequence(source))
                .Where(s => s >= 0)
                .ToList();

            if (sequences.Count > 0)
            {
                maxSequences[source] = sequences.Max();
            }
        }

        return EpochVector.FromSources(maxSequences);
    }

    /// <summary>
    /// Clears completed epochs up to and including the specified vector.
    /// This can be used for cleanup after checkpointing.
    /// </summary>
    public int ClearCompletedEpochsUpTo(EpochVector maxEpoch)
    {
        ArgumentNullException.ThrowIfNull(maxEpoch);
        
        var toRemove = _epochStates
            .Where(kvp => kvp.Value.IsCompleted && kvp.Key.IsLessThanOrEqual(maxEpoch))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var epoch in toRemove)
        {
            _epochStates.TryRemove(epoch, out _);
        }

        return toRemove.Count;
    }

    /// <summary>
    /// Gets statistics about epoch progress.
    /// </summary>
    public EpochProgressStatistics GetStatistics()
    {
        var states = _epochStates.ToArray();
        var completed = states.Count(s => s.Value.IsCompleted);
        var inProgress = states.Count(s => s.Value.IsStarted && !s.Value.IsCompleted);

        return new EpochProgressStatistics
        {
            TotalEpochs = states.Length,
            CompletedEpochs = completed,
            InProgressEpochs = inProgress,
            HighestCompletedEpoch = GetHighestCompletedEpoch()
        };
    }

    private sealed record EpochState(bool IsStarted, bool IsCompleted, DateTime? CompletedAt);
}

/// <summary>
/// Statistics about epoch progress.
/// </summary>
public sealed class EpochProgressStatistics
{
    public int TotalEpochs { get; init; }
    public int CompletedEpochs { get; init; }
    public int InProgressEpochs { get; init; }
    public EpochVector HighestCompletedEpoch { get; init; } = EpochVector.None;
}

/// <summary>
/// Tracks alignment across multiple blocks using completion-based progress.
/// </summary>
public sealed class GlobalEpochAlignment
{
    private readonly ConcurrentDictionary<string, CompletionBasedEpochProgress> _blockProgress = new();

    /// <summary>
    /// Registers progress for a specific block.
    /// </summary>
    public CompletionBasedEpochProgress GetOrCreateBlockProgress(string blockName)
    {
        ArgumentNullException.ThrowIfNull(blockName);
        return _blockProgress.GetOrAdd(blockName, _ => new CompletionBasedEpochProgress());
    }

    /// <summary>
    /// Checks if all blocks have completed processing for a specific epoch.
    /// </summary>
    public bool IsGloballyAligned(EpochVector epoch)
    {
        ArgumentNullException.ThrowIfNull(epoch);
        
        if (_blockProgress.IsEmpty)
        {
            return false;
        }

        return _blockProgress.Values.All(progress => progress.IsEpochCompleted(epoch));
    }

    /// <summary>
    /// Gets the highest epoch that all blocks have completed.
    /// This is the safe checkpoint boundary.
    /// </summary>
    public EpochVector GetGlobalCompletionWatermark()
    {
        if (_blockProgress.IsEmpty)
        {
            return EpochVector.None;
        }

        // Get each block's highest completed epoch
        var blockHighWatermarks = _blockProgress.Values
            .Select(p => p.GetHighestCompletedEpoch())
            .Where(e => !e.Equals(EpochVector.None))
            .ToList();

        if (blockHighWatermarks.Count == 0)
        {
            return EpochVector.None;
        }

        // Global watermark is the minimum across all blocks for each source
        var allSources = blockHighWatermarks
            .SelectMany(e => e.Sequences.Keys)
            .Distinct()
            .ToList();

        var minSequences = new Dictionary<string, long>();

        foreach (var source in allSources)
        {
            var sequences = blockHighWatermarks
                .Select(e => e.GetSequence(source))
                .Where(s => s >= 0)
                .ToList();

            if (sequences.Count > 0)
            {
                minSequences[source] = sequences.Min();
            }
        }

        return EpochVector.FromSources(minSequences);
    }

    /// <summary>
    /// Gets alignment statistics across all blocks.
    /// </summary>
    public GlobalAlignmentStatistics GetStatistics()
    {
        var blockStats = _blockProgress
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.GetStatistics());

        return new GlobalAlignmentStatistics
        {
            BlockCount = _blockProgress.Count,
            BlockStatistics = blockStats,
            GlobalCompletionWatermark = GetGlobalCompletionWatermark()
        };
    }
}

/// <summary>
/// Statistics about global epoch alignment.
/// </summary>
public sealed class GlobalAlignmentStatistics
{
    public int BlockCount { get; init; }
    public Dictionary<string, EpochProgressStatistics> BlockStatistics { get; init; } = new();
    public EpochVector GlobalCompletionWatermark { get; init; } = EpochVector.None;
}
