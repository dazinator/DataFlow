namespace Uniun.DataFlow.Builder.Graph;

using System;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Utilities for visualizing and exporting dataflow graphs.
/// </summary>
public static class DataFlowGraphExporter
{
    private static readonly List<IMermaidBlockRenderer> _customRenderers = new()
    {
        new RoutingBlockMermaidRenderer(),
        new BroadcastBlockMermaidRenderer()
        // Additional custom renderers can be added here for other block types
    };

    /// <summary>
    /// Registers a custom Mermaid renderer for specific block types.
    /// This allows extensions to add custom rendering logic for new block types.
    /// </summary>
    /// <param name="renderer">The custom renderer to register</param>
    public static void RegisterCustomRenderer(IMermaidBlockRenderer renderer)
    {
        if (!_customRenderers.Contains(renderer))
        {
            _customRenderers.Add(renderer);
        }
    }

    /// <summary>
    /// Generates a Mermaid diagram representation of the dataflow graph.
    /// This can be used in documentation, rendered on GitHub, or used with Mermaid tools.
    /// Uses DAG-based iteration for systematic traversal of the graph structure.
    /// </summary>
    /// <param name="graph">The dataflow graph to export</param>
    /// <param name="direction">The direction of the flowchart (LR, RL, TB, BT)</param>
    /// <param name="serviceProvider">Optional service provider for rendering route details</param>
    /// <param name="options">Optional rendering options to control diagram appearance</param>
    /// <returns>Mermaid diagram as a string</returns>
    public static string ToMermaidDiagram(
        this DataFlowGraph graph,
        string direction = "LR",
        IServiceProvider? serviceProvider = null,
        DiagramRenderOptions? options = null)
    {
        options ??= new DiagramRenderOptions();

        var sb = new StringBuilder();
        sb.AppendLine($"flowchart {direction}");
        sb.AppendLine($"    %% DataFlow: {graph.Name}");
        sb.AppendLine();

        // Create root rendering context
        var rootContext = new BlockRenderContext(sb, null!, direction, serviceProvider, "    ");

        // Use DAG iterator to organize the graph
        var iterator = new DataFlowGraphIterator(graph);
        var (branchGroups, unbranchedBlocks) = iterator.GroupByBranch();

        // Track which blocks are rendered and which connections should be shown
        var renderedBlocks = new HashSet<string>();
        var collapsedBranchInfo = new Dictionary<string, (string collapsedName, BlockDefinition exampleBlock, int count)>();

        // Render unbranched blocks first
        foreach (var block in unbranchedBlocks)
        {
            RenderBlockNode(rootContext, block);
            renderedBlocks.Add(block.Name);
        }

        // Render branches using the branch renderer
        if (branchGroups.Count > 0)
        {
            // Determine which branches to collapse
            var branchFamilies = GroupBranchesBySource(branchGroups, graph);

            foreach (var (sourceBlock, branches) in branchFamilies)
            {
                if (options.CollapseConcurrentBranches && branches.Count > options.MaxBranchesToShowIndividually)
                {
                    // Collapse these branches
                    var exampleBranch = branches[0];
                    var exampleBlocks = branchGroups[exampleBranch];

                    RenderCollapsedBranches(rootContext, options, branches, exampleBlocks, collapsedBranchInfo);

                    // Track that these branches are collapsed (don't render individual connections)
                    foreach (var branchName in branches)
                    {
                        foreach (var block in branchGroups[branchName])
                        {
                            renderedBlocks.Add(block.Name);
                        }
                    }
                }
                else
                {
                    // Render individual branches
                    RenderIndividualBranches(rootContext, options, branches, branchGroups, renderedBlocks);
                }
            }
        }

        sb.AppendLine();

        // Track which blocks were rendered as part of collapsed branches
        var collapsedBlocks = new HashSet<string>();
        if (collapsedBranchInfo.Count > 0)
        {
            // For each collapsed branch family, add all the actual block names (from all branches)
            var branchFamilies = GroupBranchesBySource(branchGroups, graph);
            foreach (var (sourceBlock, branches) in branchFamilies)
            {
                if (options.CollapseConcurrentBranches && branches.Count > options.MaxBranchesToShowIndividually)
                {
                    // Add all blocks from all collapsed branches
                    foreach (var branchName in branches)
                    {
                        if (branchGroups.TryGetValue(branchName, out var blocks))
                        {
                            foreach (var block in blocks)
                            {
                                collapsedBlocks.Add(block.Name);
                            }
                        }
                    }
                }
            }
        }

        // Add edges/connections (only for rendered blocks that aren't collapsed)
        foreach (var connection in graph.Connections)
        {
            // Check if the target block is part of a collapsed branch
            if (collapsedBlocks.Contains(connection.TargetBlockName))
            {
                // Skip this connection - we'll render it to the collapsed representation instead
                continue;
            }

            if (renderedBlocks.Contains(connection.SourceBlockName) &&
                renderedBlocks.Contains(connection.TargetBlockName))
            {
                // Normal connection between rendered blocks
                var dataTypeLabel = SanitizeTypeLabel(connection.DataType?.Name ?? "data");
                sb.AppendLine($"    {SanitizeId(connection.SourceBlockName)} -->|{dataTypeLabel}| {SanitizeId(connection.TargetBlockName)}");
            }
        }

        // Add connections to collapsed branches
        foreach (var (blockName, (collapsedName, exampleBlock, count)) in collapsedBranchInfo)
        {
            var incomingConn = graph.GetIncomingConnections(blockName).FirstOrDefault();
            if (incomingConn != null)
            {
                var dataTypeLabel = SanitizeTypeLabel(incomingConn.DataType?.Name ?? "data");
                sb.AppendLine($"    {SanitizeId(incomingConn.SourceBlockName)} -->|{dataTypeLabel}| {SanitizeId(collapsedName)}");
            }
        }

        // Add notes for collapsed branches
        if (collapsedBranchInfo.Count > 0)
        {
            sb.AppendLine();
            foreach (var (_, (_, _, count)) in collapsedBranchInfo)
            {
                sb.AppendLine($"    %% Note: {count} concurrent branches collapsed");
            }
        }

        // Apply custom renderers for blocks that need special rendering
        foreach (var block in graph.BlockDefinitions.Values)
        {
            foreach (var renderer in _customRenderers)
            {
                if (renderer.CanRender(block))
                {
                    sb.AppendLine();
                    var blockContext = new BlockRenderContext(sb, block, direction, serviceProvider, "    ");
                    renderer.RenderCustomContent(blockContext);
                    break; // Only apply the first matching renderer
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Groups branches by their source block.
    /// </summary>
    private static Dictionary<string, List<string>> GroupBranchesBySource(
        Dictionary<string, List<BlockDefinition>> branchGroups,
        DataFlowGraph graph)
    {
        var branchFamilies = new Dictionary<string, List<string>>();

        foreach (var branchName in branchGroups.Keys)
        {
            var branchBlocks = branchGroups[branchName];
            if (branchBlocks.Count == 0)
            {
                continue;
            }

            // Find the source block this branch connects to
            var firstBlock = branchBlocks[0];
            var incomingConnections = graph.GetIncomingConnections(firstBlock.Name);
            var sourceBlock = incomingConnections.FirstOrDefault()?.SourceBlockName;

            if (sourceBlock != null)
            {
                if (!branchFamilies.ContainsKey(sourceBlock))
                {
                    branchFamilies[sourceBlock] = new List<string>();
                }
                branchFamilies[sourceBlock].Add(branchName);
            }
        }

        return branchFamilies;
    }

    /// <summary>
    /// Renders individual branches (not collapsed).
    /// </summary>
    private static void RenderIndividualBranches(
        IBlockRenderContext context,
        DiagramRenderOptions options,
        List<string> branches,
        Dictionary<string, List<BlockDefinition>> branchGroups,
        HashSet<string> renderedBlocks)
    {
        // Add empty line before first branch if rendering as subgraphs
        var firstBranch = true;

        foreach (var branchName in branches)
        {
            var branchBlocks = branchGroups[branchName];

            if (options.GroupBranchesInSubgraphs)
            {
                // Add empty line before first subgraph
                if (firstBranch)
                {
                    context.AppendLine("");
                    firstBranch = false;
                }

                // Render as subgraph
                var subgraphId = SanitizeId($"branch_{branchName}");
                context.AppendLine($"subgraph {subgraphId} [\"Branch: {branchName}\"]");
                context.AppendLine($"    direction {context.Direction}");

                var nestedContext = context.CreateNested();
                foreach (var block in branchBlocks)
                {
                    RenderBlockNode(nestedContext, block);
                }

                context.AppendLine($"end");
            }
            else
            {
                // Render blocks without subgraph
                foreach (var block in branchBlocks)
                {
                    RenderBlockNode(context, block);
                }
            }

            // Track rendered blocks
            foreach (var block in branchBlocks)
            {
                renderedBlocks.Add(block.Name);
            }
        }
    }

    /// <summary>
    /// Renders collapsed branches with [×N] notation.
    /// </summary>
    private static void RenderCollapsedBranches(
        IBlockRenderContext context,
        DiagramRenderOptions options,
        List<string> branches,
        List<BlockDefinition> exampleBlocks,
        Dictionary<string, (string collapsedName, BlockDefinition exampleBlock, int count)> collapsedBranchInfo)
    {
        var exampleBranch = branches[0];
        var count = branches.Count;

        if (options.GroupBranchesInSubgraphs)
        {
            // Render as a collapsed subgraph
            var subgraphId = SanitizeId($"branch_collapsed_{count}");
            context.AppendLine($"subgraph {subgraphId} [\"..{count}\"]");
            context.AppendLine($"    direction {context.Direction}");

            var nestedContext = context.CreateNested();
            foreach (var block in exampleBlocks)
            {
                var collapsedName = block.Name.Replace(exampleBranch, $"concurrent-x{count}");
                var (Open, Close) = GetBlockShape(block);
                var label = GetBlockLabel(block);
                nestedContext.AppendLine($"{SanitizeId(collapsedName)}{Open}\"{label}\"{Close}");

                // Track collapsed block info - use the FIRST block name from the example branch
                // The key is the block name without branch identifier
                var blockKey = block.Name;
                if (!collapsedBranchInfo.ContainsKey(blockKey))
                {
                    collapsedBranchInfo[blockKey] = (collapsedName, block, count);
                }
            }

            context.AppendLine($"end");
        }
        else
        {
            // Render as individual nodes with [×N] notation
            foreach (var block in exampleBlocks)
            {
                var collapsedName = block.Name.Replace(exampleBranch, $"concurrent-x{count}");
                var (Open, Close) = GetBlockShape(block);
                var label = GetBlockLabel(block) + $"<br/>[×{count}]";
                context.AppendLine($"{SanitizeId(collapsedName)}{Open}\"{label}\"{Close}");

                // Track collapsed block info
                var blockKey = block.Name;
                if (!collapsedBranchInfo.ContainsKey(blockKey))
                {
                    collapsedBranchInfo[blockKey] = (collapsedName, block, count);
                }
            }
        }
    }

    /// <summary>
    /// Renders a single block node in the diagram.
    /// </summary>
    private static void RenderBlockNode(IBlockRenderContext context, BlockDefinition block)
    {
        var (Open, Close) = GetBlockShape(block);
        var label = GetBlockLabel(block);
        context.AppendLine($"{SanitizeId(block.Name)}{Open}\"{label}\"{Close}");
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
            {
                sb.AppendLine($"      Input: {block.InputType.Name}");
            }

            if (block.OutputType != null)
            {
                sb.AppendLine($"      Output: {block.OutputType.Name}");
            }
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
        // Just return the block name - type information is shown on connections
        return block.Name;
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
