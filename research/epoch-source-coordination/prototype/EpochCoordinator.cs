namespace DataFlow.Research.EpochSourceCoordination;

using System.Collections.Concurrent;

/// <summary>
/// Implementation of source-level epoch coordination with:
/// - Bounded epoch growth (one sequence per source per epoch)
/// - Readiness-based advancement (don't wait for completion)
/// - Single-source fast path (no coordination overhead)
/// </summary>
public sealed class EpochCoordinator : IEpochCoordinator
{
    private readonly IServiceProvider _rootServiceProvider;
    private readonly object _lock = new();
    
    // Track sources and their readiness state
    private readonly Dictionary<string, SourceReadiness> _sources = new();
    
    // Current active epoch (if any)
    private ActiveEpoch? _activeEpoch;
    
    // All epochs (for disposal tracking)
    private readonly ConcurrentDictionary<EpochVector, IEpoch> _allEpochs = new();
    
    // Task completion sources for sources waiting for readiness
    private readonly Dictionary<string, TaskCompletionSource<IEpoch>> _waitingForReadiness = new();

    public EpochCoordinator(IServiceProvider rootServiceProvider)
    {
        _rootServiceProvider = rootServiceProvider ?? throw new ArgumentNullException(nameof(rootServiceProvider));
    }

    public async ValueTask<IEpoch> GetOrCreateEpochAsync(
        string sourceId,
        EpochVector vector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(vector);

        lock (_lock)
        {
            // Register source if first time seeing it
            if (!_sources.ContainsKey(sourceId))
            {
                _sources[sourceId] = new SourceReadiness
                {
                    SourceId = sourceId,
                    CurrentVector = EpochVector.None
                };
            }

            // FAST PATH: Single source optimization
            if (_sources.Count == 1)
            {
                return GetOrCreateEpochUnsafe(vector, sourceId);
            }

            // MULTI-SOURCE PATH: Check if we need coordination
            return GetOrCreateEpochWithCoordination(sourceId, vector, cancellationToken);
        }
    }

    private IEpoch GetOrCreateEpochWithCoordination(
        string sourceId,
        EpochVector vector,
        CancellationToken cancellationToken)
    {
        var sourceState = _sources[sourceId];
        
        // Case 1: No active epoch - create new one
        if (_activeEpoch == null)
        {
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
        var scope = _rootServiceProvider.CreateScope();
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

        var scope = _rootServiceProvider.CreateScope();
        var epoch = new Epoch(vector, scope);
        _allEpochs[vector] = epoch;
        
        _sources[sourceId].CurrentVector = vector;
        
        return epoch;
    }

    public void SignalReadyForNext(string sourceId, EpochVector currentVector, EpochVector nextVector)
    {
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
        // Cleanup/disposal logic
        // In full implementation, track reference counts and dispose when done
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var epoch in _allEpochs.Values)
        {
            await epoch.DisposeAsync();
        }
        _allEpochs.Clear();
    }
}

/// <summary>
/// Simple epoch implementation with DI scope.
/// </summary>
internal sealed class Epoch : IEpoch
{
    private readonly IServiceScope _scope;

    public EpochVector Vector { get; }
    public IServiceProvider ServiceProvider => _scope.ServiceProvider;

    public Epoch(EpochVector vector, IServiceScope scope)
    {
        Vector = vector ?? throw new ArgumentNullException(nameof(vector));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    public T GetService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
    }
}

/// <summary>
/// Epoch stream implementation that carries epoch object.
/// </summary>
public sealed class EpochStream<T> : IEpochStream<T>
{
    public EpochVector Vector => Epoch.Vector;
    public IAsyncEnumerable<T> Items { get; }
    public IEpoch Epoch { get; }

    public EpochStream(IEpoch epoch, IAsyncEnumerable<T> items)
    {
        Epoch = epoch ?? throw new ArgumentNullException(nameof(epoch));
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public ValueTask DisposeAsync()
    {
        // Don't dispose epoch here - coordinator manages epoch disposal
        return ValueTask.CompletedTask;
    }
}
