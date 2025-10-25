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
/// Represents a connection (edge) between two blocks in the dataflow graph.
/// Edges own the buffering policy and channel management.
/// </summary>
public class Edge
{
    public Edge(
        IBlock sourceBlock,
        IBlock targetBlock,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
    {
        SourceBlock = sourceBlock ?? throw new ArgumentNullException(nameof(sourceBlock));
        TargetBlock = targetBlock ?? throw new ArgumentNullException(nameof(targetBlock));
        BufferMode = bufferMode;
        BufferCapacity = bufferCapacity;

        // Validate type compatibility
        if (sourceBlock.OutputType != targetBlock.InputType)
        {
            throw new ArgumentException(
                $"Type mismatch: Source block '{sourceBlock.Name}' output type {sourceBlock.OutputType.Name} " +
                $"does not match target block '{targetBlock.Name}' input type {targetBlock.InputType.Name}");
        }

        DataType = sourceBlock.OutputType;
    }

    /// <summary>
    /// The source block that produces data.
    /// </summary>
    public IBlock SourceBlock { get; }

    /// <summary>
    /// The target block that consumes data.
    /// </summary>
    public IBlock TargetBlock { get; }

    /// <summary>
    /// The buffering mode for this edge.
    /// </summary>
    public BufferMode BufferMode { get; }

    /// <summary>
    /// The capacity of the buffer (if BufferMode is Bounded).
    /// </summary>
    public int BufferCapacity { get; }

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
        return $"{SourceBlock.Name} -> {TargetBlock.Name} ({BufferMode}, {DataType.Name})";
    }
}
