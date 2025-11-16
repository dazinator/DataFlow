namespace DataFlow.POC.Checkpointing;

/// <summary>
/// Represents a checkpoint containing state from multiple blocks.
/// A checkpoint is always associated with a specific epoch vector for alignment.
/// </summary>
public interface ICheckpoint
{
    /// <summary>
    /// Unique identifier for this checkpoint.
    /// </summary>
    string CheckpointId { get; }
    
    /// <summary>
    /// The epoch vector at which this checkpoint was taken.
    /// This identifies the exact point in the stream where checkpoint was created.
    /// </summary>
    Core.EpochVector EpochVector { get; }
    
    /// <summary>
    /// Timestamp when checkpoint was created.
    /// </summary>
    DateTimeOffset Timestamp { get; }
    
    /// <summary>
    /// Block-contributed state data.
    /// Keys are block IDs, values are serialized state.
    /// Blocks are responsible for serializing/deserializing their own state.
    /// </summary>
    IReadOnlyDictionary<string, byte[]> BlockStates { get; }
}

/// <summary>
/// Marker interface for blocks that can participate in checkpointing.
/// Blocks implement this interface to save and restore state during checkpoint operations.
/// </summary>
public interface ICheckpointAware
{
    /// <summary>
    /// Called when a checkpoint is being created.
    /// Block should return serialized state data or null if no state to save.
    /// This is called during epoch processing as part of serialized operations; the checkpoint itself is persisted after commit.
    /// </summary>
    /// <param name="checkpointId">Identifier for the checkpoint being created.</param>
    /// <param name="epochVector">The epoch vector at checkpoint time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Serialized state data or null if no state to checkpoint.</returns>
    Task<byte[]?> CreateCheckpointAsync(
        string checkpointId,
        Core.EpochVector epochVector,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Called during recovery to restore block state from checkpoint.
    /// This is called BEFORE dataflow execution begins.
    /// </summary>
    /// <param name="checkpointId">Identifier of checkpoint being restored.</param>
    /// <param name="state">Previously saved state data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RestoreFromCheckpointAsync(
        string checkpointId,
        byte[] state,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Strategy for determining when checkpoints should be created.
/// </summary>
public interface ICheckpointStrategy
{
    /// <summary>
    /// Determines if a checkpoint should be created for the given epoch.
    /// </summary>
    /// <param name="epochVector">The current epoch vector.</param>
    /// <returns>True if checkpoint should be created, false otherwise.</returns>
    bool ShouldCreateCheckpoint(Core.EpochVector epochVector);
}

/// <summary>
/// Abstraction for checkpoint persistence.
/// Implementations can store checkpoints in different backends (memory, file, database, blob storage).
/// </summary>
public interface ICheckpointStore
{
    /// <summary>
    /// Persists a checkpoint (best-effort).
    /// Failures should be logged but not propagate to caller.
    /// </summary>
    /// <param name="checkpoint">The checkpoint to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveCheckpointAsync(ICheckpoint checkpoint, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves the latest checkpoint, or null if none exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Latest checkpoint or null.</returns>
    Task<ICheckpoint?> GetLatestCheckpointAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves a specific checkpoint by ID.
    /// </summary>
    /// <param name="checkpointId">The checkpoint identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Checkpoint with given ID or null if not found.</returns>
    Task<ICheckpoint?> GetCheckpointAsync(string checkpointId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a checkpoint to free storage.
    /// </summary>
    /// <param name="checkpointId">The checkpoint identifier to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteCheckpointAsync(string checkpointId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Lists all available checkpoint IDs, ordered by creation time (newest first).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of checkpoint IDs.</returns>
    Task<IReadOnlyList<string>> ListCheckpointsAsync(CancellationToken cancellationToken = default);
}
