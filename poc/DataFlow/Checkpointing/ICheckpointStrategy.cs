namespace DataFlow.POC.Checkpointing;

using DataFlow.POC.Core;

/// <summary>
/// Strategy for determining which epochs should create checkpoints.
/// </summary>
public interface ICheckpointStrategy
{
    /// <summary>
    /// Determines whether a checkpoint should be created for the given epoch.
    /// </summary>
    /// <param name="epochVector">The epoch vector to evaluate</param>
    /// <returns>True if a checkpoint should be created; otherwise, false</returns>
    bool ShouldCreateCheckpoint(EpochVector epochVector);
}
