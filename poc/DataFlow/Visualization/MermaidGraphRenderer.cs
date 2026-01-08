namespace DataFlow.POC.Visualization;

using System.Text;
using DataFlow.POC.Core;

/// <summary>
/// Renders dataflow graphs as Mermaid flowchart diagrams.
/// 
/// <para>
/// Edge styles indicate delivery semantics:
/// <list type="bullet">
/// <item><description>Solid arrows (-->) - Broadcast: all targets receive all items</description></item>
/// <item><description>Dotted arrows (-..->) - Competing: targets compete for items (each item consumed once)</description></item>
/// <item><description>Dotted arrows (-.->) with labeled subgraphs - Routed: items routed to targets based on route keys</description></item>
/// </list>
/// </para>
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

        // Collect blocks that are routed targets (will be rendered in subgraphs)
        var routedTargetBlocks = new HashSet<IBlock>();
        foreach (var edge in graph.Edges)
        {
            if (edge.Strategy.EdgeType == EdgeType.Routed && TryGetRouteMapping(edge.Strategy, out var routeMapping))
            {
                foreach (var target in routeMapping.Values)
                {
                    routedTargetBlocks.Add(target);
                }
            }
        }

        // Render epoch nodes if present
        if (options.ShowEpochNodes && graph.EpochSource != null)
        {
            RenderEpochSourceNode(sb, options);
        }

        // Render all blocks except those that will be in routing subgraphs
        foreach (var block in graph.Blocks)
        {
            if (!routedTargetBlocks.Contains(block))
            {
                RenderBlock(sb, block, options);
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

    private void RenderEdge(StringBuilder sb, Edge edge, GraphRenderOptions options)
    {
        var sourceId = SanitizeId(edge.SourceBlock.Name);
        var dataTypeLabel = SanitizeTypeLabel(edge.DataType.Name);
        var arrowStyle = GetArrowStyle(edge.Strategy.EdgeType);

        // Special handling for routing edges - use subgraphs
        if (edge.Strategy.EdgeType == EdgeType.Routed && TryGetRouteMapping(edge.Strategy, out var routeMapping))
        {
            RenderRoutingEdgeWithSubgraphs(sb, edge, sourceId, dataTypeLabel, routeMapping, options);
        }
        else
        {
            // Standard edge rendering
            foreach (var target in edge.TargetBlocks)
            {
                var targetId = SanitizeId(target.Name);
                var label = GetEdgeLabel(edge, dataTypeLabel, options);
                sb.AppendLine($"    {sourceId} {arrowStyle}|{label}| {targetId}");
            }
        }
    }

    private bool TryGetRouteMapping(EdgeStrategy strategy, out IReadOnlyDictionary<string, IBlock> routeMapping)
    {
        routeMapping = null!;
        
        // Use reflection to get the RouteKeyToBlock property from SelectiveRoutingEdgeStrategy<T>
        var strategyType = strategy.GetType();
        if (strategyType.IsGenericType && 
            strategyType.GetGenericTypeDefinition().Name == "SelectiveRoutingEdgeStrategy`1")
        {
            var property = strategyType.GetProperty("RouteKeyToBlock");
            if (property != null)
            {
                var value = property.GetValue(strategy);
                if (value is IReadOnlyDictionary<string, IBlock> mapping)
                {
                    routeMapping = mapping;
                    return true;
                }
            }
        }
        
        return false;
    }

    private void RenderRoutingEdgeWithSubgraphs(
        StringBuilder sb, 
        Edge edge, 
        string sourceId, 
        string dataTypeLabel, 
        IReadOnlyDictionary<string, IBlock> routeMapping, 
        GraphRenderOptions options)
    {
        sb.AppendLine();
        sb.AppendLine($"    %% Routes for '{edge.SourceBlock.Name}':");

        foreach (var (routeKey, targetBlock) in routeMapping)
        {
            var subgraphId = SanitizeId($"{edge.SourceBlock.Name}_route_{routeKey}");
            var routeGraphName = $"Route: {routeKey}";
            
            sb.AppendLine($"    subgraph {subgraphId} [\"{routeGraphName}\"]");
            sb.AppendLine($"        direction {options.Direction}");
            
            // Render the target block inside the subgraph
            var targetId = SanitizeId(targetBlock.Name);
            var (open, close) = GetBlockShape(targetBlock);
            var label = GetBlockLabel(targetBlock, options);
            sb.AppendLine($"        {targetId}{open}\"{label}\"{close}");
            
            sb.AppendLine($"    end");
            sb.AppendLine();
            
            // Add dotted connection from source to the subgraph with route key label
            sb.AppendLine($"    {sourceId} -.->|{dataTypeLabel}<br/>'{routeKey}'| {subgraphId}");
        }
        
        sb.AppendLine();
    }

    private string GetArrowStyle(EdgeType edgeType)
    {
        return edgeType switch
        {
            EdgeType.Broadcast => "-->",     // Solid arrow (default)
            EdgeType.Competing => "-.->",    // Dotted arrow (competing consumers)
            EdgeType.Routed => "-.->",       // Dotted arrow (routed edges use subgraphs, so this fallback matches the subgraph style)
            _ => "-->"
        };
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
