namespace DataFlow.POC.Core;

using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Implementation of <see cref="IEpoch"/> with fully serial operations queue.
/// All operations, regardless of service type, are executed serially through a single channel.
/// </summary>
internal sealed class Epoch : IEpoch
{
    private readonly IServiceScope _scope;
    private readonly Channel<IEpochOperation> _operationsChannel;
    private readonly TaskCompletionSource _completionTcs;
    private int _queuedCount;
    private int _executedCount;
    private bool _disposed;

    public EpochVector Vector { get; }
    public IServiceProvider ServiceProvider => _scope.ServiceProvider;
    public ChannelReader<IEpochOperation> OperationsReader => _operationsChannel.Reader;
    public int QueuedCount => Volatile.Read(ref _queuedCount);
    public int ExecutedCount => Volatile.Read(ref _executedCount);

    public Epoch(EpochVector vector, IServiceScope scope)
    {
        Vector = vector ?? throw new ArgumentNullException(nameof(vector));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));

        // Single unbounded channel for ALL operations (fully serial)
        _operationsChannel = Channel.CreateUnbounded<IEpochOperation>(new UnboundedChannelOptions
        {
            SingleReader = true,  // EpochProcessorNode is the single reader
            SingleWriter = false, // Multiple blocks can queue operations
            AllowSynchronousContinuations = false
        });

        _completionTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public T GetService<T>() where T : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _scope.ServiceProvider.GetRequiredService<T>();
    }

    public Task QueueSerializedOperationAsync<TService>(
        Func<TService, Task> operation,
        CancellationToken cancellationToken = default)
        where TService : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(operation);

        // Create operation wrapper that encapsulates type resolution
        var epochOperation = new EpochOperation<TService>(operation, cancellationToken);

        // Queue to the single operations channel (fully serial)
        if (!_operationsChannel.Writer.TryWrite(epochOperation))
        {
            throw new InvalidOperationException("Failed to queue operation - channel may be closed");
        }

        // Increment queued counter
        Interlocked.Increment(ref _queuedCount);

        // Return immediately (fire-and-forget)
        return Task.CompletedTask;
    }

    public void CompleteOperations()
    {
        // Signal that no more operations will be queued
        // EpochProcessorNode will finish draining the channel
        _operationsChannel.Writer.TryComplete();
    }

    public Task WhenAllOperationsCompletedAsync()
    {
        return _completionTcs.Task;
    }

    /// <summary>
    /// Called by EpochProcessorNode after each operation is executed.
    /// This allows the epoch to track progress.
    /// </summary>
    internal void NotifyOperationExecuted()
    {
        var executed = Interlocked.Increment(ref _executedCount);
        var queued = Volatile.Read(ref _queuedCount);

        // If all operations have been executed and channel is complete, signal completion
        if (executed == queued && _operationsChannel.Reader.Completion.IsCompleted)
        {
            _completionTcs.TrySetResult();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Ensure operations channel is completed
        _operationsChannel.Writer.TryComplete();

        // Wait for all operations to complete
        try
        {
            await WhenAllOperationsCompletedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error waiting for epoch operations to complete: {ex}");
        }

        // Dispose the DI scope
        _scope.Dispose();
    }
}
