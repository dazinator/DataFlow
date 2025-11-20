# Implementation Issue: Graph Export to Mermaid and Graphviz

## Overview

Implement extensible graph visualization export functionality for the POC codebase, supporting Mermaid flowchart and Graphviz/DOT formats with snapshot testing.

## Background

Research validated the approach through working prototypes (see `/research/graph-export-visualization/`). This issue tracks the implementation of the production-ready feature based on validated architecture.

**Research PR**: #[PR_NUMBER]
**Research Issue**: #520

## Requirements

### Functional Requirements

1. **Export Formats**
   - Mermaid flowchart diagrams (feature parity with production code)
   - Graphviz DOT format diagrams
   - Extensible architecture for adding new formats

2. **POC-Specific Features**
   - Visualize epoch nodes (EpochSourceNode, EpochProcessorNode)
   - Visualize buffer nodes (shared channels)
   - Visualize routing edges (SelectiveRoutingEdgeStrategy)
   - Handle POC graph structure (IBlock instances vs production's BlockDefinition)

3. **Rendering Options**
   - Configurable diagram direction (LR, TB, RL, BT)
   - Toggle type information display
   - Toggle buffer node display
   - Toggle epoch node display

4. **Testing**
   - Snapshot testing with Verify.Xunit
   - Test coverage for all graph patterns:
     - Simple flows (source → transform → target)
     - Broadcast flows (1→N)
     - Buffer node patterns
     - Routing patterns
     - Complex multi-path flows
     - Direction variants
     - Rendering option combinations

### Non-Functional Requirements

1. **Performance**: O(1) graph queries for renderer efficiency
2. **Maintainability**: Clean separation of concerns, each renderer < 250 lines
3. **Extensibility**: New formats via interface implementation
4. **Compatibility**: Works with existing POC graph structure

## Architecture

### Core Components

```
poc/DataFlow.POC/Visualization/
├── IGraphRenderer.cs           # Renderer interface
├── GraphRenderOptions.cs       # Configuration options
├── MermaidGraphRenderer.cs     # Mermaid implementation
├── GraphvizRenderer.cs         # Graphviz implementation
├── GraphQueryExtensions.cs     # Graph traversal utilities
└── DataFlowGraphExtensions.cs  # Convenience methods
```

### Interface Design

```csharp
public interface IGraphRenderer
{
    string Render(DataFlowGraph graph, GraphRenderOptions? options = null);
}

public class GraphRenderOptions
{
    public string Direction { get; set; } = "LR";
    public bool IncludeTypeInfo { get; set; } = true;
    public bool ShowBufferNodes { get; set; } = true;
    public bool ShowEpochNodes { get; set; } = true;
    public bool ShowBufferCapacity { get; set; } = false;
}
```

### DataFlowGraph Changes

Add public query methods to `DataFlowGraph`:

```csharp
// Epoch node access
public EpochSourceNode? EpochSource { get; }
public IReadOnlyList<EpochProcessorNode> EpochProcessors { get; }

// Graph query methods (O(1) lookups using internal dictionaries)
public IEnumerable<IBlock> GetBufferProducers(BufferNode buffer)
public IEnumerable<IBlock> GetBufferConsumers(BufferNode buffer)
public IEnumerable<Edge> GetOutgoingEdges(IBlock block)
public IEnumerable<Edge> GetIncomingEdges(IBlock block)
```

**Rationale**: These methods expose already-tracked internal data, enabling efficient renderer queries without re-scanning.

### Extension Methods

High-level traversal in `GraphQueryExtensions`:

```csharp
public static IEnumerable<IBlock> GetSourceBlocks(this DataFlowGraph graph)
public static IEnumerable<IBlock> GetTargetBlocks(this DataFlowGraph graph)
public static IEnumerable<IBlock> GetTopologicalOrder(this DataFlowGraph graph)
```

Convenience methods in `DataFlowGraphExtensions`:

```csharp
public static string ToMermaidDiagram(this DataFlowGraph graph, string direction = "LR", GraphRenderOptions? options = null)
public static string ToGraphviz(this DataFlowGraph graph, GraphRenderOptions? options = null)
public static string ToTextSummary(this DataFlowGraph graph)
```

## Implementation Details

### Mermaid Renderer

**Node Shapes**:
- Source blocks (no input): Stadium `([...])`
- Target blocks (no output): Rectangle `[...]`
- Transform blocks: Parallelogram `[/.../]`
- Buffer nodes: Cylinder `[(...)]`
- Epoch nodes: Hexagon `{{}}`

**Edge Styles**:
- Regular data flow: Solid arrows `-->`
- Epoch flow: Dashed arrows `-.->` with IEpoch label

**Features**:
- Type annotations in labels (configurable)
- ID sanitization for Mermaid syntax
- Direction control (LR, TB, RL, BT)

### Graphviz Renderer

**Node Shapes**:
- Source blocks: `shape=oval`
- Target blocks: `shape=box`
- Transform blocks: `shape=parallelogram`
- Buffer nodes: `shape=cylinder, fillcolor="#e8f4f8"`
- Epoch nodes: `shape=hexagon, fillcolor="#ffe8cc"`

**Features**:
- DOT-compliant formatting
- Edge labels for data types
- Dashed edges for epoch flow
- Proper escaping

### Graph Query Extensions

**Traversal Logic**:
- Source detection: Blocks with no incoming edges and not consuming from buffers
- Target detection: Blocks with no outgoing edges and not producing to buffers
- Topological sort: DAG traversal starting from sources

**Buffer Awareness**:
- Track buffer producers and consumers
- Represent buffers as explicit nodes in diagrams

**Epoch Awareness**:
- Detect and visualize epoch source and processors
- Show epoch flow with dashed edges

## Testing Strategy

### Test Infrastructure

1. Add `Verify.Xunit` package to POC test project:
   ```xml
   <PackageReference Include="Verify.Xunit" Version="26.6.0" />
   ```

2. Update `xunit` to v2.9.0 for compatibility

### Test Scenarios

Create `GraphVisualizationTests.cs` with snapshot tests:

```csharp
[Fact]
public Task Should_RenderSimpleFlow_AsMermaid()
{
    var graph = CreateSimpleFlowGraph();
    var mermaid = graph.ToMermaidDiagram();
    return Verify(mermaid).UseFileName("SimpleFlow_Mermaid");
}
```

**Required Test Coverage**:
- ✅ Simple flow (source → transform → target)
- ✅ Flow with buffer nodes
- ✅ Broadcast flow (1→N)
- ✅ Routing flow (SelectiveRoutingEdgeStrategy)
- ✅ Complex flow (multi-source + merge + broadcast)
- ✅ Direction variants (LR, TB, RL, BT)
- ✅ Option tests (hide type info, hide buffer nodes, hide epoch nodes)
- ✅ Both Mermaid and Graphviz for key scenarios

### Snapshot Approval

1. Run tests to generate `.received.txt` files
2. Review diagram output for correctness
3. Rename to `.verified.txt` or use Verify tooling
4. Commit verified snapshots

## POC vs Production Differences

### Routing

**Production**: Structured routing blocks with sub-pipelines visualized in subgraphs
**POC**: Edge-level routing via `SelectiveRoutingEdgeStrategy`

**Visualization Difference**:
- Production: Subgraphs show route structure
- POC: Multiple edges from source to route targets (simpler)

**Collapse Feature**:
- Production: `CollapseConcurrentBranches` to collapse many similar branches
- POC: Not needed currently (edge-based routing), but could add `MaxRoutesToShow` / `CollapseRoutes` as future enhancement

### Graph Structure

**Production**: `BlockDefinition` (metadata objects)
**POC**: `IBlock` instances (runtime objects)

**Impact**: POC renderers work with actual block instances, not metadata wrappers

### Unique POC Features

- **Buffer Nodes**: Shared channel concept not in production
- **Epoch Nodes**: Transaction/checkpoint concept not in production
- **Edge Strategies**: Different edge behaviors (competing, broadcast, routing)

## Implementation Steps

### Phase 1: Core Infrastructure (1-2 days)

1. Add public query methods to `DataFlowGraph`
   - `EpochSource`, `EpochProcessors` properties
   - `GetBufferProducers`, `GetBufferConsumers` methods
   - `GetOutgoingEdges`, `GetIncomingEdges` methods

2. Create `Visualization` folder structure
3. Implement `IGraphRenderer` interface
4. Implement `GraphRenderOptions` class

### Phase 2: Graph Query Extensions (1 day)

1. Implement `GraphQueryExtensions`
   - Source/target detection
   - Topological ordering
   - Buffer-aware traversal

### Phase 3: Mermaid Renderer (2 days)

1. Implement `MermaidGraphRenderer`
   - Block rendering with shapes
   - Edge rendering with labels
   - Buffer node rendering
   - Epoch node rendering
   - ID sanitization

2. Test with manual graphs
3. Verify output in Mermaid preview

### Phase 4: Graphviz Renderer (1-2 days)

1. Implement `GraphvizRenderer`
   - DOT format generation
   - Node styling
   - Edge styling
   - Escape handling

2. Test with manual graphs
3. Verify output with Graphviz tools

### Phase 5: Convenience Extensions (0.5 days)

1. Implement `DataFlowGraphExtensions`
   - `ToMermaidDiagram()`
   - `ToGraphviz()`
   - `ToTextSummary()`

### Phase 6: Testing (2-3 days)

1. Add Verify.Xunit package
2. Create test helper methods
3. Implement all test scenarios
4. Generate and review snapshots
5. Approve snapshots
6. Verify snapshot files committed

### Phase 7: Documentation (0.5 days)

1. Add XML documentation to all public APIs
2. Update POC README with export examples
3. Add usage examples

## Acceptance Criteria

- [ ] All public methods have XML documentation
- [ ] Mermaid renderer generates valid Mermaid syntax
- [ ] Graphviz renderer generates valid DOT syntax
- [ ] All POC graph patterns render correctly (simple, broadcast, buffer, routing, complex)
- [ ] Snapshot tests pass for all scenarios
- [ ] Both Mermaid and Graphviz tested
- [ ] Rendering options work correctly
- [ ] Code builds without warnings
- [ ] Renderers are cohesive (< 250 lines each)
- [ ] Architecture is extensible (new format = implement interface)

## Reference Materials

### Research Documentation
- **Architecture**: `/research/graph-export-visualization/design/renderer-architecture.md`
- **POC Comparison**: `/research/graph-export-visualization/notes/graph-structure-comparison.md`
- **Progress Summary**: `/research/graph-export-visualization/notes/progress-summary.md`

### Prototype Code
- **Location**: `/research/graph-export-visualization/handover/prototype/`
- **Usage**: Reference for implementation, not copy-paste

### Production Examples
- **Mermaid Renderer**: `/src/DataFlow/Builder/Graph/DataFlowGraphExporter.cs`
- **Routing Renderer**: `/src/DataFlow/Builder/Graph/RoutingBlockMermaidRenderer.cs`
- **Tests**: `/src/Tests/DataFlow/DataFlowGraphExporterTests.cs`
- **Options**: `/src/DataFlow/Builder/Graph/DiagramRenderOptions.cs`

## Known Limitations

1. **Epoch Policy Metadata**: Policy name, item count, time windows not currently stored on nodes (future enhancement)
2. **Route Collapse**: POC doesn't currently collapse many routes into summary (future enhancement)
3. **Subgraph Rendering**: POC routing simpler than production (no nested subgraphs)

## Future Enhancements

1. **Additional Formats**: PlantUML, D2, ASCII art
2. **Interactive Diagrams**: HTML/SVG with tooltips
3. **Epoch Policy Visualization**: Store and display policy metadata
4. **Route Collapse**: `MaxRoutesToShow` option for graphs with many routes
5. **Block Metadata**: Visualize custom metadata on blocks
6. **Performance Metrics**: Annotate with buffer sizes, throughput estimates

## Questions?

Refer to research documentation or prototype code. For architecture questions, see renderer-architecture.md design decisions section.
