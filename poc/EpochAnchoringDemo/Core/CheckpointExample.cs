namespace EpochAnchoringDemo.Core;

/// <summary>
/// Example checkpoint structure showing how anchors and epoch vectors
/// would be aggregated together at global epoch alignment.
/// 
/// This is NOT implemented in this demo but shows the conceptual model
/// described in the feedback.
/// </summary>
/// <example>
/// <code>
/// {
///   "epochVector": {"orders-source": 120},
///   "sources": {
///     "orders-source": { "lastOrderId": 84210 }
///   },
///   "blocks": {
///     "ef-writer": { "lastCommittedEpoch": 120 }
///   },
///   "timestamp": "2025-11-03T10:45:00Z"
/// }
/// </code>
/// </example>
public sealed class CheckpointExample
{
    /// <summary>
    /// Framework-level epoch vectors for alignment tracking.
    /// Each source/block has its own epoch sequence.
    /// </summary>
    public Dictionary<string, long> EpochVector { get; init; } = new();

    /// <summary>
    /// Domain-specific anchors from each source.
    /// These are resume points in the source system (e.g., database IDs, timestamps).
    /// </summary>
    public Dictionary<string, SourceAnchor> SourceAnchors { get; init; } = new();

    /// <summary>
    /// Block-specific state (e.g., transient data, metrics).
    /// </summary>
    public Dictionary<string, object> BlockMetadata { get; init; } = new();

    /// <summary>
    /// When this checkpoint was created.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Example showing two layers of persistence:
    /// 1. Epoch lifecycle (every epoch): SaveChanges() + context reset
    /// 2. Checkpoint policy (every N epochs): Aggregate and persist this checkpoint object
    /// </summary>
    public static string ExampleUsage => @"
// Layer 1: Every epoch commits transactionally
await dbContext.SaveChangesAsync();  // Local consistency
await dbContext.DisposeAsync();      // Reset change tracker

// Layer 2: Every N epochs, create checkpoint
if (epochCount % checkpointFrequency == 0)
{
    var checkpoint = new Checkpoint
    {
        EpochVector = GetCurrentEpochVectors(),
        SourceAnchors = CollectSourceAnchors(),
        BlockMetadata = CollectBlockState(),
        Timestamp = DateTime.UtcNow
    };
    
    await checkpointStore.SaveAsync(checkpoint);  // Global recovery point
}
";
}
