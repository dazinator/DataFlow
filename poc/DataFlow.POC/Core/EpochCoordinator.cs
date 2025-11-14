namespace DataFlow.POC.Core;

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Implementation of source-level epoch coordination with:
/// - Bounded epoch growth (one sequence per source per epoch)
/// - Readiness-based advancement (don't wait for completion)
/// - Single-source fast path (no coordination overhead)
/// </summary>
public sealed class EpochCoordinator : IEpochCoordinator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _lock = new();
    
    // Track sources and their readiness state
    private readonly Dictionary<string, SourceReadiness> _sources = new();
    
    // Current active epoch (if any)
    private ActiveEpoch? _activeEpoch;
    
    // All epochs (for disposal tracking)
    private readonly ConcurrentDictionary<EpochVector, IEpoch> _allEpochs = new();
    
    // Task completion sources for sources waiting for readiness
    private readonly Dictionary<string, TaskCompletionSource<IEpoch>> _waitingForReadiness = new();

    private bool _disposed;

    public EpochCoordinator(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public ValueTask<IEpoch> GetOrCreateEpochAsync(
        string sourceId,
        EpochVector vector,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(vector);

        lock (_lock)
        {
            // FAST PATH: Single source optimization (check before registration)
            bool isSingleSource = _sources.Count == 0 || (_sources.Count == 1 && _sources.ContainsKey(sourceId));
            
            // Register source if first time seeing it
            if (!_sources.ContainsKey(sourceId))
            {
                _sources[sourceId] = new SourceReadiness
                {
                    SourceId = sourceId,
                    CurrentVector = EpochVector.None
                };
            }

            if (isSingleSource)
            {
                return ValueTask.FromResult(GetOrCreateEpochUnsafe(vector, sourceId));
            }

            // MULTI-SOURCE PATH: Check if we need coordination
            return ValueTask.FromResult(GetOrCreateEpochWithCoordination(sourceId, vector, cancellationToken));
        }
    }

    private IEpoch GetOrCreateEpochWithCoordination(
        string sourceId,
        EpochVector vector,
        CancellationToken cancellationToken)
    {
        var sourceState = _sources[sourceId];
        
        // Case 1: No active epoch - check if we have an existing epoch from fast path, or create new one
        if (_activeEpoch == null)
        {
            // When transitioning from single to multi-source, there may be an epoch created by the fast path
            // We need to find it and promote it to active epoch, then subsume the new source into it
            if (_allEpochs.Any())
            {
                // There should only be one epoch in single-source mode
                var existingEntry = _allEpochs.First();
                var existingEpoch = existingEntry.Value;
                var existingVector = existingEntry.Key;
                
                // Promote to active epoch
                _activeEpoch = new ActiveEpoch
                {
                    Epoch = existingEpoch,
                    Vector = existingVector,
                    ReferenceCount = 1
                };
                
                // Add the first source (the one that created the epoch via fast path)
                var firstSource = _sources.Values.FirstOrDefault(s => s.CurrentVector.Equals(existingVector));
                if (firstSource != null)
                {
                    _activeEpoch.ParticipatingSourceIds.Add(firstSource.SourceId);
                }
                
                // Now subsume the new source
                var mergedVector = _activeEpoch.Vector.Merge(vector);
                _activeEpoch.Vector = mergedVector;
                ((Epoch)_activeEpoch.Epoch).UpdateVector(mergedVector);
                _activeEpoch.ParticipatingSourceIds.Add(sourceId);
                sourceState.CurrentVector = vector;
                
                return existingEpoch;
            }
            
            return CreateNewActiveEpoch(vector, sourceId);
        }

        // Case 2: This vector should subsume into active epoch
        // (new source joining, or same source before increment)
        if (ShouldSubsumeIntoActiveEpoch(sourceId, vector))
        {
            var mergedVector = _activeEpoch.Vector.Merge(vector);
            
            if (!mergedVector.Equals(_activeEpoch.Vector))
            {
                // Epoch grows to incorporate this source's vector
                _activeEpoch.Vector = mergedVector;
                // Also update the epoch object's vector
                ((Epoch)_activeEpoch.Epoch).UpdateVector(mergedVector);
            }
            
            _activeEpoch.ParticipatingSourceIds.Add(sourceId);
            sourceState.CurrentVector = vector;
            
            return _activeEpoch.Epoch;
        }

        // Case 3: Source trying to advance beyond active epoch
        // Check if all sources are ready for next epoch
        if (AllSourcesReadyForNext())
        {
            // Everyone ready - create new epoch
            CompleteActiveEpoch();
            return CreateNewActiveEpoch(vector, sourceId);
        }
        else
        {
            // This source must wait for others to signal readiness
            // In real implementation, this would be async wait
            // For prototype, we throw to indicate blocking needed
            throw new InvalidOperationException(
                $"Source {sourceId} attempting to advance to {vector} but other sources not ready. " +
                $"Current active epoch: {_activeEpoch.Vector}. " +
                $"Call SignalReadyForNext() and wait for coordination.");
        }
    }

    private bool ShouldSubsumeIntoActiveEpoch(string sourceId, EpochVector vector)
    {
        if (_activeEpoch == null)
            return false;

        var sourceState = _sources[sourceId];
        
        // New source (not in active epoch) - always subsume
        if (!_activeEpoch.ParticipatingSourceIds.Contains(sourceId))
            return true;

        // Same source, same vector - reuse active epoch
        if (sourceState.CurrentVector.Equals(vector))
            return true;

        // Same source trying to increment - don't subsume (need coordination)
        return false;
    }

    private bool AllSourcesReadyForNext()
    {
        // All sources must have signaled readiness for next epoch
        foreach (var source in _sources.Values)
        {
            if (!source.IsReadyForNext)
                return false;
        }
        return true;
    }

    private IEpoch CreateNewActiveEpoch(EpochVector vector, string sourceId)
    {
        // Create DI scope for this epoch
        var scope = _scopeFactory.CreateScope();
        var epoch = new Epoch(vector, scope);
        
        _activeEpoch = new ActiveEpoch
        {
            Epoch = epoch,
            Vector = vector,
            ReferenceCount = 1
        };
        _activeEpoch.ParticipatingSourceIds.Add(sourceId);
        
        _sources[sourceId].CurrentVector = vector;
        _sources[sourceId].NextVector = null; // Clear readiness flag
        
        _allEpochs[vector] = epoch;
        
        return epoch;
    }

    private void CompleteActiveEpoch()
    {
        if (_activeEpoch != null)
        {
            // Clear readiness flags for all sources
            foreach (var source in _sources.Values)
            {
                source.NextVector = null;
            }
            
            _activeEpoch = null;
        }
    }

    private IEpoch GetOrCreateEpochUnsafe(EpochVector vector, string sourceId)
    {
        // Single source - just create or reuse epoch
        if (_allEpochs.TryGetValue(vector, out var existing))
        {
            return existing;
        }

        var scope = _scopeFactory.CreateScope();
        var epoch = new Epoch(vector, scope);
        _allEpochs[vector] = epoch;
        
        _sources[sourceId].CurrentVector = vector;
        
        return epoch;
    }

    public void SignalReadyForNext(string sourceId, EpochVector currentVector, EpochVector nextVector)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(currentVector);
        ArgumentNullException.ThrowIfNull(nextVector);

        lock (_lock)
        {
            if (!_sources.TryGetValue(sourceId, out var sourceState))
            {
                throw new InvalidOperationException($"Unknown source: {sourceId}");
            }

            sourceState.NextVector = nextVector;

            // If all sources now ready, we could signal waiting sources
            // (In full implementation, would use TaskCompletionSource)
        }
    }

    public ValueTask NotifyEpochCompletedAsync(
        EpochVector vector,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        // Cleanup/disposal logic
        // In full implementation, track reference counts and dispose when done
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var epoch in _allEpochs.Values)
        {
            await epoch.DisposeAsync();
        }
        _allEpochs.Clear();
    }
}

/// <summary>
/// Source readiness state tracked by coordinator.
/// </summary>
internal class SourceReadiness
{
    public string SourceId { get; init; } = string.Empty;
    public EpochVector CurrentVector { get; set; } = EpochVector.None;
    public EpochVector? NextVector { get; set; }
    public bool IsReadyForNext => NextVector != null;
}

/// <summary>
/// Active epoch state managed by coordinator.
/// </summary>
internal class ActiveEpoch
{
    public IEpoch Epoch { get; init; } = null!;
    public EpochVector Vector { get; set; } = EpochVector.None;
    public HashSet<string> ParticipatingSourceIds { get; } = new();
    public int ReferenceCount { get; set; }
}
