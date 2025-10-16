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

        // Group blocks by branch
        var branchGroups = GroupBlocksByBranch(graph);
        var blocksToRender = new HashSet<string>();
        var collapsedBranchInfo = new Dictionary<string, (string collapsedName, BlockDefinition exampleBlock, int count)>();
        var branchesToRenderAsSubgraphs = new Dictionary<string, List<BlockDefinition>>();

        if (branchGroups.Count > 0)
        {
            ProcessBranches(graph, branchGroups, options, blocksToRender, collapsedBranchInfo, branchesToRenderAsSubgraphs);
        }
        else
        {
            // Render all blocks normally
            foreach (var block in graph.BlockDefinitions.Values)
            {
                blocksToRender.Add(block.Name);
            }
        }

        // Add non-branch nodes (blocks without BranchName metadata)
        foreach (var block in graph.BlockDefinitions.Values)
        {
            if (blocksToRender.Contains(block.Name) && !block.Metadata.ContainsKey("BranchName"))
            {
                var shape = GetBlockShape(block);
                var label = GetBlockLabel(block);
                sb.AppendLine($"    {SanitizeId(block.Name)}{shape.Open}\"{label}\"{shape.Close}");
            }
        }

        // Add branches as subgraphs if not collapsed
        if (branchesToRenderAsSubgraphs.Count > 0)
        {
            sb.AppendLine();
            foreach (var (branchName, branchBlocks) in branchesToRenderAsSubgraphs.OrderBy(kvp => kvp.Key))
            {
                var subgraphId = SanitizeId($"branch_{branchName}");
                sb.AppendLine($"    subgraph {subgraphId} [\"Branch: {branchName}\"]");
                sb.AppendLine($"        direction {direction}");
                
                // Render blocks within the branch
                foreach (var block in branchBlocks)
                {
                    var shape = GetBlockShape(block);
                    var label = GetBlockLabel(block);
                    sb.AppendLine($"        {SanitizeId(block.Name)}{shape.Open}\"{label}\"{shape.Close}");
                }
                
                sb.AppendLine($"    end");
            }
        }

        // Add collapsed branch subgraphs
        foreach (var (sourceBlock, (collapsedName, exampleBlock, count)) in collapsedBranchInfo)
        {
            if (options.GroupBranchesInSubgraphs)
            {
                // Render collapsed branches as a subgraph with simplified name
                var subgraphId = SanitizeId($"branch_collapsed_{count}");
                sb.AppendLine($"    subgraph {subgraphId} [\"..{count}\"]");
                sb.AppendLine($"        direction {direction}");
                
                // Render the example block within the subgraph
                var shape = GetBlockShape(exampleBlock);
                var label = GetBlockLabel(exampleBlock);
                sb.AppendLine($"        {SanitizeId(collapsedName)}{shape.Open}\"{label}\"{shape.Close}");
                
                sb.AppendLine($"    end");
            }
            else
            {
                // Render as a single node with [×N] notation
                var shape = GetBlockShape(exampleBlock);
                var label = GetBlockLabel(exampleBlock) + $"<br/>[×{count}]";
                sb.AppendLine($"    {SanitizeId(collapsedName)}{shape.Open}\"{label}\"{shape.Close}");
            }
        }

        sb.AppendLine();

        // Add edges
        foreach (var connection in graph.Connections)
        {
            // Only render connections where both blocks are being rendered
            if (blocksToRender.Contains(connection.SourceBlockName) && 
                blocksToRender.Contains(connection.TargetBlockName))
            {
                var dataTypeLabel = SanitizeTypeLabel(connection.DataType?.Name ?? "data");
                sb.AppendLine($"    {SanitizeId(connection.SourceBlockName)} -->|{dataTypeLabel}| {SanitizeId(connection.TargetBlockName)}");
            }
        }

        // Add collapsed branch connections
        foreach (var (sourceBlock, (collapsedName, exampleBlock, count)) in collapsedBranchInfo)
        {
            var incomingConn = graph.GetIncomingConnections(exampleBlock.Name).FirstOrDefault();
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
            if (blocksToRender.Contains(block.Name))
            {
                foreach (var renderer in _customRenderers)
                {
                    if (renderer.CanRender(block))
                    {
                        sb.AppendLine();
                        renderer.RenderCustomContent(sb, block, direction, serviceProvider);
                        break; // Only apply the first matching renderer
                    }
                }
            }
        }

        return sb.ToString();
    }

    private static Dictionary<string, List<BlockDefinition>> GroupBlocksByBranch(DataFlowGraph graph)
    {
        var branchGroups = new Dictionary<string, List<BlockDefinition>>();

        foreach (var block in graph.BlockDefinitions.Values)
        {
            if (block.Metadata.TryGetValue("BranchName", out var branchNameObj) && branchNameObj is string branchName)
            {
                if (!branchGroups.ContainsKey(branchName))
                {
                    branchGroups[branchName] = new List<BlockDefinition>();
                }
                branchGroups[branchName].Add(block);
            }
        }

        return branchGroups;
    }

    private static void ProcessBranches(
        DataFlowGraph graph,
        Dictionary<string, List<BlockDefinition>> branchGroups,
        DiagramRenderOptions options,
        HashSet<string> blocksToRender,
        Dictionary<string, (string collapsedName, BlockDefinition exampleBlock, int count)> collapsedBranchInfo,
        Dictionary<string, List<BlockDefinition>> branchesToRenderAsSubgraphs)
    {
        // First, add blocks that are NOT in any branch
        foreach (var block in graph.BlockDefinitions.Values)
        {
            if (!block.Metadata.ContainsKey("BranchName"))
            {
                blocksToRender.Add(block.Name);
            }
        }

        // Now handle branches
        // Group branches that connect to the same source block
        var branchFamilies = new Dictionary<string, List<string>>();
        
        foreach (var branchName in branchGroups.Keys)
        {
            var branchBlocks = branchGroups[branchName];
            if (branchBlocks.Count == 0) continue;

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

        // For each branch family, decide whether to collapse
        foreach (var (sourceBlock, branches) in branchFamilies)
        {
            if (options.CollapseConcurrentBranches && branches.Count > options.MaxBranchesToShowIndividually)
            {
                // Collapse these branches
                var exampleBranch = branches[0];
                var exampleBlocks = branchGroups[exampleBranch];
                
                // Create collapsed representation for each block in the branch
                foreach (var block in exampleBlocks)
                {
                    var collapsedName = block.Name.Replace(exampleBranch, $"concurrent-x{branches.Count}");
                    collapsedBranchInfo[collapsedName] = (collapsedName, block, branches.Count);
                }
            }
            else
            {
                // Show all branches individually (as subgraphs if option enabled)
                foreach (var branchName in branches)
                {
                    var branchBlocks = branchGroups[branchName];
                    
                    if (options.GroupBranchesInSubgraphs)
                    {
                        // Render as subgraph
                        branchesToRenderAsSubgraphs[branchName] = branchBlocks;
                    }
                    
                    // Still add to blocksToRender for connection processing
                    foreach (var block in branchBlocks)
                    {
                        blocksToRender.Add(block.Name);
                    }
                }
            }
        }
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
