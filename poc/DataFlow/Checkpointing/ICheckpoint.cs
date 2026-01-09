namespace DataFlow.POC.Checkpointing;

using System.Text.Json;
using DataFlow.POC.Core;

/// <summary>
/// Represents a checkpoint containing block states at a specific epoch boundary.
/// Provides write-once semantics to prevent interference between blocks.
/// Block states are stored as JsonElement for human-readable JSON serialization.
/// </summary>
public interface ICheckpoint
{
    /// <summary>
    /// Unique identifier for this checkpoint.
    /// </summary>
    string CheckpointId { get; }

    /// <summary>
    /// The epoch vector identifying the position in the stream.
    /// </summary>
    EpochVector EpochVector { get; }

    /// <summary>
    /// Timestamp when the checkpoint was created.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Adds block state to the checkpoint.
    /// This method has write-once semantics - calling it twice with the same blockId throws an exception.
    /// </summary>
    /// <param name="blockId">Unique identifier for the block</param>
    /// <param name="state">Block state as JsonElement (use JsonSerializer.SerializeToElement to create)</param>
    /// <exception cref="InvalidOperationException">Thrown if blockId already exists in the checkpoint</exception>
    void AddBlockState(string blockId, JsonElement state);

    /// <summary>
    /// Tries to get block state from the checkpoint by block ID.
    /// </summary>
    /// <param name="blockId">Unique identifier for the block</param>
    /// <param name="state">The block state if found, otherwise default JsonElement</param>
    /// <returns>True if state was found, false otherwise</returns>
    bool TryGetBlockState(string blockId, out JsonElement state);

    /// <summary>
    /// Read-only view of all block states in the checkpoint.
    /// </summary>
    IReadOnlyDictionary<string, JsonElement> BlockStates { get; }
}
