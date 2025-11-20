# Renderer Architecture Design

## Overview

The graph export system follows a strategy pattern with three main components:

1. **Graph Query API** - Extension methods to traverse the POC graph
2. **Renderer Interface** - Abstract interface for different export formats
3. **Concrete Renderers** - Specific implementations (Mermaid, Graphviz, etc.)

## Architecture

```
┌─────────────────────────────────────┐
│     DataFlowGraph (POC)             │
│  - Blocks                           │
│  - Edges                            │
│  - BufferNodes                      │
│  - EpochSource (optional)           │
│  - EpochProcessors (optional)       │
└──────────────┬──────────────────────┘
               │
               │ uses
               ▼
┌─────────────────────────────────────┐
│  GraphQueryExtensions               │
│  - GetSourceBlocks()                │
│  - GetTargetBlocks()                │
│  - GetIncoming/OutgoingEdges()      │
│  - TopologicalSort()                │
└──────────────┬──────────────────────┘
               │
               │ uses
               ▼
┌─────────────────────────────────────┐
│  IGraphRenderer (Interface)         │
│  + Render(DataFlowGraph): string    │
└──────────────┬──────────────────────┘
               │
               │ implements
               ▼
  ┌────────────┴────────────┐
  │                         │
  ▼                         ▼
┌──────────────┐    ┌──────────────┐
│  Mermaid     │    │  Graphviz    │
│  Renderer    │    │  Renderer    │
└──────────────┘    └──────────────┘
```

## Component Responsibilities

### GraphQueryExtensions
- Provides read-only query methods for graph traversal
- Does NOT modify graph structure
- Static extension methods on `DataFlowGraph`
- Enables iteration patterns needed by renderers

### IGraphRenderer
```csharp
public interface IGraphRenderer
{
    /// <summary>
    /// Renders the dataflow graph to a text-based diagram format.
    /// </summary>
    string Render(DataFlowGraph graph, GraphRenderOptions? options = null);
}

public class GraphRenderOptions
{
    /// <summary>
    /// Direction for flowcharts (LR, TB, RL, BT) - Mermaid-specific
    /// </summary>
    public string Direction { get; set; } = "LR";
    
    /// <summary>
    /// Whether to include type information in labels
    /// </summary>
    public bool IncludeTypeInfo { get; set; } = true;
    
    /// <summary>
    /// Whether to show buffer nodes explicitly
    /// </summary>
    public bool ShowBufferNodes { get; set; } = true;
    
    /// <summary>
    /// Whether to show epoch nodes (POC-specific)
    /// </summary>
    public bool ShowEpochNodes { get; set; } = true;
}
```

### MermaidGraphRenderer
- Implements `IGraphRenderer`
- Generates Mermaid flowchart syntax
- Uses different shapes for different block types:
  - Source blocks (no input): Stadium shape `([...])`
  - Target blocks (no output): Rectangle `[...]`
  - Transform/propagator blocks: Parallelogram `[/.../]`
  - Buffer nodes: Cylinder `[(...)]`
  - Epoch nodes: Hexagon `{{}}`

### GraphvizRenderer
- Implements `IGraphRenderer`
- Generates DOT format (Graphviz)
- Uses DOT language features:
  - `digraph` for directed graphs
  - Different node shapes via `shape` attribute
  - Edge labels for data types

## POC-Specific Features

### Epoch Nodes
The POC codebase has epoch processing features not present in production code:

- **EpochSourceNode**: Manages epoch stream and publishes epochs for processing
- **EpochProcessorNode**: Processes epochs by draining operation queues

These nodes are visualized when present in a graph and the `ShowEpochNodes` option is enabled (default: true).

**Visual Representation**:
- Hexagon shape to distinguish from regular blocks
- Dashed edges to show epoch flow (IEpoch type)
- Epoch processors numbered (0, 1, 2...) when multiple exist

**Current Limitations**:
- Epoch policy metadata (e.g., policy name, item count, time windows) is not currently exposed on the nodes
- The `EpochConfiguration` used to create the nodes is not retained after graph construction
- Future enhancement could store policy metadata on `EpochSourceNode` for visualization

**Implementation Note**: 
`DataFlowGraph` exposes epoch nodes via public properties:
- `EpochSource` - The epoch source node (null if epochs not configured)
- `EpochProcessors` - List of epoch processor nodes

### Routing
The POC codebase implements routing differently than production:

**Production Approach**:
- Routing blocks with subgraph visualization
- Routes defined with factories that build sub-pipelines
- Mermaid subgraphs show route structure
- Collapse feature when routes exceed threshold

**POC Approach**:
- Edge-level routing via `SelectiveRoutingEdgeStrategy`
- Routes defined by selector function (item → route key)
- Multiple edges from source to route targets
- Simpler than production's structured routing blocks

**Visual Representation in POC**:
- Source block connects to multiple target blocks via separate edges
- Each edge represents a potential route
- Edge labels show data type
- No subgraph nesting (routes are just edges)

**Routing vs Broadcast**:
- Routing (`SelectiveRoutingEdgeStrategy`): Item goes to ONE target based on selector
- Broadcast (`BroadcastEdgeStrategy`): Item goes to ALL targets
- Visualization is identical (both show N edges), semantics differ

**Collapse Feature**:
Production has `CollapseConcurrentBranches` to collapse many similar branches into a summary (e.g., "processor [x100]"). POC doesn't currently need this as routing is edge-based rather than block-based. Future enhancement could add:
- `MaxRoutesToShow` - Limit number of route edges visualized
- `CollapseRoutes` - Show "N routes" indicator instead of individual edges
- Requires edge metadata to identify routing edges vs regular edges

