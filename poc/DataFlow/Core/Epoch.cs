namespace DataFlow.POC.Core;

using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Checkpointing;

/// <summary>
/// Implementation of <see cref="IEpoch"/> with fully serial operations queue.
/// All operations, regardless of service type, are executed serially through a single channel.
/// Also implements <see cref="IEpochOperationContext"/> to avoid allocating context objects.
/// </summary>
internal sealed class Epoch : IEpoch, IEpochOperationContext
{
    private readonly IServiceScope _scope;
    private readonly Channel<IEpochOperation> _operationsChannel;
    private readonly ICheckpoint? _checkpoint;
    private bool _disposed;

    public EpochVector Vector { get; private set; }
    public IServiceProvider ServiceProvider => _scope.ServiceProvider;
    public ChannelReader<IEpochOperation> OperationsReader => _operationsChannel.Reader;
    public bool IsCheckpointing => _checkpoint != null;
    
    // IEpochOperationContext implementation - Epoch provides its own context
    ICheckpoint? IEpochOperationContext.Checkpoint => _checkpoint;

    public Epoch(EpochVector vector, IServiceScope scope, int operationsQueueCapacity = 100, ICheckpoint? checkpoint = null)
    {
        Vector = vector ?? throw new ArgumentNullException(nameof(vector));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _checkpoint = checkpoint;

        if (operationsQueueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(operationsQueueCapacity), 
                "Operations queue capacity must be greater than 0");
        }

        // Bounded channel for ALL operations (fully serial)
        // Use WaitToWrite mode to block when full
        _operationsChannel = Channel.CreateBounded<IEpochOperation>(new BoundedChannelOptions(operationsQueueCapacity)
        {
            SingleReader = true,  // EpochProcessorNode is the single reader
            SingleWriter = false, // Multiple blocks can queue operations
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait // Block when full
        });
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

    public async Task QueueSerializedOperationAsync<TService>(
        Func<TService, Task> operation,
        CancellationToken cancellationToken = default)
        where TService : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(operation);

        // Wrap the operation to capture and ignore the context parameter
        // This allows EpochOperation to have a single, consistent signature
        Func<TService, IEpochOperationContext, Task> wrappedOperation = async (service, context) =>
        {
            await operation(service).ConfigureAwait(false);
        };

        // Create operation wrapper that encapsulates type resolution
        var epochOperation = new EpochOperation<TService>(wrappedOperation, cancellationToken);

        // Queue to the single operations channel (bounded with Wait mode)
        // This will block if the channel is full
        await _operationsChannel.Writer.WriteAsync(epochOperation, cancellationToken).ConfigureAwait(false);
    }

    public async Task QueueSerializedOperationAsync<TService>(
        Func<TService, IEpochOperationContext, Task> operation,
        CancellationToken cancellationToken = default)
        where TService : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(operation);

        // Create operation wrapper that encapsulates type resolution and context
        var epochOperation = new EpochOperation<TService>(operation, cancellationToken);

        // Queue to the single operations channel (bounded with Wait mode)
        // This will block if the channel is full
        await _operationsChannel.Writer.WriteAsync(epochOperation, cancellationToken).ConfigureAwait(false);
    }

    public void CompleteOperations()
    {
        // Signal that no more operations will be queued
        // Called externally when graph block alignment signals all blocks are done
        _operationsChannel.Writer.TryComplete();
    }

    public Task WhenAllOperationsCompletedAsync()
    {
        // Return the channel's completion task
        // This completes when the channel is closed AND all operations have been read
        return _operationsChannel.Reader.Completion;
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

        // Wait for all operations to complete (with timeout to prevent hanging)
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var completionTask = WhenAllOperationsCompletedAsync();
            var timeoutTask = Task.Delay(Timeout.Infinite, cts.Token);
            
            var completedTask = await Task.WhenAny(completionTask, timeoutTask).ConfigureAwait(false);
            if (completedTask == completionTask)
            {
                await completionTask.ConfigureAwait(false);
            }
        }
        catch
        {
            // Swallow exception: timeout or error during disposal is a documented safeguard and not critical
        }

        // Dispose the DI scope
        _scope.Dispose();
    }
}
