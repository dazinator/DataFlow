namespace DataFlow.POC.Core;

/// <summary>
/// Interface for components that participate in epoch lifecycle events.
/// This extends the observer pattern from Phase 5 by adding block context information,
/// enabling fine-grained reporting and per-block lifecycle management.
/// </summary>
public interface IEpochLifecycleParticipant
{
    /// <summary>
    /// Called when a new epoch is created/started.
    /// </summary>
    /// <param name="epoch">The epoch that was created</param>
    /// <param name="block">The block context for the participant</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask OnEpochCreatedAsync(EpochVector epoch, IBlockContext block, CancellationToken cancellationToken);

    /// <summary>
    /// Called when an epoch completes (all data processed locally by this block).
    /// This is the natural boundary for committing per-epoch resources (transactions, buffers, etc.).
    /// </summary>
    /// <param name="epoch">The epoch that completed</param>
    /// <param name="block">The block context for the participant</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask OnEpochCompletedAsync(EpochVector epoch, IBlockContext block, CancellationToken cancellationToken);

    /// <summary>
    /// Called when all blocks reach a shared completion watermark (global alignment).
    /// This represents a consistent graph-level checkpoint boundary where all blocks have
    /// completed processing through a specific epoch. Components can contribute state
    /// (anchors, metadata) for checkpoint persistence at this point.
    /// 
    /// Note: This event is global (not per-block), so block context is not provided.
    /// </summary>
    /// <param name="watermark">The global completion watermark</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for epoch lifecycle participants with optional overrides.
/// Provides default no-op implementations for convenience.
/// </summary>
public abstract class EpochLifecycleParticipantBase : IEpochLifecycleParticipant
{
    /// <inheritdoc />
    public virtual ValueTask OnEpochCreatedAsync(EpochVector epoch, IBlockContext block, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
    
    /// <inheritdoc />
    public virtual ValueTask OnEpochCompletedAsync(EpochVector epoch, IBlockContext block, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
    
    /// <inheritdoc />
    public virtual ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}
