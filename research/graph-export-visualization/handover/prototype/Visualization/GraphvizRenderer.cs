namespace DataFlow.POC.Visualization;

using System.Text;
using DataFlow.POC.Core;

/// <summary>
/// Renders dataflow graphs in Graphviz DOT format.
/// </summary>
public class GraphvizRenderer : IGraphRenderer
{
    /// <summary>
    /// Renders the dataflow graph as a Graphviz DOT diagram.
    /// </summary>
    public string Render(DataFlowGraph graph, GraphRenderOptions? options = null)
    {
        options ??= new GraphRenderOptions();

        var sb = new StringBuilder();
        sb.AppendLine($"digraph \"{EscapeQuotes(graph.Name)}\" {{");
        sb.AppendLine($"    label=\"DataFlow: {EscapeQuotes(graph.Name)}\";");
        sb.AppendLine("    rankdir=LR;");
        sb.AppendLine("    node [fontname=\"Arial\"];");
        sb.AppendLine();

        // Render epoch nodes if present
        if (options.ShowEpochNodes && graph.EpochSource != null)
        {
            RenderEpochSourceNode(sb, options);
        }

        // Render all blocks
        foreach (var block in graph.Blocks)
        {
            RenderBlock(sb, block, options);
        }

        // Render buffer nodes if enabled
        if (options.ShowBufferNodes)
        {
            foreach (var buffer in graph.BufferNodes)
            {
                RenderBufferNode(sb, buffer, options);
            }
        }

        // Render epoch processor nodes if present
        if (options.ShowEpochNodes && graph.EpochProcessors.Count > 0)
        {
            for (int i = 0; i < graph.EpochProcessors.Count; i++)
            {
                sb.AppendLine($"    EpochProcessor_{i} [shape=hexagon, label=\"Epoch Processor {i}\", style=filled, fillcolor=\"#ffe8cc\"];");
            }
        }

        sb.AppendLine();

        // Render edges
        foreach (var edge in graph.Edges)
        {
            RenderEdge(sb, edge, options);
        }

        // Render buffer connections
        if (options.ShowBufferNodes)
        {
            foreach (var buffer in graph.BufferNodes)
            {
                var bufferId = GetBufferNodeId(buffer);

                // Render producer -> buffer connections
                foreach (var producer in graph.GetBufferProducers(buffer))
                {
                    RenderBufferConnection(sb, producer.Name, bufferId, buffer.DataType, options);
                }

                // Render buffer -> consumer connections
                foreach (var consumer in graph.GetBufferConsumers(buffer))
                {
                    RenderBufferConnection(sb, bufferId, consumer.Name, buffer.DataType, options);
                }
            }
        }

        // Render epoch connections if present
        if (options.ShowEpochNodes && graph.EpochSource != null && graph.EpochProcessors.Count > 0)
        {
            for (int i = 0; i < graph.EpochProcessors.Count; i++)
            {
                sb.AppendLine($"    EpochSource -> EpochProcessor_{i} [label=\"IEpoch\", style=dashed];");
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private void RenderBlock(StringBuilder sb, IBlock block, GraphRenderOptions options)
    {
        var shape = GetBlockShape(block);
        var label = GetBlockLabel(block, options);
        var id = SanitizeId(block.Name);

        sb.AppendLine($"    {id} [shape={shape}, label=\"{EscapeQuotes(label)}\"];");
    }

    private void RenderBufferNode(StringBuilder sb, BufferNode buffer, GraphRenderOptions options)
    {
        var id = SanitizeId(GetBufferNodeId(buffer));
        var label = GetBufferLabel(buffer, options);

        // Use cylinder shape for buffer nodes
        sb.AppendLine($"    {id} [shape=cylinder, label=\"{EscapeQuotes(label)}\", style=filled, fillcolor=\"#e8f4f8\"];");
    }

    private void RenderEdge(StringBuilder sb, Edge edge, GraphRenderOptions options)
    {
        var sourceId = SanitizeId(edge.SourceBlock.Name);
        var label = GetEdgeLabel(edge, options);

        foreach (var target in edge.TargetBlocks)
        {
            var targetId = SanitizeId(target.Name);
            sb.AppendLine($"    {sourceId} -> {targetId} [label=\"{EscapeQuotes(label)}\"];");
        }
    }

    private void RenderBufferConnection(StringBuilder sb, string sourceId, string targetId, 
        Type dataType, GraphRenderOptions options)
    {
        var label = dataType.Name;
        sb.AppendLine($"    {SanitizeId(sourceId)} -> {SanitizeId(targetId)} [label=\"{EscapeQuotes(label)}\"];");
    }

    private string GetBlockShape(IBlock block)
    {
        // Source blocks (object input, has typed output)
        if (block.InputType == typeof(object) && block.OutputType != typeof(object))
        {
            return "oval";  // Oval for sources
        }

        // Target blocks (has typed input, object output)
        if (block.InputType != typeof(object) && block.OutputType == typeof(object))
        {
            return "box";   // Box for targets
        }

        // Propagator blocks (both typed input and output)
        if (block.InputType != typeof(object) && block.OutputType != typeof(object))
        {
            return "parallelogram";  // Parallelogram for transforms
        }

        // Unknown
        return "diamond";  // Diamond for unknown
    }

    private string GetBlockLabel(IBlock block, GraphRenderOptions options)
    {
        if (!options.IncludeTypeInfo)
        {
            return block.Name;
        }

        // Include type information
        var parts = new List<string> { block.Name };

        if (block.InputType != typeof(object) && block.OutputType != typeof(object))
        {
            // Transform block - show input → output
            parts.Add($"\\n{GetShortTypeName(block.InputType)} → {GetShortTypeName(block.OutputType)}");
        }
        else if (block.OutputType != typeof(object))
        {
            // Source block - show output
            parts.Add($"\\n→ {GetShortTypeName(block.OutputType)}");
        }
        else if (block.InputType != typeof(object))
        {
            // Target block - show input
            parts.Add($"\\n{GetShortTypeName(block.InputType)} →");
        }

        return string.Join("", parts);
    }

    private string GetBufferLabel(BufferNode buffer, GraphRenderOptions options)
    {
        var name = buffer.Name ?? "Buffer";
        if (options.ShowBufferCapacity)
        {
            return $"{name}\\n[{buffer.Capacity}]";
        }
        return name;
    }

    private string GetBufferNodeId(BufferNode buffer)
    {
        return buffer.Name ?? $"buffer_{buffer.DataType.Name}";
    }

    private string GetEdgeLabel(Edge edge, GraphRenderOptions options)
    {
        var label = edge.DataType.Name;
        
        if (options.ShowBufferCapacity && edge.BufferMode == BufferMode.Bounded)
        {
            label += $" [{edge.BufferCapacity}]";
        }

        return label;
    }

    private string GetShortTypeName(Type type)
    {
        // Simplify common type names
        return type.Name switch
        {
            "Int32" => "int",
            "Int64" => "long",
            "String" => "string",
            "Boolean" => "bool",
            "Double" => "double",
            "Single" => "float",
            _ => type.Name
        };
    }

    private string SanitizeId(string id)
    {
        // Replace invalid characters for DOT IDs
        return id.Replace("-", "_")
                 .Replace(" ", "_")
                 .Replace(":", "_")
                 .Replace(".", "_");
    }

    private string EscapeQuotes(string text)
    {
        return text.Replace("\"", "\\\"");
    }

    private void RenderEpochSourceNode(StringBuilder sb, GraphRenderOptions options)
    {
        // Use hexagon shape for epoch source node with distinct color
        sb.AppendLine($"    EpochSource [shape=hexagon, label=\"Epoch Source\", style=filled, fillcolor=\"#ffe8cc\"];");
    }
}
