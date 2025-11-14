namespace DataFlow.POC.Core;

/// <summary>
/// Coordinates epoch creation across multiple sources to ensure:
/// 1. Sources get the same epoch object (single DI scope)
/// 2. Bounded epoch growth (one sequence per source per epoch)
/// 3. Readiness-based advancement (don't wait for completion)
/// 4. Single-source optimization (no coordination overhead)
/// </summary>
public interface IEpochCoordinator : IAsyncDisposable
{
    /// <summary>
    /// Request an epoch for the given vector. Coordinates with other sources
    /// to ensure bounded growth and same epoch object across sources.
    /// </summary>
    /// <param name="sourceId">Identifier for the source requesting the epoch</param>
    /// <param name="vector">The epoch vector for this source</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// The epoch object (with DI scope) that this source should use.
    /// Multiple sources may get the SAME epoch object if they're coordinating.
    /// </returns>
    /// <remarks>
    /// For single source: Returns immediately (fast path, no coordination).
    /// For multi-source: May block if waiting for other sources to signal readiness.
    /// </remarks>
    ValueTask<IEpoch> GetOrCreateEpochAsync(
        string sourceId,
        EpochVector vector,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signal that this source is ready to advance to the next epoch.
    /// Does NOT wait for current epoch to complete - signals intent to move forward.
    /// </summary>
    /// <param name="sourceId">Source signaling readiness</param>
    /// <param name="currentVector">Current vector this source is on</param>
    /// <param name="nextVector">Next vector this source wants to create</param>
    void SignalReadyForNext(string sourceId, EpochVector currentVector, EpochVector nextVector);

    /// <summary>
    /// Notify that an epoch has completed processing.
    /// This is for cleanup/disposal, not for coordination.
    /// </summary>
    ValueTask NotifyEpochCompletedAsync(
        EpochVector vector,
        CancellationToken cancellationToken = default);
}
