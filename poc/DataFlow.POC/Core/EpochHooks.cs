namespace DataFlow.POC.Core;

using DataFlow.POC.Checkpointing;

/// <summary>
/// Defines lifecycle hooks that can be executed at different stages of epoch processing.
/// These hooks allow custom logic to be executed before processing epoch operations (OnBeginEpoch),
/// after all operations complete successfully (OnCommitEpoch), or when an error occurs (OnEpochError).
/// </summary>
public sealed class EpochHooks
{
    /// <summary>
    /// Hook called at dataflow startup to load a recovery checkpoint.
    /// Return the checkpoint to restore from, or null to start fresh.
    /// This checkpoint will be available in the execution context for all blocks.
    /// </summary>
    public Func<CancellationToken, Task<ICheckpoint?>>? OnLoadRecoveryCheckpoint { get; set; }

    /// <summary>
    /// Hook called before processing epoch operations.
    /// Typically used to begin transactions or prepare resources.
    /// </summary>
    public Func<IEpoch, CancellationToken, Task>? OnBeginEpoch { get; set; }

    /// <summary>
    /// Hook called after all epoch operations complete successfully.
    /// Typically used to commit transactions or finalize resources.
    /// </summary>
    public Func<IEpoch, CancellationToken, Task>? OnCommitEpoch { get; set; }

    /// <summary>
    /// Hook called when epoch processing fails.
    /// Typically used to rollback transactions or handle errors.
    /// </summary>
    public Func<IEpoch, Exception, CancellationToken, Task>? OnEpochError { get; set; }
}
