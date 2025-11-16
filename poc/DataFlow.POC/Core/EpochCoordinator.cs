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
    private readonly int _operationsQueueCapacity;
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

    public EpochCoordinator(IServiceScopeFactory scopeFactory, int operationsQueueCapacity = 100)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        
        if (operationsQueueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(operationsQueueCapacity), 
                "Operations queue capacity must be greater than 0");
        }
        
        _operationsQueueCapacity = operationsQueueCapacity;
    }

    public async ValueTask<IEpoch> GetOrCreateEpochAsync(
        string sourceId,
        EpochVector vector,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(vector);

        TaskCompletionSource<IEpoch>? tcsToAwait = null;

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
                return GetOrCreateEpochUnsafe(vector, sourceId);
            }

            // MULTI-SOURCE PATH: Check if we need coordination
            var result = GetOrCreateEpochWithCoordination(sourceId, vector, out tcsToAwait);
            if (result != null)
            {
                return result;
            }
        }

        // Need to wait outside the lock for other sources to be ready
        if (tcsToAwait != null)
        {
            using var registration = cancellationToken.Register(() =>
            {
                lock (_lock)
                {
                    if (_waitingForReadiness.TryGetValue(sourceId, out var tcs))
                    {
                        _waitingForReadiness.Remove(sourceId);
                        tcs.TrySetCanceled(cancellationToken);
                    }
                }
            });

            return await tcsToAwait.Task.ConfigureAwait(false);
        }

        // This should be unreachable if GetOrCreateEpochWithCoordination is working correctly
        throw new InvalidOperationException(
            $"Unexpected state in GetOrCreateEpochAsync: sourceId={sourceId}, vector={vector}, " +
            $"result was null but tcsToAwait was also null. This indicates a logic error in GetOrCreateEpochWithCoordination.");
    }

    private IEpoch? GetOrCreateEpochWithCoordination(
        string sourceId,
        EpochVector vector,
        out TaskCompletionSource<IEpoch>? tcsToAwait)
    {
        tcsToAwait = null;
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
            // Create a TaskCompletionSource for this source
            var tcs = new TaskCompletionSource<IEpoch>(TaskCreationOptions.RunContinuationsAsynchronously);
            _waitingForReadiness[sourceId] = tcs;
            
            // Store the vector this source wants to advance to (but don't mark as "ready")
            sourceState.WaitingForVector = vector;
            
            tcsToAwait = tcs;
            return null; // Indicates we need to await
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
        var epoch = new Epoch(vector, scope, _operationsQueueCapacity);
        
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
                source.WaitingForVector = null;
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
        var epoch = new Epoch(vector, scope, _operationsQueueCapacity);
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

            // Mark this source as ready for the next epoch
            sourceState.NextVector = nextVector;

            // If all sources now ready, signal any waiting sources
            if (AllSourcesReadyForNext())
            {
                // Create merged vector from all sources' next vectors (or waiting vectors)
                EpochVector? mergedVector = null;
                foreach (var source in _sources.Values)
                {
                    // Use NextVector if set (explicitly signaled), otherwise use WaitingForVector
                    // Note: This fallback ensures that sources which are still waiting (i.e., have not signaled readiness)
                    // still contribute their desired vector to the merge via WaitingForVector.
                    var vectorToMerge = source.NextVector ?? source.WaitingForVector;
                    if (vectorToMerge != null)
                    {
                        mergedVector = mergedVector == null 
                            ? vectorToMerge 
                            : mergedVector.Merge(vectorToMerge);
                    }
                }

                // This should never be null if AllSourcesReadyForNext() returned true, as at least one source
                // must have NextVector set. If this occurs, it indicates a logic error.
                if (mergedVector == null)
                {
                    throw new InvalidOperationException(
                        "All sources ready but no vectors found. This indicates a logic error in AllSourcesReadyForNext.");
                }

                // Complete the active epoch (this clears NextVector fields)
                CompleteActiveEpoch();
                
                // Create ONE new epoch for all sources
                IEpoch? newEpoch = null;
                
                // Get all waiting sources
                var waitingSources = _waitingForReadiness.ToList();
                _waitingForReadiness.Clear();
                
                // Process each waiting source
                foreach (var (waitingSourceId, tcs) in waitingSources)
                {
                    if (_sources.TryGetValue(waitingSourceId, out var waitingSource))
                    {
                        try
                        {
                            if (newEpoch == null)
                            {
                                // First waiting source creates the epoch
                                newEpoch = CreateNewActiveEpoch(mergedVector, waitingSourceId);
                            }
                            else
                            {
                                // Subsequent sources join the same epoch
                                _activeEpoch!.ParticipatingSourceIds.Add(waitingSourceId);
                                waitingSource.CurrentVector = mergedVector;
                            }
                            // Clear the waiting vector
                            waitingSource.WaitingForVector = null;
                            tcs.TrySetResult(newEpoch);
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(ex);
                        }
                    }
                    else
                    {
                        tcs.TrySetException(new InvalidOperationException(
                            $"Waiting source {waitingSourceId} not found"));
                    }
                }
            }
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

        // Cancel any pending waiters
        lock (_lock)
        {
            foreach (var tcs in _waitingForReadiness.Values)
            {
                tcs.TrySetException(new ObjectDisposedException(nameof(EpochCoordinator)));
            }
            _waitingForReadiness.Clear();
        }

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
    public EpochVector? NextVector { get; set; } // Explicitly signaled via SignalReadyForNext
    public EpochVector? WaitingForVector { get; set; } // Set when source is waiting for this vector
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
