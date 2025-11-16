namespace DataFlow.POC.Core;

using System.Threading.Channels;

/// <summary>
/// Represents an epoch instance with its own DI scope and serialized operations queue.
/// Multiple sources/blocks accessing the same epoch will share the same DI scope
/// and can queue operations that will be executed serially.
/// </summary>
public interface IEpoch : IAsyncDisposable
{
    /// <summary>
    /// The epoch vector identifying this epoch.
    /// </summary>
    EpochVector Vector { get; }

    /// <summary>
    /// Resolves a service from this epoch's DI scope.
    /// Multiple sources/blocks accessing the same epoch will get the same instance.
    /// </summary>
    T GetService<T>() where T : notnull;

    /// <summary>
    /// Gets the service provider for this epoch's scope.
    /// </summary>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Queues an operation for serialized execution with an epoch-scoped service.
    /// 
    /// All operations are queued to a single channel and executed FULLY SERIALLY
    /// (not per service type). This ensures:
    /// - No MSDTC escalation (only one connection active at a time)
    /// - No concurrency bugs (thread-safe by design)
    /// - Deterministic execution order (FIFO)
    /// 
    /// The returned task completes when the operation is successfully queued,
    /// NOT when the operation has been executed. This encourages blocks to submit
    /// their work and continue without blocking on execution completion.
    /// 
    /// All queued operations will be executed before the epoch completes.
    /// </summary>
    /// <typeparam name="TService">The type of service to access.</typeparam>
    /// <param name="operation">The operation to execute with the service.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that completes when the operation is queued (not executed).</returns>
    Task QueueSerializedOperationAsync<TService>(
        Func<TService, Task> operation,
        CancellationToken cancellationToken = default)
        where TService : notnull;

    /// <summary>
    /// Gets the operations channel for this epoch. Used by EpochProcessorNode to drain operations.
    /// </summary>
    ChannelReader<IEpochOperation> OperationsReader { get; }

    /// <summary>
    /// Signals that no more operations will be queued for this epoch.
    /// Called by the epoch coordinator when all blocks have finished processing the epoch.
    /// </summary>
    void CompleteOperations();

    /// <summary>
    /// Gets a task that completes when all queued operations have been executed.
    /// This is used internally during epoch completion to ensure all operations are drained
    /// before the epoch is disposed.
    /// </summary>
    Task WhenAllOperationsCompletedAsync();

    /// <summary>
    /// Gets the number of operations queued for execution.
    /// </summary>
    int QueuedCount { get; }

    /// <summary>
    /// Gets the number of operations that have been executed.
    /// </summary>
    int ExecutedCount { get; }
}
