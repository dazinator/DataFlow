# Prototype Code Reference

This folder contains the working prototype code developed during research. This code was used to validate the architecture and approach but has been reverted from the main codebase per the research workflow.

## Purpose

The prototype demonstrates:
- Extensible renderer architecture with `IGraphRenderer` interface
- Mermaid and Graphviz/DOT format export
- Graph query extensions for traversal
- Snapshot testing with Verify.Xunit
- POC-specific feature support (epochs, buffers, routing)

## Structure

```
prototype/
├── Visualization/              # Renderer implementation
│   ├── IGraphRenderer.cs       # Renderer interface
│   ├── GraphRenderOptions.cs   # Rendering configuration
│   ├── MermaidGraphRenderer.cs # Mermaid flowchart renderer
│   ├── GraphvizRenderer.cs     # Graphviz DOT renderer
│   ├── GraphQueryExtensions.cs # Graph traversal utilities
│   └── DataFlowGraphExtensions.cs # Convenience methods
└── Tests/
    └── GraphVisualizationTests.cs # Snapshot tests
```

## Usage Reference

### Basic Usage
```csharp
var graph = builder.Build();

// Mermaid export
var mermaid = graph.ToMermaidDiagram("LR");

// Graphviz export
var dot = graph.ToGraphviz();
```

### With Options
```csharp
var options = new GraphRenderOptions
{
    Direction = "TB",
    IncludeTypeInfo = true,
    ShowBufferNodes = true,
    ShowEpochNodes = true
};

var mermaid = graph.ToMermaidDiagram(options: options);
```

### Using Renderers Directly
```csharp
var renderer = new MermaidGraphRenderer();
var diagram = renderer.Render(graph, options);
```

## Implementation Notes

### DataFlowGraph Changes Required
The prototype added these public members to `DataFlowGraph`:
```csharp
// Public properties for epoch nodes
public EpochSourceNode? EpochSource { get; }
public IReadOnlyList<EpochProcessorNode> EpochProcessors { get; }

// Public methods for graph queries
public IEnumerable<IBlock> GetBufferProducers(BufferNode buffer)
public IEnumerable<IBlock> GetBufferConsumers(BufferNode buffer)
public IEnumerable<Edge> GetOutgoingEdges(IBlock block)
public IEnumerable<Edge> GetIncomingEdges(IBlock block)
```

### Test Dependencies
Tests require:
- `Verify.Xunit` package (v26.6.0 or later)
- `xunit` package (v2.9.0 or later for compatibility)

## Key Design Decisions

1. **Strategy Pattern**: Different renderers implement `IGraphRenderer`
2. **Hybrid Query API**: O(1) public methods on DataFlowGraph, extension methods for high-level traversal
3. **POC-Specific Features**: Handles epochs, buffers, and edge-level routing
4. **Snapshot Testing**: Uses Verify.Xunit for diagram validation

## See Also

- `/research/graph-export-visualization/design/renderer-architecture.md` - Architecture design
- `/research/graph-export-visualization/notes/graph-structure-comparison.md` - POC vs production differences
- `/research/graph-export-visualization/handover/github-issue-implementation.md` - Implementation specifications
