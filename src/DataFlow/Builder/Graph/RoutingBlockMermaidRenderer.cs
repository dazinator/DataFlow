namespace Uniun.DataFlow.Builder.Graph;

using System;
using System.Text;

/// <summary>
/// Mermaid renderer for routing blocks.
/// Renders routes as subgraphs with their internal block structure.
/// </summary>
public class RoutingBlockMermaidRenderer : IMermaidBlockRenderer
{
    /// <summary>
    /// Determines if this renderer can handle the given block definition.
    /// </summary>
    public bool CanRender(BlockDefinition block)
    {
        return block.Metadata.TryGetValue("IsRoutingBlock", out var isRoutingObj) &&
               isRoutingObj is bool isRouting && isRouting;
    }

    /// <summary>
    /// Renders routing block routes as Mermaid subgraphs.
    /// </summary>
    public void RenderCustomContent(StringBuilder sb, BlockDefinition block, string direction, IServiceProvider? serviceProvider)
    {
        if (!block.Metadata.TryGetValue("RouteDefinitions", out var routeDefsObj) ||
            routeDefsObj is not Dictionary<string, RouteDefinition> routeDefinitions)
        {
            sb.AppendLine($"    %% Routing block '{block.Name}' has no static routes defined");
            return;
        }

        if (routeDefinitions.Count == 0)
        {
            sb.AppendLine($"    %% Routing block '{block.Name}' has no static routes defined");
            return;
        }

        sb.AppendLine($"    %% Routes for '{block.Name}':");

        foreach (var route in routeDefinitions.Values)
        {
            var routeGraphName = $"Route: {route.Name}";
            var subgraphId = SanitizeId($"{block.Name}_route_{route.Name}");

            sb.AppendLine($"    subgraph {subgraphId} [\"{routeGraphName}\"]");
            sb.AppendLine($"        direction {direction}");

            // Attempt to build the route structure to extract its graph
            if (serviceProvider != null)
            {
                try
                {
                    var routeBuilder = new RouteBuilder(serviceProvider, route.Name);
                    var routeContext = new RouteContext
                    {
                        RouteName = route.Name,
                        RouteDefinitionName = route.Name,
                        TriggeringItem = null,
                        ServiceProvider = serviceProvider,
                        RouteBuilder = routeBuilder,
                        IsDesignTime = true  // Indicate we're building for design-time visualization
                    };

                    // Call the factory to build the route structure
                    // This populates the routeBuilder.Graph with block definitions and connections
                    var _ = route.Factory(routeContext);

                    // Now render the route's internal graph structure
                    RenderRouteGraph(sb, routeBuilder.Graph, "        ");
                }
                catch (Exception)
                {
                    // If we can't build the route (e.g., due to DI dependencies), show a placeholder
                    sb.AppendLine($"        %% Route '{route.Name}' structure not available");
                    sb.AppendLine($"        %% Error building route (check service dependencies)");
                }
            }
            else
            {
                // No service provider - show placeholder
                sb.AppendLine($"        %% Route '{route.Name}' blocks not shown");
                sb.AppendLine($"        %% (Pass IServiceProvider to ToMermaidDiagram to render route details)");
            }

            sb.AppendLine($"    end");
            sb.AppendLine();

            // Add connection from routing block to the route subgraph
            var routeInputType = SanitizeTypeLabel(route.ItemType.Name);
            sb.AppendLine($"    {SanitizeId(block.Name)} -.->|{routeInputType}<br/>'{route.Name}'| {subgraphId}");
        }
    }

    /// <summary>
    /// Renders a route's internal graph structure with proper indentation.
    /// </summary>
    private static void RenderRouteGraph(StringBuilder sb, DataFlowGraph routeGraph, string indent)
    {
        // Add route blocks
        foreach (var block in routeGraph.BlockDefinitions.Values)
        {
            var shape = GetBlockShape(block);
            var label = GetBlockLabel(block);
            var blockId = SanitizeId(block.Name);

            // Mark entry blocks with a special indicator
            if (block.IsEntryBlock)
            {
                label = "[ENTRY] " + label;  // Entry point indicator
            }

            sb.AppendLine($"{indent}{blockId}{shape.Open}\"{label}\"{shape.Close}");
        }

        sb.AppendLine();

        // Add route connections
        foreach (var connection in routeGraph.Connections)
        {
            var dataTypeLabel = SanitizeTypeLabel(connection.DataType?.Name ?? "data");
            sb.AppendLine($"{indent}{SanitizeId(connection.SourceBlockName)} -->|{dataTypeLabel}| {SanitizeId(connection.TargetBlockName)}");
        }
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

    private static string SanitizeTypeLabel(string typeLabel)
    {
        // Replace square brackets which are not valid in Mermaid edge labels
        // Convert array notation from T[] to T Array
        return typeLabel.Replace("[]", " Array").Replace("[", "").Replace("]", "");
    }
}
