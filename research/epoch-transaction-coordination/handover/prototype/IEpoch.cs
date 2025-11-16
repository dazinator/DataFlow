namespace DataFlow.POC.Core;

/// <summary>
/// Represents an epoch instance with its own DI scope.
/// Multiple sources/blocks accessing the same epoch will share the same DI scope
/// and get the same scoped service instances.
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
    /// Multiple concurrent callers will have their operations queued via a channel
    /// and executed sequentially by a single reader task.
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
    /// Gets a task that completes when all queued operations for the epoch have been executed.
    /// This is used internally during epoch completion to ensure all operations are drained
    /// before the epoch is disposed.
    /// </summary>
    Task WhenAllOperationsCompletedAsync();
}
