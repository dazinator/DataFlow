# Research: Graph Export to Mermaid and Graphviz

**Status**: ✅ Complete  
**Research Issue**: #520  
**Implementation Issue**: [To be created]

## Executive Summary

This research validated an extensible architecture for exporting POC dataflow graphs to text-based diagram formats (Mermaid, Graphviz/DOT). A working prototype demonstrates feasibility, performance, and compatibility with POC-specific features (epochs, buffers, routing).

### Key Findings

✅ **Architecture Validated**: Strategy pattern with `IGraphRenderer` interface enables clean extensibility  
✅ **POC Compatibility**: Handles POC-specific concepts (epochs, buffers, edge-level routing)  
✅ **Performance**: O(1) graph queries using internal dictionaries  
✅ **Maintainability**: Each renderer < 250 lines, cohesive and focused  
✅ **Testing**: Snapshot testing with Verify.Xunit works well

### Recommendation

**Proceed with implementation** using the validated architecture. The approach is production-ready and addresses all requirements.

## Research Questions Answered

### 1. What sort of architecture do we want?

**Answer**: Strategy pattern with renderer interface

**Rationale**:
- Clean separation: rendering logic isolated from graph structure
- Extensible: new formats via interface implementation
- Testable: can mock renderers
- DI-friendly: can inject renderers if needed

```csharp
public interface IGraphRenderer
{
    string Render(DataFlowGraph graph, GraphRenderOptions? options = null);
}
```

### 2. Do we need graph traversal mechanism?

**Answer**: Yes, hybrid approach

**Implementation**:
- **Public methods on DataFlowGraph**: O(1) lookups using internal dictionaries
  - `GetBufferProducers()`, `GetBufferConsumers()`
  - `GetIncomingEdges()`, `GetOutgoingEdges()`
- **Extension methods**: Higher-level traversal logic
  - `GetSourceBlocks()`, `GetTargetBlocks()`
  - `GetTopologicalOrder()`

**Rationale**: Balances efficiency (O(1) queries) with clean API design (extensions for convenience).

### 3. Do we need abstraction from string builder?

**Answer**: No, simple StringBuilder is sufficient

**Rationale**:
- POC graph structure is simpler than production
- No complex nesting requirements (no routing subgraphs)
- StringBuilder provides adequate control
- Can add abstraction later if needed

**Production Comparison**: Production has `IBlockRenderContext` for nested subgraphs (routing blocks). POC doesn't need this complexity yet.

### 4. Can we keep renderer cohesive?

**Answer**: Yes, each renderer < 250 lines

**Validation**:
- MermaidGraphRenderer: ~240 lines
- GraphvizRenderer: ~240 lines
- Clean separation of concerns
- Each renderer focused on single format

### 5. Is visitor pattern worth it?

**Answer**: No, simple iteration is sufficient

**Rationale**:
- POC block types are less diverse than production
- Simple iteration is more readable
- No complex type-specific rendering needed
- Can reconsider if block type diversity increases

## Architecture Design

### Components

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
│  - GetTopologicalOrder()            │
└──────────────┬──────────────────────┘
               │
               │ uses
               ▼
┌─────────────────────────────────────┐
│  IGraphRenderer (Interface)         │
│  + Render(graph, options): string   │
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

### Key Design Decisions

See `/design/renderer-architecture.md` for detailed rationale on:
- Hybrid query API (public methods + extensions)
- Renderer interface vs static methods
- Buffer node representation
- Epoch node representation
- Routing visualization
- Visitor pattern (not used)

## POC-Specific Features

### Epoch Nodes

**Challenge**: POC has epoch processing (transactions/checkpoints) not in production

**Solution**:
- Expose `EpochSource` and `EpochProcessors` as public properties
- Visualize with hexagon shapes
- Dashed edges for epoch flow
- Configurable via `ShowEpochNodes` option

**Limitation**: Epoch policy metadata (name, item count, time windows) not currently stored on nodes. Future enhancement could add this.

### Buffer Nodes

**Challenge**: POC has shared channel concept not in production

**Solution**:
- Expose `GetBufferProducers()` and `GetBufferConsumers()` methods
- Visualize as cylinder nodes
- Show producer → buffer → consumer flows
- Configurable via `ShowBufferNodes` option

### Routing

**Challenge**: POC uses edge-level routing, production uses routing blocks

**POC Approach**: `SelectiveRoutingEdgeStrategy` with selector function  
**Production Approach**: Structured routing blocks with sub-pipelines  

**Visualization**:
- POC: Multiple edges from source to route targets
- Production: Subgraphs showing route structure

