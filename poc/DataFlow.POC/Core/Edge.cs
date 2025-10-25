namespace DataFlow.POC.Core;

/// <summary>
/// Defines the buffering behavior of an edge.
/// Note: Only Bounded mode is supported in this design to ensure developers
/// explicitly consider memory constraints.
/// </summary>
public enum BufferMode
{
    /// <summary>
    /// No buffering - data flows inline during enumeration.
    /// Limitations in the current POC implementation:
    /// - Uses small buffer internally for coordination between blocks
    /// - May not provide true zero-buffer streaming in all scenarios
    /// - Bounded mode is recommended and better supported
    /// </summary>
    None,

    /// <summary>
    /// Buffered with a bounded channel for backpressure.
    /// This is the recommended and default mode.
    /// </summary>
    Bounded
}

/// <summary>
/// Represents a connection (edge) between a source block and one or more target blocks in the dataflow graph.
/// Edges own the buffering policy, channel management, and delivery semantics via EdgeStrategy.
/// </summary>
public class Edge
{
    public Edge(
        IBlock sourceBlock,
        IBlock targetBlock,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
        : this(sourceBlock, new[] { targetBlock }, new BroadcastEdgeStrategy(bufferMode, bufferCapacity))
    {
    }

    public Edge(
        IBlock sourceBlock,
        IBlock targetBlock,
        EdgeStrategy strategy)
        : this(sourceBlock, new[] { targetBlock }, strategy)
    {
    }

    public Edge(
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks,
        EdgeStrategy strategy)
    {
        SourceBlock = sourceBlock ?? throw new ArgumentNullException(nameof(sourceBlock));
        TargetBlocks = targetBlocks ?? throw new ArgumentNullException(nameof(targetBlocks));
        Strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));

        if (targetBlocks.Count == 0)
        {
            throw new ArgumentException("At least one target block is required", nameof(targetBlocks));
        }

        // Validate type compatibility for all targets
        foreach (var targetBlock in targetBlocks)
        {
            if (sourceBlock.OutputType != targetBlock.InputType)
            {
                throw new ArgumentException(
                    $"Type mismatch: Source block '{sourceBlock.Name}' output type {sourceBlock.OutputType.Name} " +
                    $"does not match target block '{targetBlock.Name}' input type {targetBlock.InputType.Name}");
            }
        }

        DataType = sourceBlock.OutputType;
    }

    /// <summary>
    /// The source block that produces data.
    /// </summary>
    public IBlock SourceBlock { get; }

    /// <summary>
    /// The target blocks that consume data.
    /// For single-target edges, this contains one block.
    /// For multi-target edges (broadcast, competing), this contains multiple blocks.
    /// </summary>
    public IReadOnlyList<IBlock> TargetBlocks { get; }

    /// <summary>
    /// The target block that consumes data (for single-target edges).
    /// For backward compatibility.
    /// </summary>
    public IBlock TargetBlock => TargetBlocks[0];

    /// <summary>
    /// The edge strategy that defines delivery semantics.
    /// </summary>
    public EdgeStrategy Strategy { get; }

    /// <summary>
    /// The buffering mode for this edge.
    /// </summary>
    public BufferMode BufferMode => Strategy.BufferMode;

    /// <summary>
    /// The capacity of the buffer (if BufferMode is Bounded).
    /// </summary>
    public int BufferCapacity => Strategy.BufferCapacity;

    /// <summary>
    /// The type of data flowing through this edge.
    /// </summary>
    public Type DataType { get; }

    /// <summary>
    /// Optional metadata for this edge (for extensibility).
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = new();

    public override string ToString()
    {
        var targetNames = string.Join(", ", TargetBlocks.Select(t => t.Name));
        return $"{SourceBlock.Name} -> [{targetNames}] ({Strategy.EdgeType}, {DataType.Name})";
    }
}
