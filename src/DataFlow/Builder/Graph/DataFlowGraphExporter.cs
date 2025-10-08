namespace Uniun.DataFlow.Builder.Graph;

using System.Text;

/// <summary>
/// Utilities for visualizing and exporting dataflow graphs.
/// </summary>
public static class DataFlowGraphExporter
{
    /// <summary>
    /// Generates a Mermaid diagram representation of the dataflow graph.
    /// This can be used in documentation, rendered on GitHub, or used with Mermaid tools.
    /// </summary>
    /// <param name="graph">The dataflow graph to export</param>
    /// <param name="direction">The direction of the flowchart (LR, RL, TB, BT)</param>
    /// <returns>Mermaid diagram as a string</returns>
    public static string ToMermaidDiagram(this DataFlowGraph graph, string direction = "LR")
    {
        var sb = new StringBuilder();
        sb.AppendLine($"flowchart {direction}");
        sb.AppendLine($"    %% DataFlow: {graph.Name}");
        sb.AppendLine();

        // Add nodes with types
        foreach (var block in graph.BlockDefinitions.Values)
        {
            var shape = GetBlockShape(block);
            var label = GetBlockLabel(block);
            sb.AppendLine($"    {SanitizeId(block.Name)}{shape.Open}\"{label}\"{shape.Close}");
        }

        sb.AppendLine();

        // Add edges
        foreach (var connection in graph.Connections)
        {
            var dataTypeLabel = connection.DataType?.Name ?? "data";
            sb.AppendLine($"    {SanitizeId(connection.SourceBlockName)} -->|{dataTypeLabel}| {SanitizeId(connection.TargetBlockName)}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a simple text representation of the graph structure.
    /// </summary>
    public static string ToTextDiagram(this DataFlowGraph graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"DataFlow: {graph.Name}");
        sb.AppendLine($"Blocks: {graph.BlockDefinitions.Count}");
        sb.AppendLine($"Connections: {graph.Connections.Count}");
        sb.AppendLine();

        sb.AppendLine("Block Definitions:");
        foreach (var block in graph.BlockDefinitions.Values)
        {
            sb.AppendLine($"  - {block.Name}");
            sb.AppendLine($"      Type: {block.BlockType.Name}");
            if (block.InputType != null)
                sb.AppendLine($"      Input: {block.InputType.Name}");
            if (block.OutputType != null)
                sb.AppendLine($"      Output: {block.OutputType.Name}");
        }

        sb.AppendLine();
        sb.AppendLine("Connections:");
        foreach (var connection in graph.Connections)
        {
            var dataType = connection.DataType?.Name ?? "?";
            sb.AppendLine($"  {connection.SourceBlockName} --({dataType})--> {connection.TargetBlockName}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets a summary of the graph structure.
    /// </summary>
    public static DataFlowGraphSummary GetSummary(this DataFlowGraph graph)
    {
        return new DataFlowGraphSummary
        {
            Name = graph.Name,
            BlockCount = graph.BlockDefinitions.Count,
            ConnectionCount = graph.Connections.Count,
            SourceBlockCount = graph.GetSourceBlocks().Count(),
            TargetBlockCount = graph.GetTargetBlocks().Count(),
            BlockTypes = graph.BlockDefinitions.Values
                .GroupBy(b => b.BlockType)
                .ToDictionary(g => g.Key.Name, g => g.Count())
        };
    }

    private static (string Open, string Close) GetBlockShape(BlockDefinition block)
    {
        // Source blocks (no input)
        if (block.InputType == null && block.OutputType != null)
            return ("([", "])");  // Stadium shape for sources

        // Target blocks (no output)
        if (block.InputType != null && block.OutputType == null)
            return ("[", "]");    // Rectangle for targets

        // Propagator blocks (both input and output)
        if (block.InputType != null && block.OutputType != null)
            return ("[/", "/]");  // Parallelogram for transforms/propagators

        // Unknown
        return ("{", "}");         // Rhombus for unknown
    }

    private static string GetBlockLabel(BlockDefinition block)
    {
        var label = block.Name;
        
        // Add type hints
        if (block.InputType != null && block.OutputType != null)
        {
            label += $"<br/>{block.InputType.Name} → {block.OutputType.Name}";
        }
        else if (block.OutputType != null)
        {
            label += $"<br/>→ {block.OutputType.Name}";
        }
        else if (block.InputType != null)
        {
            label += $"<br/>{block.InputType.Name} →";
        }

        return label;
    }

    private static string SanitizeId(string id)
    {
        // Replace invalid characters for Mermaid IDs
        return id.Replace("-", "_").Replace(" ", "_");
    }
}

/// <summary>
/// Summary information about a dataflow graph.
/// </summary>
public class DataFlowGraphSummary
{
    public string Name { get; init; } = string.Empty;
    public int BlockCount { get; init; }
    public int ConnectionCount { get; init; }
    public int SourceBlockCount { get; init; }
    public int TargetBlockCount { get; init; }
    public Dictionary<string, int> BlockTypes { get; init; } = new();
}
