namespace Uniun.DataFlow.Builder.Graph;

using Microsoft.Extensions.Logging;

/// <summary>
/// Represents the structure of a dataflow as a directed acyclic graph (DAG).
/// This holds the metadata about blocks and their connections before they are instantiated.
/// </summary>
public class DataFlowGraph
{
    private readonly Dictionary<string, BlockDefinition> _blockDefinitions = new();
    private readonly List<BlockConnection> _connections = new();

    public DataFlowGraph(string name)
    {
        Name = name;
    }

    /// <summary>
    /// The name of the dataflow.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// All block definitions in the dataflow, keyed by block name.
    /// </summary>
    public IReadOnlyDictionary<string, BlockDefinition> BlockDefinitions => _blockDefinitions;

    /// <summary>
    /// All connections between blocks in the dataflow.
    /// </summary>
    public IReadOnlyList<BlockConnection> Connections => _connections;

    /// <summary>
    /// Gets all blocks marked as entry blocks.
    /// Entry blocks are target blocks where routed items can enter.
    /// </summary>
    public IEnumerable<BlockDefinition> GetEntryBlocks()
    {
        return _blockDefinitions.Values.Where(b => b.IsEntryBlock && b.IsTargetBlock());
    }

    /// <summary>
    /// Adds a block definition to the graph.
    /// </summary>
    public void AddBlockDefinition(BlockDefinition definition)
    {
        if (_blockDefinitions.ContainsKey(definition.Name))
        {
            throw new ArgumentException($"Block with name '{definition.Name}' already exists", nameof(definition));
        }

        _blockDefinitions[definition.Name] = definition;
    }

    /// <summary>
    /// Adds a connection between two blocks.
    /// </summary>
    public void AddConnection(BlockConnection connection)
    {
        if (!_blockDefinitions.ContainsKey(connection.SourceBlockName))
        {
            throw new InvalidOperationException($"Source block '{connection.SourceBlockName}' does not exist");
        }

        if (!_blockDefinitions.ContainsKey(connection.TargetBlockName))
        {
            throw new InvalidOperationException($"Target block '{connection.TargetBlockName}' does not exist");
        }

        _connections.Add(connection);
    }

    /// <summary>
    /// Gets the block definition by name.
    /// </summary>
    public BlockDefinition GetBlockDefinition(string name)
    {
        if (!_blockDefinitions.TryGetValue(name, out var definition))
        {
            throw new InvalidOperationException($"Block '{name}' not found in graph");
        }

        return definition;
    }

    /// <summary>
    /// Gets all source blocks (blocks that implement ISourceBlock).
    /// Source blocks with incoming connections are considered invalid but still returned by this method.
    /// </summary>
    public IEnumerable<BlockDefinition> GetSourceBlocks()
    {
        return _blockDefinitions.Values.Where(b => b.IsSourceBlock());
    }

    /// <summary>
    /// Gets all target blocks (blocks that implement ITargetBlock).
    /// This includes propagator blocks which are both source and target blocks.
    /// </summary>
    public IEnumerable<BlockDefinition> GetTargetBlocks()
    {
        return _blockDefinitions.Values.Where(b => b.IsTargetBlock());
    }

    /// <summary>
    /// Gets all root blocks (blocks with no incoming connections, regardless of type).
    /// </summary>
    public IEnumerable<BlockDefinition> GetRootBlocks()
    {
        var blocksWithIncomingConnections = _connections.Select(c => c.TargetBlockName).ToHashSet();
        return _blockDefinitions.Values.Where(b => !blocksWithIncomingConnections.Contains(b.Name));
    }

    /// <summary>
    /// Gets all leaf blocks (blocks with no outgoing connections, regardless of type).
    /// </summary>
    public IEnumerable<BlockDefinition> GetLeafBlocks()
    {
        var blocksWithOutgoingConnections = _connections.Select(c => c.SourceBlockName).ToHashSet();
        return _blockDefinitions.Values.Where(b => !blocksWithOutgoingConnections.Contains(b.Name));
    }

    /// <summary>
    /// Gets all connections where the specified block is the source.
    /// </summary>
    public IEnumerable<BlockConnection> GetOutgoingConnections(string blockName)
    {
        return _connections.Where(c => c.SourceBlockName == blockName);
    }

    /// <summary>
    /// Gets all connections where the specified block is the target.
    /// </summary>
    public IEnumerable<BlockConnection> GetIncomingConnections(string blockName)
    {
        return _connections.Where(c => c.TargetBlockName == blockName);
    }

    /// <summary>
    /// Validates the graph structure for common issues.
    /// </summary>
    /// <param name="logger">Optional logger for validation warnings</param>
    public void Validate(Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        // Check for blocks with no connections
        var connectedBlocks = _connections
            .SelectMany(c => new[] { c.SourceBlockName, c.TargetBlockName })
            .ToHashSet();

        var disconnectedBlocks = _blockDefinitions.Keys.Except(connectedBlocks).ToList();

        // Warn if we have more than one block and some are disconnected
        if (_blockDefinitions.Count > 1 && disconnectedBlocks.Any())
        {
            var message = $"Graph '{Name}' contains {disconnectedBlocks.Count} disconnected block(s): {string.Join(", ", disconnectedBlocks)}. This may be intentional.";
            logger?.LogWarning(message);
        }

        // Check for multiple sources to single target (most blocks don't support this yet)
        var blocksWithMultipleSources = _connections
            .GroupBy(c => c.TargetBlockName)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var blockName in blocksWithMultipleSources)
        {
            var blockDef = _blockDefinitions[blockName];

            // Check if this block explicitly supports multiple sources via metadata
            var supportsMultipleSources = blockDef.Metadata.ContainsKey("SupportsMultipleSources")
                && blockDef.Metadata["SupportsMultipleSources"] is bool supports
                && supports;

            if (!supportsMultipleSources)
            {
                throw new InvalidOperationException(
                    $"Block '{blockName}' has multiple incoming connections. " +
                    $"Most blocks do not support multiple source blocks yet. " +
                    $"Consider using a buffer or merge block to combine multiple sources.");
            }
        }

        // Check for cycles (basic check - could be enhanced)
        foreach (var block in _blockDefinitions.Keys)
        {
            if (HasCycle(block, new HashSet<string>()))
            {
                throw new InvalidOperationException($"Cycle detected in dataflow graph involving block '{block}'");
            }
        }
    }

    private bool HasCycle(string blockName, HashSet<string> visited)
    {
        if (visited.Contains(blockName))
        {
            return true;
        }

        visited.Add(blockName);

        foreach (var connection in GetOutgoingConnections(blockName))
        {
            if (HasCycle(connection.TargetBlockName, new HashSet<string>(visited)))
            {
                return true;
            }
        }

        return false;
    }
}
