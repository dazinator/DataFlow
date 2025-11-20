# Research Plan: Graph Export Visualization

## Research Objective

Implement an extensible graph export functionality for the POC codebase that can export dataflow graphs to text-based diagram formats (Mermaid, Graphviz/DOT), maintaining feature parity with the production codebase while being idiomatic to the POC's architecture.

## Research Questions

1. **Architecture Design**
   - What sort of architecture do we want for supporting multiple text-based diagram formats?
   - Can we create a renderer abstraction that's cohesive and doesn't sprawl across the codebase?
   - Is the visitor pattern worth it for graph traversal/rendering?

2. **Graph Traversal**
   - Do we need to introduce a dedicated graph traversal mechanism?
   - How should the POC graph structure be traversed for logical diagram rendering?
   - Can we reuse patterns from the production codebase's `DataFlowGraphIterator`?

3. **Rendering Abstraction**
   - Do we need abstraction from string builder/writer to allow renderers to emit logical structure with nesting?
   - How should renderers control diagram structure (indentation, subgraphs, etc.)?
   - What's the right balance between simplicity and extensibility?

4. **POC vs Production Differences**
   - How does the POC's `DataFlowGraph` structure differ from production's `DataFlowGraph`?
   - What are the key differences in block types and connections?
   - Can we adapt the production patterns or do we need a fresh approach?

5. **Testing Strategy**
   - How should snapshot testing be integrated with Verify.Xunit?
   - What test scenarios need coverage?
   - Should we mirror production test patterns?

## Success Metrics

### Quantitative
- Export to at least 2 formats: Mermaid and Graphviz/DOT
- Snapshot tests cover at least 5 common graph patterns
- Code is maintainable: renderer implementations < 200 lines each

### Qualitative
- Clean separation between rendering concern and graph structure
- Easy to add new export formats without modifying existing code
- Idiomatic to POC architecture patterns
- Developer-friendly API matching production experience where applicable

### Baseline
- Production codebase has Mermaid export with ~450 lines (DataFlowGraphExporter.cs)
- Production has snapshot testing with Verify.Xunit
- Production supports multiple block types, routing, branches

### Validation
- Prototype demonstrates exporters for both formats
- Snapshot tests validate diagram output matches expectations
- Can render all POC graph structures: basic flows, broadcast, buffer nodes
- Architecture easily extensible for new formats

## Validation Approach

1. **Code Review**: Analyze production `DataFlowGraphExporter` implementation
2. **Prototyping**: Build working exporters for Mermaid and Graphviz
3. **Testing**: Create comprehensive snapshot tests
4. **Architecture Validation**: Add a third mini-format to prove extensibility
5. **Documentation**: Capture design decisions and implementation guidance

## Expected Outcomes

1. **Research Documentation**: Complete analysis in `/research/graph-export-visualization/`
2. **Implementation Issue**: Detailed handover with specifications
3. **Prototype Code**: Working reference implementation in `/research/.../handover/prototype/`
4. **Design Document**: Architecture decisions and patterns
5. **Test Scenarios**: Documented test cases for implementation

## Timeline

- Days 1-2: Analysis and architecture design
- Days 3-4: Prototype Mermaid exporter
- Day 5: Prototype Graphviz exporter
- Day 6: Snapshot testing and validation
- Day 7: Documentation and handover preparation

## Key Deliverables

- [ ] Analysis of production vs POC graph structure differences
- [ ] Renderer architecture design document
- [ ] Working Mermaid exporter prototype
- [ ] Working Graphviz/DOT exporter prototype
- [ ] Snapshot test suite with Verify.Xunit
- [ ] Implementation handover issue with complete specifications
- [ ] Saved prototype code in handover folder
- [ ] Research findings documented in README.md
