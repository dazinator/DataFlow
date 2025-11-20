namespace DataFlow.POC.Visualization;

using System.Text;
using DataFlow.POC.Core;

/// <summary>
/// Renders dataflow graphs as Mermaid flowchart diagrams.
/// </summary>
public class MermaidGraphRenderer : IGraphRenderer
{
    /// <summary>
    /// Renders the dataflow graph as a Mermaid flowchart.
    /// </summary>
    public string Render(DataFlowGraph graph, GraphRenderOptions? options = null)
    {
        options ??= new GraphRenderOptions();

        var sb = new StringBuilder();
        sb.AppendLine($"flowchart {options.Direction}");
        sb.AppendLine($"    %% DataFlow: {graph.Name}");
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
                // Use hexagon shape for epoch processor nodes
                sb.AppendLine($"    EpochProcessor_{i}{{{{\"Epoch Processor {i}\"}}}};");
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
                // Render producer -> buffer connections
                foreach (var producer in graph.GetBufferProducers(buffer))
                {
                    RenderBufferConnection(sb, producer.Name, GetBufferNodeId(buffer), 
                        buffer.DataType, options);
                }

                // Render buffer -> consumer connections
                foreach (var consumer in graph.GetBufferConsumers(buffer))
                {
                    RenderBufferConnection(sb, GetBufferNodeId(buffer), consumer.Name, 
                        buffer.DataType, options);
                }
            }
        }

        // Render epoch connections if present
        if (options.ShowEpochNodes && graph.EpochSource != null && graph.EpochProcessors.Count > 0)
        {
            for (int i = 0; i < graph.EpochProcessors.Count; i++)
            {
                sb.AppendLine($"    EpochSource -.->|IEpoch| EpochProcessor_{i}");
            }
        }

        return sb.ToString();
    }

    private void RenderBlock(StringBuilder sb, IBlock block, GraphRenderOptions options)
    {
        var (open, close) = GetBlockShape(block);
        var label = GetBlockLabel(block, options);
        sb.AppendLine($"    {SanitizeId(block.Name)}{open}\"{label}\"{close}");
    }

    private void RenderBufferNode(StringBuilder sb, BufferNode buffer, GraphRenderOptions options)
    {
        var nodeId = GetBufferNodeId(buffer);
        var label = GetBufferLabel(buffer, options);
        // Use cylinder shape for buffer nodes [(name)]
        sb.AppendLine($"    {SanitizeId(nodeId)}[(\"{label}\")]");
    }

    private void RenderEdge(StringBuilder sb, Edge edge, GraphRenderOptions options)
    {
        var sourceId = SanitizeId(edge.SourceBlock.Name);
        var dataTypeLabel = SanitizeTypeLabel(edge.DataType.Name);

        foreach (var target in edge.TargetBlocks)
        {
            var targetId = SanitizeId(target.Name);
            var label = GetEdgeLabel(edge, dataTypeLabel, options);
            sb.AppendLine($"    {sourceId} -->|{label}| {targetId}");
        }
    }

    private void RenderBufferConnection(StringBuilder sb, string sourceId, string targetId, 
        Type dataType, GraphRenderOptions options)
    {
        var dataTypeLabel = SanitizeTypeLabel(dataType.Name);
        sb.AppendLine($"    {SanitizeId(sourceId)} -->|{dataTypeLabel}| {SanitizeId(targetId)}");
    }

    private (string open, string close) GetBlockShape(IBlock block)
    {
        // Source blocks (object input, has typed output)
        if (block.InputType == typeof(object) && block.OutputType != typeof(object))
        {
            return ("([", "])");  // Stadium shape for sources
        }

        // Target blocks (has typed input, object output)
        if (block.InputType != typeof(object) && block.OutputType == typeof(object))
        {
            return ("[", "]");    // Rectangle for targets
        }

        // Propagator blocks (both typed input and output)
        if (block.InputType != typeof(object) && block.OutputType != typeof(object))
        {
            return ("[/", "/]");  // Parallelogram for transforms/propagators
        }

        // Unknown
        return ("{", "}");         // Rhombus for unknown
    }

    private string GetBlockLabel(IBlock block, GraphRenderOptions options)
    {
        if (!options.IncludeTypeInfo)
        {
            return block.Name;
        }

        // Include type information in a subtle way
        var parts = new List<string> { block.Name };
        
        if (block.InputType != typeof(object) && block.OutputType != typeof(object))
        {
            // Transform block - show input → output
            parts.Add($"<br/><small>{GetShortTypeName(block.InputType)} → {GetShortTypeName(block.OutputType)}</small>");
        }
        else if (block.OutputType != typeof(object))
        {
            // Source block - show output
            parts.Add($"<br/><small>→ {GetShortTypeName(block.OutputType)}</small>");
        }
        else if (block.InputType != typeof(object))
        {
            // Target block - show input
            parts.Add($"<br/><small>{GetShortTypeName(block.InputType)} →</small>");
        }

        return string.Join("", parts);
    }

    private string GetBufferLabel(BufferNode buffer, GraphRenderOptions options)
    {
        var name = buffer.Name ?? "Buffer";
        if (options.ShowBufferCapacity)
        {
            return $"{name}<br/><small>[{buffer.Capacity}]</small>";
        }
        return name;
    }

    private string GetBufferNodeId(BufferNode buffer)
    {
        return buffer.Name ?? $"buffer_{buffer.DataType.Name}";
    }

    private string GetEdgeLabel(Edge edge, string dataTypeLabel, GraphRenderOptions options)
    {
        var parts = new List<string> { dataTypeLabel };
        
        if (options.ShowBufferCapacity && edge.BufferMode == BufferMode.Bounded)
        {
            parts.Add($"[{edge.BufferCapacity}]");
        }

        return string.Join(" ", parts);
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
        // Replace invalid characters for Mermaid IDs
        return id.Replace("-", "_")
                 .Replace(" ", "_")
                 .Replace(":", "_")
                 .Replace(".", "_");
    }

    private string SanitizeTypeLabel(string typeLabel)
    {
        // Replace square brackets which are not valid in Mermaid edge labels
        // Convert array notation from T[] to T Array
        return typeLabel.Replace("[]", " Array")
                        .Replace("[", "")
                        .Replace("]", "");
    }

    private void RenderEpochSourceNode(StringBuilder sb, GraphRenderOptions options)
    {
        // Use hexagon shape for epoch source node
        sb.AppendLine($"    EpochSource{{{{\"Epoch Source\"}}}};");
    }
}
