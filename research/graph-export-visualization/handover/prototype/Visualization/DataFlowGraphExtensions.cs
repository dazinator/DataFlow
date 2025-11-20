namespace DataFlow.POC.Visualization;

using DataFlow.POC.Core;

/// <summary>
/// Extension methods for convenient graph visualization.
/// </summary>
public static class DataFlowGraphExtensions
{
    /// <summary>
    /// Exports the dataflow graph as a Mermaid flowchart diagram.
    /// </summary>
    /// <param name="graph">The dataflow graph</param>
    /// <param name="direction">Flowchart direction (LR, TB, RL, BT)</param>
    /// <param name="options">Optional rendering options</param>
    /// <returns>Mermaid diagram as a string</returns>
    public static string ToMermaidDiagram(
        this DataFlowGraph graph, 
        string direction = "LR",
        GraphRenderOptions? options = null)
    {
        options ??= new GraphRenderOptions();
        options.Direction = direction;

        var renderer = new MermaidGraphRenderer();
        return renderer.Render(graph, options);
    }

    /// <summary>
    /// Exports the dataflow graph in Graphviz DOT format.
    /// </summary>
    /// <param name="graph">The dataflow graph</param>
    /// <param name="options">Optional rendering options</param>
    /// <returns>DOT format diagram as a string</returns>
    public static string ToGraphviz(
        this DataFlowGraph graph,
        GraphRenderOptions? options = null)
    {
        var renderer = new GraphvizRenderer();
        return renderer.Render(graph, options);
    }

    /// <summary>
    /// Generates a simple text summary of the graph structure.
    /// </summary>
    /// <param name="graph">The dataflow graph</param>
    /// <returns>Text summary of the graph</returns>
    public static string ToTextSummary(this DataFlowGraph graph)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"DataFlow: {graph.Name}");
        sb.AppendLine($"Blocks: {graph.Blocks.Count}");
        sb.AppendLine($"Edges: {graph.Edges.Count}");
        sb.AppendLine($"Buffer Nodes: {graph.BufferNodes.Count}");
        sb.AppendLine();

        sb.AppendLine("Block List:");
        foreach (var block in graph.Blocks)
        {
            sb.AppendLine($"  - {block.Name}");
            sb.AppendLine($"      Input: {block.InputType.Name}");
            sb.AppendLine($"      Output: {block.OutputType.Name}");
        }

        if (graph.BufferNodes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Buffer Nodes:");
            foreach (var buffer in graph.BufferNodes)
            {
                sb.AppendLine($"  - {buffer.Name ?? buffer.DataType.Name}");
                sb.AppendLine($"      Type: {buffer.DataType.Name}");
                sb.AppendLine($"      Capacity: {buffer.Capacity}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Edges:");
        foreach (var edge in graph.Edges)
        {
            foreach (var target in edge.TargetBlocks)
            {
                sb.AppendLine($"  {edge.SourceBlock.Name} --({edge.DataType.Name})--> {target.Name}");
            }
        }

        return sb.ToString();
    }
}
