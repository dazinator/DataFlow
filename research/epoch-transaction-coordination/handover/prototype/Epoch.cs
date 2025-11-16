namespace DataFlow.POC.Core;

using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

/// <summary>
/// Implementation of an epoch with its own DI scope.
/// Manages the lifecycle of the DI scope and provides service resolution.
/// </summary>
internal sealed class Epoch : IEpoch
{
    private readonly IServiceScope _scope;
    private readonly ConcurrentDictionary<Type, SerializedServiceExecutor> _serializedExecutors;
    private bool _disposed;

    public EpochVector Vector { get; private set; }
    public IServiceProvider ServiceProvider => _scope.ServiceProvider;

    public Epoch(EpochVector vector, IServiceScope scope)
    {
        Vector = vector ?? throw new ArgumentNullException(nameof(vector));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _serializedExecutors = new ConcurrentDictionary<Type, SerializedServiceExecutor>();
    }

    /// <summary>
    /// Updates the epoch vector during subsumption when sources join.
    /// Internal to restrict mutation to coordinator implementation only.
    /// </summary>
    internal void UpdateVector(EpochVector newVector)
    {
        ArgumentNullException.ThrowIfNull(newVector);
        Vector = newVector;
    }

    public T GetService<T>() where T : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ServiceProvider.GetRequiredService<T>();
    }

    public Task QueueSerializedOperationAsync<TService>(
        Func<TService, Task> operation,
        CancellationToken cancellationToken = default)
        where TService : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(operation);

        var executor = GetOrCreateExecutor<TService>();
        return executor.QueueOperationAsync(operation, cancellationToken);
    }

    public async Task WhenAllOperationsCompletedAsync()
    {
        // Complete all executors (signal no more operations)
        foreach (var executor in _serializedExecutors.Values)
        {
            await executor.CompleteAndDrainAsync().ConfigureAwait(false);
        }
    }

    private SerializedServiceExecutor<TService> GetOrCreateExecutor<TService>()
        where TService : notnull
    {
        var serviceType = typeof(TService);
        
        var executor = _serializedExecutors.GetOrAdd(serviceType, _ =>
        {
            var service = GetService<TService>();
            return new SerializedServiceExecutor<TService>(service);
        });

        return (SerializedServiceExecutor<TService>)executor;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Ensure all operations complete before disposing
        await WhenAllOperationsCompletedAsync().ConfigureAwait(false);

        // Dispose all serialized executors
        foreach (var executor in _serializedExecutors.Values)
        {
            await executor.DisposeAsync().ConfigureAwait(false);
        }
        
        _serializedExecutors.Clear();
        
        // Dispose the scope
        _scope.Dispose();
    }

    // Base class for type-erased executor storage
    private abstract class SerializedServiceExecutor : IAsyncDisposable
    {
        public abstract Task CompleteAndDrainAsync();
        public abstract ValueTask DisposeAsync();
    }

    // Typed executor wrapper
    private sealed class SerializedServiceExecutor<TService> : SerializedServiceExecutor
        where TService : notnull
    {
        private readonly Core.SerializedServiceExecutor<TService> _inner;

        public SerializedServiceExecutor(TService service)
        {
            _inner = new Core.SerializedServiceExecutor<TService>(service);
        }

        public Task QueueOperationAsync(Func<TService, Task> operation, CancellationToken cancellationToken)
        {
            return _inner.QueueOperationAsync(operation, cancellationToken);
        }

        public override Task CompleteAndDrainAsync()
        {
            return _inner.CompleteAndDrainAsync();
        }

        public override ValueTask DisposeAsync()
        {
            return _inner.DisposeAsync();
        }
    }
}

