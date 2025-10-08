namespace Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Represents a connection between two blocks in the dataflow graph.
/// This is an edge in the DAG that describes how data flows from source to target.
/// </summary>
public class BlockConnection
{
    public BlockConnection(string sourceBlockName, string targetBlockName)
    {
        SourceBlockName = sourceBlockName;
        TargetBlockName = targetBlockName;
    }

    /// <summary>
    /// The name of the source block (data producer).
    /// </summary>
    public string SourceBlockName { get; }

    /// <summary>
    /// The name of the target block (data consumer).
    /// </summary>
    public string TargetBlockName { get; }

    /// <summary>
    /// The type of data flowing through this connection.
    /// </summary>
    public Type? DataType { get; init; }

    /// <summary>
    /// Metadata about this connection that can be used by interceptors.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}