## Design Decisions

### Decision 1: Graph Query Methods

**Choice**: Add public methods to `DataFlowGraph` for O(1) lookups, use extension methods for higher-level traversal

**Rationale**:
- Public methods (`GetBufferProducers`, `GetIncomingEdges`, etc.) expose already-tracked internal data
- Enables efficient O(1) lookups without re-scanning
- Keeps query API close to data structure
- Extension methods handle higher-level logic (topological sort, source/target detection)
- Hybrid approach: core queries on class, convenience methods as extensions

### Decision 2: Extension Methods vs Graph Methods (High-Level)

**Choice**: Use static extension methods for visualization and high-level traversal

**Rationale**:
- Keeps POC `DataFlowGraph` focused on core functionality
- Aligns with production approach (static `DataFlowGraphExporter`)
- Easy to discover via IntelliSense
- Doesn't pollute the core graph API

### Decision 3: Renderer Interface vs Static Methods

**Choice**: Use interface-based renderers

**Rationale**:
- More testable (can mock renderers)
- Easier to extend (add new renderers without modifying existing code)
- Can inject renderers if needed (DI-friendly)
- Production uses mixed approach, but interface is cleaner for POC

### Decision 4: Buffer Node Representation

**Choice**: Represent buffer nodes as explicit diagram nodes

**Rationale**:
- Buffer nodes are a first-class concept in POC
- Important for understanding graph topology
- Helps visualize shared channels
- Can be toggled off via options if not needed

### Decision 4: Graph Traversal

**Choice**: Simple topological sort, no complex iterator class initially

**Rationale**:
- POC graph structure is simpler than production
- Can add sophistication later if needed
- Start minimal, extend as required

### Decision 5: Visitor Pattern

**Choice**: NO - use simple iteration

**Rationale**:
- Visitor pattern adds complexity without clear benefit here
- POC block types are not as diverse as production
- Simple iteration is more readable
- Can reconsider if block type diversity increases

## Implementation Plan

### Phase 1: Graph Query API
```csharp
// In GraphQueryExtensions.cs
public static class GraphQueryExtensions
{
    public static IEnumerable<IBlock> GetSourceBlocks(this DataFlowGraph graph)
    public static IEnumerable<IBlock> GetTargetBlocks(this DataFlowGraph graph)
    public static IEnumerable<Edge> GetOutgoingEdges(this DataFlowGraph graph, IBlock block)
    public static IEnumerable<Edge> GetIncomingEdges(this DataFlowGraph graph, IBlock block)
    public static IEnumerable<IBlock> GetTopologicalOrder(this DataFlowGraph graph)
}
```

### Phase 2: Renderer Interface & Base
```csharp
// In IGraphRenderer.cs
public interface IGraphRenderer
{
    string Render(DataFlowGraph graph, GraphRenderOptions? options = null);
}

// In GraphRenderOptions.cs
public class GraphRenderOptions { ... }
```

### Phase 3: Mermaid Renderer
```csharp
// In MermaidGraphRenderer.cs
public class MermaidGraphRenderer : IGraphRenderer
{
    public string Render(DataFlowGraph graph, GraphRenderOptions? options = null)
    {
        // Build Mermaid flowchart
    }
}
```

### Phase 4: Graphviz Renderer
```csharp
// In GraphvizRenderer.cs
public class GraphvizRenderer : IGraphRenderer
{
    public string Render(DataFlowGraph graph, GraphRenderOptions? options = null)
    {
        // Build DOT format
    }
}
```

### Phase 5: Extension Method for Convenience
```csharp
// In DataFlowGraphExtensions.cs
public static class DataFlowGraphExtensions
{
    public static string ToMermaidDiagram(this DataFlowGraph graph, string direction = "LR")
    {
        var renderer = new MermaidGraphRenderer();
        return renderer.Render(graph, new GraphRenderOptions { Direction = direction });
    }
    
    public static string ToGraphviz(this DataFlowGraph graph)
    {
        var renderer = new GraphvizRenderer();
        return renderer.Render(graph);
    }
}
```

## File Organization

```
poc/DataFlow.POC/
└── Visualization/              # New folder
    ├── GraphQueryExtensions.cs
    ├── IGraphRenderer.cs
    ├── GraphRenderOptions.cs
    ├── MermaidGraphRenderer.cs
    ├── GraphvizRenderer.cs
    └── DataFlowGraphExtensions.cs   # Convenience methods
```

## Testing Strategy

Use Verify.Xunit for snapshot testing:

```csharp
[Fact]
public Task Should_RenderSimpleFlow_AsMermaid()
{
    // Arrange
    var graph = CreateSimpleGraph();
    
    // Act
    var mermaid = graph.ToMermaidDiagram();
    
    // Assert
    return Verify(mermaid);
}
```

Test scenarios:
1. Simple source → transform → target flow
2. Broadcast to multiple targets
3. Graph with buffer nodes
4. Complex flow with multiple paths
5. **Routing with SelectiveRoutingEdgeStrategy** (even/odd routing)
6. Direction variants (LR, TB, RL, BT)
7. Rendering options (hide types, hide buffers, hide epochs)

**Note on Routing Tests**: POC routing differs from production. POC uses edge-level routing (`SelectiveRoutingEdgeStrategy`) where a selector function determines which target receives each item. Production uses structured routing blocks with sub-pipelines. Both are tested but visualize differently.

## Next Steps

1. Implement GraphQueryExtensions
2. Implement IGraphRenderer and options
3. Implement MermaidGraphRenderer
4. Add Verify.Xunit to POC test project
5. Create snapshot tests
6. Implement GraphvizRenderer
7. Validate and document
