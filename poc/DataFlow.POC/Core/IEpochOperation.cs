namespace DataFlow.POC.Core;

using DataFlow.POC.Checkpointing;

/// <summary>
/// Represents an operation that can be queued for serialized execution within an epoch.
/// This interface enables type-erasure for channel storage while maintaining type-safe
/// queueing via the generic helper method.
/// </summary>
public interface IEpochOperation
{
    /// <summary>
    /// Executes the operation using services resolved from the provided service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve dependencies from.</param>
    /// <param name="context">The epoch operation context, providing access to checkpoint if applicable.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task ExecuteAsync(IServiceProvider serviceProvider, IEpochOperationContext context, CancellationToken cancellationToken);
}
