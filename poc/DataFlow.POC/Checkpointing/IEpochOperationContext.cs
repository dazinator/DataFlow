namespace DataFlow.POC.Checkpointing;

/// <summary>
/// Context provided to epoch operations, giving access to checkpoint if the epoch is being checkpointed.
/// This interface is passed to serialized operations, ensuring thread-safe checkpoint access.
/// </summary>
public interface IEpochOperationContext
{
    /// <summary>
    /// The checkpoint for this epoch, or null if this epoch is not being checkpointed.
    /// Only accessible within serialized epoch operations for thread safety.
    /// </summary>
    ICheckpoint? Checkpoint { get; }
}
