namespace DataFlow.POC.Checkpointing;

using System.Text.Json;

/// <summary>
/// Extension methods for ICheckpoint to provide ergonomic checkpoint state management.
/// </summary>
public static class CheckpointExtensions
{
    /// <summary>
    /// Sets block state in the checkpoint with automatic JSON serialization.
    /// This is a convenience wrapper around AddBlockState that handles serialization.
    /// </summary>
    /// <typeparam name="T">Type of the state object to serialize</typeparam>
    /// <param name="checkpoint">The checkpoint to add state to</param>
    /// <param name="blockId">Unique identifier for the block</param>
    /// <param name="state">State object to serialize and store</param>
    /// <exception cref="InvalidOperationException">Thrown if blockId already exists in the checkpoint</exception>
    /// <example>
    /// <code>
    /// // Clean, canonical usage
    /// ctx.Checkpoint?.SetState(blockId, new { offset = 123, lastId = "abc" });
    /// </code>
    /// </example>
    public static void SetState<T>(this ICheckpoint checkpoint, string blockId, T state)
    {
        var element = JsonSerializer.SerializeToElement(state);
        checkpoint.AddBlockState(blockId, element);
    }
}
