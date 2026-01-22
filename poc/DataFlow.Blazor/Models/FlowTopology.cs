namespace DataFlow.Blazor.Models;

/// <summary>
/// Represents the topology of a flow for visualization layout.
/// </summary>
public class FlowTopology
{
    public List<string> Layers { get; } = new();
    public Dictionary<string, List<string>> Connections { get; } = new();
    public Dictionary<string, (int Layer, int Position)> BlockPositions { get; } = new();
    
    /// <summary>
    /// Analyzes the flow state and builds a topology map.
    /// </summary>
    public static FlowTopology Analyze(Dictionary<string, Models.BlockState> blocks, Dictionary<string, ChannelState> channels)
    {
        var topology = new FlowTopology();
        
        // Build connection map from channel information
        // In the mock, channels are named after their source block
        foreach (var (blockName, _) in channels)
        {
            if (blocks.ContainsKey(blockName))
            {
                // Find blocks that could be downstream
                // For now, we'll use a simple heuristic based on block names
                foreach (var targetBlock in blocks.Keys)
                {
                    if (targetBlock != blockName)
                    {
                        if (!topology.Connections.ContainsKey(blockName))
                            topology.Connections[blockName] = new List<string>();
                        
                        // In a real implementation, this would come from flow definition
                        // For mock data, we infer connections
                    }
                }
            }
        }
        
        // For the mock sources, we'll detect patterns by block names
        var blockNames = blocks.Keys.ToList();
        
        // Detect fan-out pattern (e.g., router -> multiple processors)
        var routerBlock = blockNames.FirstOrDefault(b => b.Contains("router"));
        if (routerBlock != null)
        {
            var producers = blockNames.Where(b => b.Contains("producer")).ToList();
            var processors = blockNames.Where(b => b.Contains("processor")).ToList();
            
            topology.Connections[routerBlock] = processors;
            foreach (var producer in producers)
            {
                topology.Connections[producer] = new List<string> { routerBlock };
            }
        }
        // Detect fan-in pattern (e.g., multiple producers -> buffer)
        else if (blockNames.Count(b => b.Contains("producer")) > 1)
        {
            var producers = blockNames.Where(b => b.Contains("producer")).ToList();
            var buffer = blockNames.FirstOrDefault(b => b.Contains("buffer"));
            var batch = blockNames.FirstOrDefault(b => b.Contains("batch"));
            var processor = blockNames.FirstOrDefault(b => b.Contains("processor"));
            
            if (buffer != null)
            {
                foreach (var producer in producers)
                {
                    topology.Connections[producer] = new List<string> { buffer };
                }
                
                if (batch != null)
                {
                    topology.Connections[buffer] = new List<string> { batch };
                    if (processor != null)
                    {
                        topology.Connections[batch] = new List<string> { processor };
                    }
                }
            }
        }
        // Linear topology (default)
        else
        {
            for (int i = 0; i < blockNames.Count - 1; i++)
            {
                topology.Connections[blockNames[i]] = new List<string> { blockNames[i + 1] };
            }
        }
        
        // Calculate layout positions
        topology.CalculateLayouts(blocks);
        
        return topology;
    }
    
    private void CalculateLayouts(Dictionary<string, Models.BlockState> blocks)
    {
        var blockNames = blocks.Keys.ToList();
        var visited = new HashSet<string>();
        var layerMap = new Dictionary<int, List<string>>();
        
        // Simple layering: use topological sort
        var inDegree = blockNames.ToDictionary(b => b, b => 0);
        foreach (var (source, targets) in Connections)
        {
            foreach (var target in targets)
            {
                inDegree[target]++;
            }
        }
        
        var queue = new Queue<(string block, int layer)>();
        foreach (var block in blockNames.Where(b => inDegree[b] == 0))
        {
            queue.Enqueue((block, 0));
        }
        
        while (queue.Count > 0)
        {
            var (block, layer) = queue.Dequeue();
            if (visited.Contains(block)) continue;
            
            visited.Add(block);
            
            if (!layerMap.ContainsKey(layer))
                layerMap[layer] = new List<string>();
            layerMap[layer].Add(block);
            
            if (Connections.TryGetValue(block, out var targets))
            {
                foreach (var target in targets)
                {
                    queue.Enqueue((target, layer + 1));
                }
            }
        }
        
        // Assign positions within layers
        foreach (var (layer, blocksInLayer) in layerMap.OrderBy(kv => kv.Key))
        {
            for (int i = 0; i < blocksInLayer.Count; i++)
            {
                BlockPositions[blocksInLayer[i]] = (layer, i);
            }
        }
    }
}
