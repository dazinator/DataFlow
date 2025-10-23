namespace Uniun.DataFlow.Builder.Graph;

using System;

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
    /// Renders routing block routes as Mermaid subgraphs using the context-based approach.
    /// </summary>
    public void RenderCustomContent(IBlockRenderContext context)
    {
        if (!context.Block.Metadata.TryGetValue("RouteDefinitions", out var routeDefsObj) ||
            routeDefsObj is not Dictionary<string, RouteDefinition> routeDefinitions)
        {
            context.AppendLine($"%% Routing block '{context.Block.Name}' has no static routes defined");
            return;
        }

        if (routeDefinitions.Count == 0)
        {
            context.AppendLine($"%% Routing block '{context.Block.Name}' has no static routes defined");
            return;
        }

        // Check if there's a merge target configured
        string? mergeTargetBlockName = null;
        if (context.Block.Metadata.TryGetValue("RoutingOptions", out var routingOptionsObj))
        {
            // Use reflection to get MergeIntoBlockName from the options object
            var mergeProperty = routingOptionsObj.GetType().GetProperty("MergeIntoBlockName");
            if (mergeProperty != null)
            {
                mergeTargetBlockName = mergeProperty.GetValue(routingOptionsObj) as string;
            }
        }

        context.AppendLine($"%% Routes for '{context.Block.Name}':");

        foreach (var route in routeDefinitions.Values)
        {
            var routeGraphName = $"Route: {route.Name}";
            var subgraphId = SanitizeId($"{context.Block.Name}_route_{route.Name}");

            context.AppendLine($"subgraph {subgraphId} [\"{routeGraphName}\"]");
            context.AppendLine($"    direction {context.Direction}");

            string? lastSourceBlockName = null;

            // Attempt to build the route structure to extract its graph
            if (context.ServiceProvider != null)
            {
                try
                {
                    var routeBuilder = new RouteBuilder(context.ServiceProvider, route.Name, parentGraph: null);
                    var routeContext = new RouteContext
                    {
                        RouteName = route.Name,
                        RouteDefinitionName = route.Name,
                        TriggeringItem = null,
                        ServiceProvider = context.ServiceProvider,
                        ParentGraph = new DataFlowGraph("Visualization"), // Temporary graph for visualization
                        RouteBuilder = routeBuilder,
                        IsDesignTime = true  // Indicate we're building for design-time visualization
                    };

                    // Call the factory to build the route structure
                    var routeBranch = route.Factory(routeContext);
                    
                    // Capture the last source block name for merge connections
                    // Use GetLastSourceBlockForMerge() which analyzes the graph to find terminal source blocks
                    if (routeBranch is IRouteBuilder asRouteBuilder)
                    {
                        lastSourceBlockName = asRouteBuilder.GetLastSourceBlockForMerge();
                    }
                    else
                    {
                        // Fallback to GetLastSourceBlockName for non-route branches
                        lastSourceBlockName = routeBranch.GetLastSourceBlockName();
                    }

                    // Now render the route's internal graph structure using nested context
                    var nestedContext = context.CreateNested();
                    RenderRouteGraph(nestedContext, routeBuilder.Graph);
                }
                catch (Exception)
                {
                    // If we can't build the route (e.g., due to DI dependencies), show a placeholder
                    var nestedContext = context.CreateNested();
                    nestedContext.AppendLine($"%% Route '{route.Name}' structure not available");
                    nestedContext.AppendLine($"%% Error building route (check service dependencies)");
                }
            }
            else
            {
                // No service provider - show placeholder
                var nestedContext = context.CreateNested();
                nestedContext.AppendLine($"%% Route '{route.Name}' blocks not shown");
                nestedContext.AppendLine($"%% (Pass IServiceProvider to ToMermaidDiagram to render route details)");
            }

            context.AppendLine($"end");
            context.AppendLine("");

            // Add connection from routing block to the route subgraph
            var routeInputType = SanitizeTypeLabel(route.ItemType.Name);
            context.AppendLine($"{SanitizeId(context.Block.Name)} -.->|{routeInputType}<br/>'{route.Name}'| {subgraphId}");
            
            // Add merge connection if configured and we have a last source block
            if (!string.IsNullOrEmpty(mergeTargetBlockName) && !string.IsNullOrEmpty(lastSourceBlockName))
            {
                // The connection goes from the last block in the route to the merge target
                var lastBlockId = SanitizeId(lastSourceBlockName);
                var mergeBlockId = SanitizeId(mergeTargetBlockName);
                
                // Determine the output type from the route's last source block
                // For now, use a generic label since we don't have easy access to the exact type
                context.AppendLine($"{lastBlockId} -.->|output| {mergeBlockId}");
            }
        }
    }

    /// <summary>
    /// Renders a route's internal graph structure using the context-based approach.
    /// </summary>
    private static void RenderRouteGraph(IBlockRenderContext context, DataFlowGraph routeGraph)
    {
        // Add route blocks
        foreach (var block in routeGraph.BlockDefinitions.Values)
        {
            var (Open, Close) = GetBlockShape(block);
            var label = GetBlockLabel(block);
            var blockId = SanitizeId(block.Name);

            // Mark entry blocks with a special indicator
            if (block.IsEntryBlock)
            {
                label = "[ENTRY] " + label;  // Entry point indicator
            }

            context.AppendLine($"{blockId}{Open}\"{label}\"{Close}");
        }

        context.AppendLine("");

        // Add route connections
        foreach (var connection in routeGraph.Connections)
        {
            var dataTypeLabel = SanitizeTypeLabel(connection.DataType?.Name ?? "data");
            context.AppendLine($"{SanitizeId(connection.SourceBlockName)} -->|{dataTypeLabel}| {SanitizeId(connection.TargetBlockName)}");
        }
    }

    private static (string Open, string Close) GetBlockShape(BlockDefinition block)
    {
        // Source blocks (no input)
        if (block.InputType == null && block.OutputType != null)
        {
            return ("([", "])");  // Stadium shape for sources
        }

        // Target blocks (no output)
        if (block.InputType != null && block.OutputType == null)
        {
            return ("[", "]");    // Rectangle for targets
        }

        // Propagator blocks (both input and output)
        if (block.InputType != null && block.OutputType != null)
        {
            return ("[/", "/]");  // Parallelogram for transforms/propagators
        }

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
