namespace DataFlow.POC.Checkpointing;

using System.Text.Json;
using DataFlow.POC.Core;

/// <summary>
/// Implementation of ICheckpoint with write-once semantics.
/// Access is serialized by design (only accessible within serialized epoch operations),
/// so no concurrent dictionary is needed.
/// Stores block states as JsonElement for human-readable JSON serialization.
/// </summary>
public sealed class Checkpoint : ICheckpoint
{
    private readonly Dictionary<string, JsonElement> _blockStates = new();

    public Checkpoint(string checkpointId, EpochVector epochVector, DateTimeOffset timestamp)
    {
        CheckpointId = checkpointId ?? throw new ArgumentNullException(nameof(checkpointId));
        EpochVector = epochVector ?? throw new ArgumentNullException(nameof(epochVector));
        Timestamp = timestamp;
    }

    public string CheckpointId { get; }

    public EpochVector EpochVector { get; }

    public DateTimeOffset Timestamp { get; }

    public IReadOnlyDictionary<string, JsonElement> BlockStates => _blockStates;

    public void AddBlockState(string blockId, JsonElement state)
    {
        ArgumentNullException.ThrowIfNull(blockId);

        if (_blockStates.ContainsKey(blockId))
        {
            throw new InvalidOperationException(
                $"Block state for '{blockId}' has already been added to checkpoint '{CheckpointId}'. " +
                "Each block can only contribute state once per checkpoint.");
        }

        _blockStates[blockId] = state;
    }

    public bool TryGetBlockState(string blockId, out JsonElement state)
    {
        ArgumentNullException.ThrowIfNull(blockId);
        
        return _blockStates.TryGetValue(blockId, out state);
    }
}

