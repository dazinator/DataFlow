namespace DataFlow.POC.Core;

/// <summary>
/// Provides context information about a block instance participating in epoch lifecycle.
/// This allows lifecycle events to identify which block is being notified.
/// </summary>
public interface IBlockContext
{
    /// <summary>
    /// The unique name/identifier of this block in the pipeline.
    /// </summary>
    string BlockName { get; }
    
    /// <summary>
    /// Optional metadata about the block (e.g., configuration, type information).
    /// </summary>
    IReadOnlyDictionary<string, object>? Metadata { get; }
}