**Collapse Feature**: Production has `CollapseConcurrentBranches` to collapse many similar branches. POC could add `MaxRoutesToShow` / `CollapseRoutes` for routes, but not needed currently.

## Validation Results

### Prototype Testing

**Test Coverage**:
- ✅ Simple flows (source → transform → target)
- ✅ Buffer node patterns
- ✅ Broadcast flows (1→N)
- ✅ Routing flows (SelectiveRoutingEdgeStrategy)
- ✅ Complex flows (multi-source + merge + broadcast)
- ✅ Direction variants (LR, TB, RL, BT)
- ✅ Rendering options (hide types, hide buffers, hide epochs)

**Snapshot Testing**: Verify.Xunit works excellently for diagram validation

### Performance

- **Graph Queries**: O(1) using internal dictionaries
- **Rendering**: Linear in graph size (O(V + E))
- **Memory**: Minimal overhead (StringBuilder + temporary collections)

### Code Quality

- **Mermaid Renderer**: 240 lines, cohesive
- **Graphviz Renderer**: 243 lines, cohesive
- **No Warnings**: Builds cleanly
- **Clear Separation**: Rendering isolated from graph structure

## Implementation Guidance

### Critical Path

1. Add query methods to `DataFlowGraph` (enables all renderers)
2. Implement `MermaidGraphRenderer` (primary format)
3. Add snapshot tests (validates output)
4. Implement `GraphvizRenderer` (secondary format)
5. Documentation and examples

### Potential Pitfalls

1. **Type Handling**: POC uses `object` instead of `void` for source/target blocks
2. **Buffer Connections**: Need access to internal dictionaries (add public methods)
3. **Edge Strategies**: Different strategies need consistent visualization
4. **Verify.Xunit Version**: Requires xunit 2.9.0+ for compatibility

### Success Metrics

**Quantitative**:
- ✅ 2 formats implemented (Mermaid + Graphviz)
- ✅ 10+ test scenarios with snapshots
- ✅ Each renderer < 250 lines

**Qualitative**:
- ✅ Clean separation of concerns
- ✅ Easy to extend (implement interface)
- ✅ Idiomatic to POC architecture
- ✅ Developer-friendly API

## Deliverables

### Research Documentation
- ✅ `research-plan.md` - Research objectives and questions
- ✅ `notes/graph-structure-comparison.md` - POC vs production differences
- ✅ `notes/progress-summary.md` - Completion status
- ✅ `design/renderer-architecture.md` - Architecture decisions
- ✅ This README

### Prototype Code
- ✅ `handover/prototype/Visualization/` - Working renderer implementations
- ✅ `handover/prototype/Tests/` - Snapshot test examples
- ✅ `handover/prototype/README.md` - Prototype usage guide

### Implementation Specifications
- ✅ `handover/github-issue-implementation.md` - Complete implementation guide

## Next Steps

1. **Create Implementation Issue**: Use `handover/github-issue-implementation.md` as template
2. **Assign to Implementation Team**: Hand off with complete context
3. **Reference Prototype**: Use as implementation guide (not copy-paste)
4. **Follow Architecture**: Validated design is production-ready

## Lessons Learned

### What Worked Well

1. **Prototype-First Approach**: Validated architecture before committing
2. **Snapshot Testing**: Excellent for diagram validation
3. **Hybrid Query API**: Balances efficiency and convenience
4. **POC Feature Support**: Epochs, buffers, routing handled cleanly

### What Could Be Improved

1. **Epoch Metadata**: Should store policy info on nodes for richer visualization
2. **Route Collapse**: Could add threshold-based route collapsing
3. **Subgraph Support**: Future feature for complex routing patterns

### Recommendations for Implementation Team

1. **Start with DataFlowGraph changes**: Unblocks all renderers
2. **Test incrementally**: Validate each renderer with manual graphs before snapshot tests
3. **Review Mermaid/Graphviz syntax**: Ensure output is valid
4. **Use prototype as reference**: Don't copy-paste, understand and adapt
5. **Add XML docs**: All public APIs need documentation

## Conclusion

The research successfully validated an extensible, maintainable architecture for graph export in the POC codebase. The prototype demonstrates all requirements are achievable with clean, efficient code. The implementation team has complete specifications and working reference code to proceed with confidence.

**Recommendation**: ✅ **Approved for implementation**

## References

- **Research Issue**: #520
- **Production Code**: `/src/DataFlow/Builder/Graph/DataFlowGraphExporter.cs`
- **POC Graph**: `/poc/DataFlow.POC/Core/DataFlowGraph.cs`
- **Verify.Xunit**: https://github.com/VerifyTests/Verify
- **Mermaid Docs**: https://mermaid.js.org/
- **Graphviz Docs**: https://graphviz.org/
