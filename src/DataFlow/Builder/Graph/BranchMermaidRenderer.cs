namespace Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Renderer for branch groups in the dataflow diagram.
/// Handles rendering of branched execution paths with optional collapsing.
/// </summary>
public class BranchMermaidRenderer
{
    private readonly DiagramRenderOptions _options;

    public BranchMermaidRenderer(DiagramRenderOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Renders branches, deciding whether to show them individually or collapsed.
    /// </summary>
    /// <param name="context">The rendering context</param>
    /// <param name="branchGroups">Blocks grouped by branch name</param>
    /// <param name="graph">The dataflow graph</param>
    public void RenderBranches(
        IBlockRenderContext context,
        Dictionary<string, List<BlockDefinition>> branchGroups,
        DataFlowGraph graph)
    {
        // Group branches by their source block (branch families)
        var branchFamilies = GroupBranchesBySource(branchGroups, graph);

        foreach (var (sourceBlock, branches) in branchFamilies)
        {
            if (_options.CollapseConcurrentBranches && branches.Count > _options.MaxBranchesToShowIndividually)
            {
                RenderCollapsedBranches(context, branches, branchGroups);
            }
            else
            {
                RenderIndividualBranches(context, branches, branchGroups);
            }
        }
    }

    /// <summary>
    /// Renders individual branches, optionally as subgraphs.
    /// </summary>
    private void RenderIndividualBranches(
        IBlockRenderContext context,
        List<string> branches,
        Dictionary<string, List<BlockDefinition>> branchGroups)
    {
        foreach (var branchName in branches)
        {
            var branchBlocks = branchGroups[branchName];

            if (_options.GroupBranchesInSubgraphs)
            {
                RenderBranchAsSubgraph(context, branchName, branchBlocks);
            }
            else
            {
                // Render blocks without subgraph
                foreach (var block in branchBlocks)
                {
                    RenderBlock(context, block);
                }
            }
        }
    }

    /// <summary>
    /// Renders a branch as a subgraph containing its blocks.
    /// </summary>
    private void RenderBranchAsSubgraph(
        IBlockRenderContext context,
        string branchName,
        List<BlockDefinition> branchBlocks)
    {
        var subgraphId = SanitizeId($"branch_{branchName}");
        context.AppendLine($"subgraph {subgraphId} [\"Branch: {branchName}\"]");
        context.AppendLine($"    direction {context.Direction}");

        var nestedContext = context.CreateNested();
        foreach (var block in branchBlocks)
        {
            RenderBlock(nestedContext, block);
        }

        context.AppendLine($"end");
    }

    /// <summary>
    /// Renders collapsed branches with [×N] notation.
    /// </summary>
    private void RenderCollapsedBranches(
        IBlockRenderContext context,
        List<string> branches,
        Dictionary<string, List<BlockDefinition>> branchGroups)
    {
        var exampleBranch = branches[0];
        var exampleBlocks = branchGroups[exampleBranch];

        if (_options.GroupBranchesInSubgraphs)
        {
            // Render as a collapsed subgraph
            var subgraphId = SanitizeId($"branch_collapsed_{branches.Count}");
            context.AppendLine($"subgraph {subgraphId} [\"..{branches.Count}\"]");
            context.AppendLine($"    direction {context.Direction}");

            var nestedContext = context.CreateNested();
            foreach (var block in exampleBlocks)
            {
                var collapsedName = block.Name.Replace(exampleBranch, $"concurrent-x{branches.Count}");
                var (Open, Close) = GetBlockShape(block);
                var label = GetBlockLabel(block);
                nestedContext.AppendLine($"{SanitizeId(collapsedName)}{Open}\"{label}\"{Close}");
            }

            context.AppendLine($"end");
        }
        else
        {
            // Render as individual nodes with [×N] notation
            foreach (var block in exampleBlocks)
            {
                var collapsedName = block.Name.Replace(exampleBranch, $"concurrent-x{branches.Count}");
                var (Open, Close) = GetBlockShape(block);
                var label = GetBlockLabel(block) + $"<br/>[×{branches.Count}]";
                context.AppendLine($"{SanitizeId(collapsedName)}{Open}\"{label}\"{Close}");
            }
        }
    }

    /// <summary>
    /// Renders a single block using its shape and label.
    /// </summary>
    private void RenderBlock(IBlockRenderContext context, BlockDefinition block)
    {
        var (Open, Close) = GetBlockShape(block);
        var label = GetBlockLabel(block);
        context.AppendLine($"{SanitizeId(block.Name)}{Open}\"{label}\"{Close}");
    }

    /// <summary>
    /// Groups branches by their source block (the block they branch from).
    /// </summary>
    private Dictionary<string, List<string>> GroupBranchesBySource(
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
        return block.Name;
    }

    private static string SanitizeId(string id)
    {
        return id.Replace("-", "_").Replace(" ", "_");
    }
}
