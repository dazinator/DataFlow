# Research Plan: Dynamic Flows

**Research Issue**: [Research] Dynamic Flows  
**Research Date**: 2026-03-30  
**Researcher**: @copilot

---

## Research Objective

Investigate whether the DataFlow library can support **metadata-driven, dynamically-defined dataflow pipelines** — i.e. flows whose topology (blocks and connections) is defined at runtime from a serializable definition rather than being hardcoded at compile time.

This opens the door to a flow *designer* UI where users can compose pipelines by connecting registered blocks visually.

---

## Research Questions

1. **Feasibility**: Do we have enough metadata in the existing `IBlockTypeRegistry` to validate and execute a dynamically-defined flow?

2. **Definition Format**: What should a flow *definition* look like?  JSON? YAML? Something else?

3. **Dynamic Execution**: How would we build and execute a `DataFlowGraph` from such a definition?

4. **Error Handling**: What happens when a flow definition is invalid (type mismatches, missing blocks, cycles)?  Does the existing Flow Run List UI already show an error state for a failed flow?

5. **Persistence**: How can an application persist flow definitions?  Should the library ship a repository abstraction with a default EF Core implementation?

6. **Demo**: Can we validate the approach with a minimal prototype that runs a dynamic flow?

7. **Designer UI**: What would a sane flow-designer UI look like in Blazor?

8. **Block Configuration**: How can blocks become configurable via the UI?  Can JSON Forms / JSON Schema provide a generic approach?

9. **Configuration Versioning**: How do we snapshot configuration alongside a flow definition version so that historical runs use the config from that version?

---

## Success Metrics

- **Qualitative**: Research comprehensively answers all questions above
- **Quantitative**: Prototype demonstrates dynamic flow execution end-to-end
- **Validation**: Prototype runs without modification to production code (uses existing APIs)

---

## Validation Approach

1. Examine `IBlockTypeRegistry` and `DataFlowGraphBuilder` to determine what's already available
2. Design a minimal JSON definition schema
3. Write a prototype `DynamicFlowBuilder` that parses a JSON definition and builds a `DataFlowGraph`
4. Run the prototype against the existing demo blocks
5. Document findings, gaps, and implementation guidance

---

## Expected Outcomes

- Research documentation in `/research/dynamic-flows/`
- Design documents in `/research/dynamic-flows/design/`
- Prototype code in `/research/dynamic-flows/handover/prototype/`
- Implementation handover GitHub issue
- ADR for dynamic flow definition format

---

## Timeline

Estimated: 1–2 days research + prototyping
