# Graph Structure Comparison: Production vs POC

## Production DataFlowGraph (src/DataFlow/Builder/Graph/)

### Key Classes
- `DataFlowGraph` - Main graph container
- `BlockDefinition` - Represents a block with metadata
- `BlockConnection` - Represents edges between blocks
- `DataFlowGraphIterator` - DAG-based traversal
- `DataFlowGraphExporter` - Export functionality (static extension methods)

### Structure
```csharp
public class DataFlowGraph
{
    public string Name { get; }
    public Dictionary<string, BlockDefinition> BlockDefinitions { get; }
    public List<BlockConnection> Connections { get; }
    
    // Methods for querying structure
    public IEnumerable<BlockDefinition> GetSourceBlocks()
    public IEnumerable<BlockDefinition> GetTargetBlocks()
    public IEnumerable<BlockDefinition> GetRootBlocks()
    public IEnumerable<BlockConnection> GetIncomingConnections(string blockName)
    public IEnumerable<BlockConnection> GetOutgoingConnections(string blockName)
}

public class BlockDefinition
{
    public string Name { get; }
    public Type BlockType { get; }
    public Type? InputType { get; }
    public Type? OutputType { get; }
    public Dictionary<string, object> Metadata { get; }
}

public class BlockConnection
{
    public string SourceBlockName { get; }
    public string TargetBlockName { get; }
    public Type? DataType { get; }
}
```

### Features
- Branch support with metadata
- Routing blocks with custom renderers
- Broadcast blocks
- Topological iteration
- Supports collapsing concurrent branches
- Extensible renderer system via `IMermaidBlockRenderer`

## POC DataFlowGraph (poc/DataFlow.POC/Core/)

### Key Classes
- `DataFlowGraph` - Main graph container
- `IBlock` - Block interface (not a definition class)
- `Edge` - Represents connections between blocks
- `BufferNode` - Shared channel nodes

### Structure
```csharp
public class DataFlowGraph
{
    public string Name { get; }
    public IReadOnlyList<IBlock> Blocks { get; }
    public IReadOnlyList<BufferNode> BufferNodes { get; }
    public IReadOnlyList<Edge> Edges { get; }
    
    // Internal tracking
    private readonly Dictionary<IBlock, List<Edge>> _outgoingEdges
    private readonly Dictionary<IBlock, List<Edge>> _incomingEdges
    private readonly Dictionary<BufferNode, List<IBlock>> _bufferProducers
    private readonly Dictionary<BufferNode, List<IBlock>> _bufferConsumers
}

public interface IBlock
{
    string Name { get; }
    Type InputType { get; }
    Type OutputType { get; }
    // ... actor methods
}

public class Edge
{
    public IBlock Source { get; }
    public IBlock Target { get; } // or IReadOnlyList<IBlock> for multi-target
    public IEdgeStrategy Strategy { get; }
}

public class BufferNode
{
    public Type DataType { get; }
    public int Capacity { get; }
    public string? Name { get; }
}
```

### Key Differences

1. **Block Representation**
   - Production: Uses `BlockDefinition` (metadata object)
   - POC: Uses actual `IBlock` instances (runtime objects)

2. **Connections**
   - Production: `BlockConnection` with block names (strings)
   - POC: `Edge` with actual block references (objects)

3. **Additional Concepts in POC**
   - `BufferNode` - Shared channel concept (not in production)
   - `EdgeStrategy` - Different edge behaviors (competing, cloning, etc.)
   - Epoch-related nodes (EpochSourceNode, EpochProcessorNode)

4. **Metadata**
   - Production: Explicit `Metadata` dictionary on `BlockDefinition`
   - POC: Block instances themselves carry state

5. **Graph Queries**
   - Production: Built-in query methods for sources, targets, connections
   - POC: Internal dictionaries, but no public query API

## Implications for Export

### Challenges
1. POC graph doesn't have a `BlockDefinition` abstraction - export must work with `IBlock` instances
2. No existing public API for graph traversal/queries
3. Buffer nodes are a unique concept that need representation in diagrams
4. Edge strategies might need different visual representation

### Opportunities
1. Can create a graph query API as part of this work
2. Buffer nodes could be represented as special nodes in diagrams
3. Edge strategies could be shown as edge labels or styles
4. Simpler than production in some ways (no routing blocks with sub-routes yet)

### Recommended Approach
1. Add public query methods to `DataFlowGraph` (or create extension methods)
2. Create a graph iterator/traversal helper (inspired by production's `DataFlowGraphIterator`)
3. Build renderer abstraction that works with `IBlock` instances
4. Represent buffer nodes as special diagram nodes
5. Keep edge strategies simple initially (just show data type)

## Next Steps
1. Design graph query API extension methods
2. Design renderer interface and architecture
3. Prototype Mermaid exporter
