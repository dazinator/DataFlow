namespace DataFlow.POC.Visualization;

using DataFlow.POC.Core;

/// <summary>
/// Extension methods for querying DataFlowGraph structure.
/// Provides read-only traversal capabilities for graph visualization and analysis.
/// </summary>
public static class GraphQueryExtensions
{
    /// <summary>
    /// Gets all source blocks (blocks with no incoming edges).
    /// </summary>
    public static IEnumerable<IBlock> GetSourceBlocks(this DataFlowGraph graph)
    {
        var blocksWithIncoming = new HashSet<IBlock>();

        // Add blocks that have incoming edges
        foreach (var block in graph.Blocks)
        {
            if (graph.GetIncomingEdges(block).Any())
            {
                blocksWithIncoming.Add(block);
            }
        }

        return graph.Blocks.Where(b => !blocksWithIncoming.Contains(b));
    }

    /// <summary>
    /// Gets all target blocks (blocks with no outgoing edges).
    /// </summary>
    public static IEnumerable<IBlock> GetTargetBlocks(this DataFlowGraph graph)
    {
        var blocksWithOutgoing = new HashSet<IBlock>();

        // Add blocks that have outgoing edges
        foreach (var block in graph.Blocks)
        {
            if (graph.GetOutgoingEdges(block).Any())
            {
                blocksWithOutgoing.Add(block);
            }
        }

        return graph.Blocks.Where(b => !blocksWithOutgoing.Contains(b));
    }

    /// <summary>
    /// Gets blocks in topological order (sources first, then dependents).
    /// </summary>
    public static IEnumerable<IBlock> GetTopologicalOrder(this DataFlowGraph graph)
    {
        var visited = new HashSet<IBlock>();
        var result = new List<IBlock>();

        // Start with source blocks
        foreach (var source in graph.GetSourceBlocks())
        {
            VisitBlock(source, visited, result, graph);
        }

        // Handle any remaining blocks (in case of cycles or disconnected subgraphs)
        foreach (var block in graph.Blocks)
        {
            if (!visited.Contains(block))
            {
                VisitBlock(block, visited, result, graph);
            }
        }

        return result;
    }

    private static void VisitBlock(
        IBlock block,
        HashSet<IBlock> visited,
        List<IBlock> result,
        DataFlowGraph graph)
    {
        if (visited.Contains(block))
        {
            return;
        }

        visited.Add(block);
        result.Add(block);

        // Visit downstream blocks (edges)
        foreach (var edge in graph.GetOutgoingEdges(block))
        {
            foreach (var target in edge.TargetBlocks)
            {
                VisitBlock(target, visited, result, graph);
            }
        }
    }
}
