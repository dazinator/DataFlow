namespace DataFlow.POC.Checkpointing;

using DataFlow.POC.Core;

/// <summary>
/// Coordinates checkpoint creation during epoch lifecycle.
/// Integrates with EpochHooks to create checkpoints after epoch commit.
/// </summary>
public sealed class CheckpointCoordinator
{
    private readonly ICheckpointStore _store;
    private readonly ICheckpointStrategy _strategy;
    private readonly Dictionary<string, ICheckpointAware> _checkpointAwareBlocks;
    
    public CheckpointCoordinator(
        ICheckpointStore store,
        ICheckpointStrategy strategy)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _checkpointAwareBlocks = new Dictionary<string, ICheckpointAware>();
    }
    
    /// <summary>
    /// Registers a checkpoint-aware block with a unique block ID.
    /// </summary>
    /// <param name="blockId">Unique identifier for the block.</param>
    /// <param name="block">The checkpoint-aware block instance.</param>
    public void RegisterBlock(string blockId, ICheckpointAware block)
    {
        ArgumentNullException.ThrowIfNull(blockId);
        ArgumentNullException.ThrowIfNull(block);
        
        _checkpointAwareBlocks[blockId] = block;
    }
    
    /// <summary>
    /// Called after epoch commit to potentially create a checkpoint.
    /// This should be invoked from the OnCommitEpoch hook.
    /// </summary>
    /// <param name="epoch">The epoch that just completed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task OnEpochCompletedAsync(IEpoch epoch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(epoch);
        
        // Check if we should create a checkpoint for this epoch
        if (!_strategy.ShouldCreateCheckpoint(epoch.Vector))
        {
            return; // Skip checkpoint for this epoch
        }
        
        try
        {
            var checkpointId = GenerateCheckpointId(epoch.Vector);
            var blockStates = new Dictionary<string, byte[]>();
            
            // Collect state from all checkpoint-aware blocks
            foreach (var (blockId, block) in _checkpointAwareBlocks)
            {
                try
                {
                    var state = await block.CreateCheckpointAsync(checkpointId, epoch.Vector, cancellationToken)
                        .ConfigureAwait(false);
                    
                    if (state != null && state.Length > 0)
                    {
                        blockStates[blockId] = state;
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue with other blocks
                    // Individual block checkpoint failures shouldn't fail entire checkpoint
                    Console.WriteLine($"Warning: Failed to create checkpoint for block '{blockId}': {ex.Message}");
                }
            }
            
            // Create checkpoint object
            var checkpoint = new Checkpoint
            {
                CheckpointId = checkpointId,
                EpochVector = epoch.Vector,
                Timestamp = DateTimeOffset.UtcNow,
                BlockStates = blockStates
            };
            
            // Persist checkpoint (best-effort, don't propagate failures)
            await _store.SaveCheckpointAsync(checkpoint, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log error but don't propagate - checkpoint creation is best-effort
            // The dataflow should continue executing normally even if checkpoint fails
            Console.WriteLine($"Warning: Failed to create checkpoint: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Restores all registered blocks from the latest checkpoint.
    /// This should be called BEFORE dataflow execution begins.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if checkpoint was restored, false if no checkpoint found.</returns>
    public async Task<bool> RestoreFromLatestCheckpointAsync(CancellationToken cancellationToken = default)
    {
        var checkpoint = await _store.GetLatestCheckpointAsync(cancellationToken).ConfigureAwait(false);
        if (checkpoint == null)
        {
            return false; // No checkpoint to restore from
        }
        
        return await RestoreFromCheckpointAsync(checkpoint, cancellationToken).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Restores all registered blocks from a specific checkpoint.
    /// This should be called BEFORE dataflow execution begins.
    /// </summary>
    /// <param name="checkpointId">The checkpoint ID to restore from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if checkpoint was restored, false if checkpoint not found.</returns>
    public async Task<bool> RestoreFromCheckpointAsync(string checkpointId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpointId);
        
        var checkpoint = await _store.GetCheckpointAsync(checkpointId, cancellationToken).ConfigureAwait(false);
        if (checkpoint == null)
        {
            return false; // Checkpoint not found
        }
        
        return await RestoreFromCheckpointAsync(checkpoint, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task<bool> RestoreFromCheckpointAsync(ICheckpoint checkpoint, CancellationToken cancellationToken)
    {
        // Restore each block that has saved state
        foreach (var (blockId, state) in checkpoint.BlockStates)
        {
            if (_checkpointAwareBlocks.TryGetValue(blockId, out var block))
            {
                try
                {
                    await block.RestoreFromCheckpointAsync(checkpoint.CheckpointId, state, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Log error but continue with other blocks
                    Console.WriteLine($"Warning: Failed to restore block '{blockId}' from checkpoint: {ex.Message}");
                }
            }
            else
            {
                // Block not registered - this could happen if dataflow configuration changed
                Console.WriteLine($"Warning: Checkpoint contains state for unknown block '{blockId}'");
            }
        }
        
        return true;
    }
    
    private static string GenerateCheckpointId(EpochVector vector)
    {
        // Create deterministic checkpoint ID from epoch vector
        // Format: checkpoint-{vector_string}-{timestamp_ticks}
        return $"checkpoint-{vector}-{DateTimeOffset.UtcNow.Ticks}";
    }
}
