# Research Plan: Better Handling of EpochCoordinator

## Research Objective

Investigate and improve the handling of `IEpochCoordinator` to reduce API friction while maintaining proper epoch coordination between multiple sources on the same graph.

## Problem Statement

### Current Issues

1. **Unused coordinator in EpochSourceNode**: The `EpochSourceNode` constructor accepts an `IEpochCoordinator` but never uses it (line 62 exposes it via property, but the node itself doesn't use it internally).

2. **API Friction**: The `ConfigureEpochs` API requires a factory function that feels awkward:
   ```csharp
   builder.ConfigureEpochs(config =>
   {
       config.SetPolicy(EpochPolicy.ByCount(100));
       config.AddProcessor("processor1");
   }, _ => _coordinator);  // ❌ Awkward - parameter not used, but required
   ```

3. **Coordinator Lifetime Ambiguity**: Unclear whether coordinator should be:
   - Singleton (shared across graphs) ❌ - causes cross-graph interference
   - Per-graph instance ✅ - but how to manage?
   - Transient ❌ - breaks multi-source coordination

4. **DI Scope Conflict**: `SourceActorBase` injects `IEpochCoordinator`, requiring:
   - Multiple actors on same graph share same coordinator instance
   - But different graphs should have different coordinators
   - Transient registration breaks coordination
   - Scoped registration requires managing scopes carefully

## Research Questions

### Primary Questions

1. **Does EpochSourceNode need the coordinator reference?**
   - What was the original intent?
   - Is it used elsewhere via the Coordinator property?
   - Can we remove it safely?

2. **How should coordinator lifetime be managed?**
   - Should it be per-graph instance?
   - How do we ensure graph isolation?
   - How do we ensure actors in same graph share the coordinator?

3. **Can we improve the API while maintaining coordination?**
   - Remove unused parameters?
   - Simplify factory function?
   - Make coordinator management transparent?

### Secondary Questions

4. **How does DI scoping work with actors?**
   - Are actor instances created in a shared DI scope per graph?
   - Can we leverage this for coordinator sharing?

5. **What prevents cross-graph interference?**
   - If coordinators are singleton, can one graph's coordinator affect another?
   - How do source IDs prevent collisions?

6. **What is the relationship between graph builder and coordinator?**
   - Should the builder create and own the coordinator?
   - Should the coordinator be injected from DI?
   - Should the builder manage the coordinator lifecycle?

## Success Metrics

### Quantitative
- **API Simplicity**: Reduce ConfigureEpochs parameters from 2 to 1 (remove factory when possible)
- **Code Clarity**: Remove unused coordinator parameter from EpochSourceNode if not needed
- **Test Complexity**: Simplify test setup (no manual coordinator creation in tests)

### Qualitative
- **Coordination Preserved**: Multi-source epochs still work correctly
- **Graph Isolation**: Different graphs don't interfere with each other
- **API Ergonomics**: Developers don't need to understand coordinator internals
- **Backward Compatibility**: Existing code continues to work

### Baseline vs. Target

**Baseline**:
- EpochSourceNode stores coordinator but doesn't use it
- Tests manually create coordinator: `_ => _coordinator`
- Factory function parameter is required but often ignored
- Unclear coordinator lifetime strategy

**Target**:
- Clean, obvious coordinator lifecycle management
- Minimal API surface (factory optional when not needed)
- Graph isolation guaranteed by design
- Clear documentation of coordinator lifetime

## Validation Approach

### Phase 1: Code Analysis
1. Trace all uses of `EpochSourceNode.Coordinator` property
2. Trace all uses of `IEpochCoordinator` in actor classes
3. Map DI scope creation and actor instantiation
4. Document current coordination flow

### Phase 2: Prototyping
1. **Prototype A**: Remove coordinator from EpochSourceNode, pass directly to actors
2. **Prototype B**: Builder creates and owns coordinator per graph
3. **Prototype C**: Use keyed services in DI for per-graph coordinators
4. Compare approaches on:
   - API ergonomics
   - Coordination correctness
   - Graph isolation
   - Complexity

### Phase 3: Validation Tests
Create tests demonstrating:
1. Multi-source coordination within a graph
2. Independent coordination across different graphs
3. Simplified API usage (no factory when using DI)
4. Backward compatibility with factory approach

## Expected Outcomes

### Research Documentation
- `/research/epoch-coordinator-handling/README.md` - Research findings
- `/research/epoch-coordinator-handling/notes/` - Analysis notes
- `/research/epoch-coordinator-handling/design/` - Approach comparisons

### Prototype Code
- Working demonstrations of different approaches
- Saved in `/research/epoch-coordinator-handling/handover/prototype/`

### Implementation Specification
- Clear design for production implementation
- Migration path for existing code
- Test scenarios documented

### Formal Documentation
- ADR for coordinator lifetime management decision
- Updated API documentation

## Timeline

- **Days 1-2**: Code analysis and problem understanding
- **Days 3-5**: Prototyping different approaches
- **Days 6-7**: Validation testing and documentation
- **Day 8**: Create implementation handover

**Total**: 8 days estimated

## Success Criteria

- [ ] All research questions answered with evidence
- [ ] Coordinator lifecycle clearly understood and documented
- [ ] API friction identified and solutions prototyped
- [ ] Graph isolation validated
- [ ] Multi-source coordination preserved
- [ ] Implementation specification created
- [ ] Prototype code saved for reference
- [ ] Self-improvement feedback submitted
