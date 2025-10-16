namespace Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Iterator for traversing a DataFlowGraph in topological order (DAG-based).
/// This allows systematic traversal of blocks from sources to targets.
/// </summary>
public class DataFlowGraphIterator
{
    private readonly DataFlowGraph _graph;

    public DataFlowGraphIterator(DataFlowGraph graph)
    {
        _graph = graph;
    }

    /// <summary>
    /// Iterates through the graph in topological order, visiting each block once.
    /// Blocks with no dependencies are visited first, followed by their dependents.
    /// </summary>
    /// <returns>Blocks in topological order</returns>
    public IEnumerable<BlockDefinition> IterateTopologically()
    {
        var visited = new HashSet<string>();
        var result = new List<BlockDefinition>();

        // Start with root blocks (those with no incoming connections)
        foreach (var rootBlock in _graph.GetRootBlocks())
        {
            VisitBlockTopologically(rootBlock, visited, result);
        }

        // Handle any remaining unvisited blocks (in case of disconnected subgraphs)
        foreach (var block in _graph.BlockDefinitions.Values)
        {
            if (!visited.Contains(block.Name))
            {
                VisitBlockTopologically(block, visited, result);
            }
        }

        return result;
    }

    private void VisitBlockTopologically(
        BlockDefinition block,
        HashSet<string> visited,
        List<BlockDefinition> result)
    {
        if (visited.Contains(block.Name))
        {
            return; // Already visited
        }

        visited.Add(block.Name);
        
        // Add this block to result BEFORE visiting children (pre-order traversal)
        // This ensures dependencies come before dependents in the result
        result.Add(block);

        // Then visit children recursively
        foreach (var connection in _graph.GetOutgoingConnections(block.Name))
        {
            var childBlock = _graph.GetBlockDefinition(connection.TargetBlockName);
            VisitBlockTopologically(childBlock, visited, result);
        }
    }

    /// <summary>
    /// Gets the immediate children (downstream blocks) of a given block.
    /// </summary>
    public IEnumerable<BlockDefinition> GetChildren(BlockDefinition block)
    {
        return _graph.GetOutgoingConnections(block.Name)
            .Select(c => _graph.GetBlockDefinition(c.TargetBlockName));
    }

    /// <summary>
    /// Gets the immediate parents (upstream blocks) of a given block.
    /// </summary>
    public IEnumerable<BlockDefinition> GetParents(BlockDefinition block)
    {
        return _graph.GetIncomingConnections(block.Name)
            .Select(c => _graph.GetBlockDefinition(c.SourceBlockName));
    }

    /// <summary>
    /// Determines if a block has multiple children (is a branching point).
    /// </summary>
    public bool IsBranchingPoint(BlockDefinition block)
    {
        return _graph.GetOutgoingConnections(block.Name).Count() > 1;
    }

    /// <summary>
    /// Groups blocks by their branch metadata.
    /// Returns blocks grouped by branch name, plus a collection of unbranched blocks.
    /// </summary>
    public (Dictionary<string, List<BlockDefinition>> branchGroups, List<BlockDefinition> unbranchedBlocks) GroupByBranch()
    {
        var branchGroups = new Dictionary<string, List<BlockDefinition>>();
        var unbranchedBlocks = new List<BlockDefinition>();

        foreach (var block in _graph.BlockDefinitions.Values)
        {
            if (block.Metadata.TryGetValue("BranchName", out var branchNameObj) && branchNameObj is string branchName)
            {
                if (!branchGroups.ContainsKey(branchName))
                {
                    branchGroups[branchName] = new List<BlockDefinition>();
                }
                branchGroups[branchName].Add(block);
            }
            else
            {
                unbranchedBlocks.Add(block);
            }
        }

        return (branchGroups, unbranchedBlocks);
    }
}
