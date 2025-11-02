namespace EpochAnchoringDemo.Core;

/// <summary>
/// Represents a domain-specific anchor for resuming data processing.
/// Unlike EpochVector (which tracks framework-level alignment), an anchor
/// contains source-specific resume information (e.g., database offset, last processed ID).
/// </summary>
public sealed class SourceAnchor
{
    /// <summary>
    /// The last processed record ID. This is the domain-specific resume point.
    /// On restart, the source will resume from records with ID > LastProcessedId.
    /// </summary>
    public int LastProcessedId { get; init; }

    /// <summary>
    /// Optional: The epoch vector at the time this anchor was saved.
    /// This links the domain anchor to the framework's epoch tracking.
    /// In a full checkpoint, both the anchor and epoch vector would be persisted.
    /// </summary>
    public string? EpochVectorSnapshot { get; init; }

    /// <summary>
    /// Timestamp when this anchor was created.
    /// </summary>
    public DateTime Timestamp { get; init; }

    public SourceAnchor(int lastProcessedId, string? epochVectorSnapshot = null)
    {
        LastProcessedId = lastProcessedId;
        EpochVectorSnapshot = epochVectorSnapshot;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates an initial anchor (no records processed yet).
    /// </summary>
    public static SourceAnchor Initial => new(0);

    public override string ToString()
    {
        return $"SourceAnchor(LastProcessedId={LastProcessedId}, Epoch={EpochVectorSnapshot ?? "None"}, Timestamp={Timestamp:O})";
    }
}
