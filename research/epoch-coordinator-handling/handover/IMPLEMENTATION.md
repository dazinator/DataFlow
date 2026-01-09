# Implementation: Better Handling of EpochCoordinator

**Research Reference**: Research work in `/research/epoch-coordinator-handling/`  
**Issue Type**: Implementation  
**Estimated Effort**: Medium (3-5 days)  
**Updated**: 2026-01-08 (Based on PR feedback - Execution Context Approach)

---

## ⚠️ FINAL RECOMMENDATION: Execution Context Approach

**Based on PR feedback discussion (2026-01-08)**:

The **best approach** uses execution context to pass coordinator to actors:

1. **Keyed Services**: Register per-graph coordinator (consistent with DataFlow patterns)
2. **Execution Context**: Add `EpochCoordinator` property to `IActorExecutionContext`
3. **Block Resolution**: `EpochSourceBlock` resolves coordinator via keyed service
4. **Actor Access**: Actors get coordinator from `context.EpochCoordinator`
5. **Deferred Init**: Coordinator doesn't need DI at construction time

**See**: `/research/epoch-coordinator-handling/notes/04-execution-context-approach.md` for complete implementation details.

### Key Changes

| Component | Change |
|-----------|--------|
| `IActorExecutionContext` | Add `IEpochCoordinator? EpochCoordinator { get; }` property |
| `ActorExecutionContext` | Store coordinator, provide via property |
| `EpochSourceBlock` | Resolve coordinator via `GetRequiredKeyedService<IEpochCoordinator>(graphId)` |
| `SourceActorBase` | Constructor no longer needs coordinator parameter |
| Helper methods | Take `IActorExecutionContext context` parameter, get coordinator from it |

### Benefits

- ✅ Keyed services (consistent with DataFlow patterns)
- ✅ No new containers (respects DI architecture)
- ✅ Simplified actor constructors
- ✅ Natural coordinator flow via context
- ✅ Per-graph isolation
- ✅ Backward compatible

---

## ⚠️ Previous PR Feedback - Important Context

**Key insights from earlier PR review**:

1. **Deferred Initialization Pattern**: Consider changing `EpochCoordinator` so it doesn't require `IServiceScopeFactory` in constructor. Instead, provide it during `Build()`. This:
   - Simplifies construction (no service provider needed upfront)
   - Makes it more flexible (container provided nearer to execution)
   - Separates construction phase from execution phase

2. **Keyed Services Preferred**: Do NOT create separate service collections/containers. Instead:
   - Use keyed services pattern (consistent with existing `DataFlowBuilder`)
   - Actors should leverage application dependencies via application container
   - Creating separate containers undermines DI and breaks dependency chains

See `/research/epoch-coordinator-handling/notes/02-di-scoping-analysis.md` for detailed analysis.

---

## Objective

Implement the graph-owned coordinator pattern to remove API friction and provide per-graph coordinator isolation while maintaining multi-source epoch coordination.

## Background

### Problem

The current implementation has an architectural disconnect:
1. `EpochSourceNode` accepts `IEpochCoordinator` but never uses it (dead code)
2. `SourceActorBase` needs coordinator but gets it from DI (different instance)
3. Tests must manually create and manage coordinators
4. No guarantee of per-graph isolation

### Research Findings

See `/research/epoch-coordinator-handling/README.md` for complete research findings.

**Key Discovery**: `EpochSourceNode.Coordinator` property is never used anywhere in the codebase.

**Validated Solution**: Make `DataFlowGraph` own the coordinator.

---

## Approach (Validated by Research)

### Core Changes

1. **Remove coordinator from EpochSourceNode**
   - Remove `_coordinator` field
   - Remove `coordinator` constructor parameter
   - Remove `Coordinator` property

2. **Add coordinator to DataFlowGraph**
   - Add `_epochCoordinator` field
   - Add `EpochCoordinator` property
   - Add `SetEpochCoordinator()` method

3. **Update DataFlowGraphBuilder**
   - Add `_epochCoordinator` field
   - Add `SetEpochCoordinator()` method
   - Update `Build()` to pass coordinator to graph

4. **Update ConfigureEpochs**
   - Store coordinator in builder (not node)
   - Create `EpochSourceNode` without coordinator parameter

### Files to Modify

| File | Changes |
|------|---------|
| `poc/DataFlow/Core/EpochSourceNode.cs` | Remove coordinator storage |
| `poc/DataFlow/Core/DataFlowGraph.cs` | Add coordinator ownership |
| `poc/DataFlow/Builder/DataFlowGraphBuilder.cs` | Add coordinator management |
| `poc/DataFlow/Builder/EpochConfigurationExtensions.cs` | Update to use builder |

### Tests to Update

12 test locations need updating (remove coordinator parameter from `EpochSourceNode` constructor):

1. `poc/DataFlow.Tests/Checkpointing/CheckpointIntegrationTests.cs` - 4 locations
2. `poc/DataFlow.Tests/EpochNodeTests.cs` - 8 locations

**Fix Pattern**:
```csharp
// Before
var node = new EpochSourceNode(coordinator);

// After  
var node = new EpochSourceNode();
```

---

## Success Criteria

### Functional Requirements

- [x] EpochSourceNode no longer accepts coordinator parameter
- [x] DataFlowGraph exposes coordinator via property
- [x] ConfigureEpochs stores coordinator in graph
- [ ] All existing tests pass with updates
- [ ] Multi-source coordination still works
- [ ] Multiple graphs have independent coordinators

### Quality Requirements

- [ ] No breaking changes (factory parameter still works)
- [ ] Code compiles with zero errors
- [ ] All tests pass
- [ ] Performance unchanged (benchmark validation)

### Documentation Requirements

- [ ] XML documentation updated
- [ ] Migration guide created
- [ ] ADR created for coordinator lifetime decision

---

## Implementation Checklist

### Phase 1: Core Changes

- [ ] Update `EpochSourceNode.cs` (remove coordinator)
- [ ] Update `DataFlowGraph.cs` (add coordinator)
- [ ] Update `DataFlowGraphBuilder.cs` (manage coordinator)
- [ ] Update `EpochConfigurationExtensions.cs` (use builder)
- [ ] Verify library compiles

### Phase 2: Test Updates

- [ ] Fix `CheckpointIntegrationTests.cs` (4 locations)
- [ ] Fix `EpochNodeTests.cs` (8 locations)
- [ ] Update `EpochAnchoringDemo.Tests` benchmarks
- [ ] Verify all tests pass

### Phase 3: Validation

- [ ] Run multi-source coordination tests
- [ ] Run multi-graph isolation tests
- [ ] Run performance benchmarks
- [ ] Validate backward compatibility

### Phase 4: Documentation

- [ ] Update API documentation
- [ ] Create ADR for coordinator lifetime management
- [ ] Create migration guide
- [ ] Update getting started guide

---

## Test Scenarios

### Critical Test Scenarios from Research

1. **Node Creation Without Coordinator**
   ```csharp
   var node = new EpochSourceNode();
   Assert.NotNull(node);
   ```

2. **Graph Exposes Coordinator**
   ```csharp
   builder.ConfigureEpochs(config => { /* ... */ });
   var graph = builder.Build();
   Assert.NotNull(graph.EpochCoordinator);
   ```

3. **Multiple Graphs Have Independent Coordinators**
   ```csharp
   var graph1 = builder1.ConfigureEpochs(...).Build();
   var graph2 = builder2.ConfigureEpochs(...).Build();
   Assert.NotSame(graph1.EpochCoordinator, graph2.EpochCoordinator);
   ```

4. **Backward Compatibility**
   ```csharp
   builder.ConfigureEpochs(
       config => { /* ... */ },
       _ => customCoordinator  // Still works
   );
   ```

---

## Performance Requirements

**Baseline**: Current implementation performance  
**Target**: No performance degradation  
**Validation**: Run existing benchmarks

The changes are purely architectural with no performance impact expected.

---

## Migration Path

### For Existing Code

**No changes required** - Factory parameter still supported:
```csharp
builder.ConfigureEpochs(
    config => { /* ... */ },
    _ => customCoordinator  // ← Still works
);
```

### For New Code

**Simpler API** available:
```csharp
builder.ConfigureEpochs(config => { /* ... */ });  // ← Factory optional
```

### For Tests

Tests can stop creating coordinators manually. Access via `graph.EpochCoordinator` if needed.

---

## Design References

- **Research Documentation**: `/research/epoch-coordinator-handling/README.md`
- **Prototype Code**: `/research/epoch-coordinator-handling/handover/prototype/`
- **Design Document**: `/research/epoch-coordinator-handling/design/graph-owned-coordinator.md`
- **Code Analysis**: `/research/epoch-coordinator-handling/notes/01-code-analysis.md`
- **DI Scoping Analysis**: `/research/epoch-coordinator-handling/notes/02-di-scoping-analysis.md`

---

## Implementation Notes

### Prototype Validation

A working prototype exists in `/research/epoch-coordinator-handling/handover/prototype/` that:
- ✅ Compiles successfully  
- ✅ Demonstrates per-graph isolation
- ✅ Shows backward compatibility
- ✅ Includes validation tests

### Key Insights

1. **Dead Code Removal**: `EpochSourceNode.Coordinator` property had zero uses
2. **DI Lifetime Mismatch**: Standard DI lifetimes don't match per-graph requirements
3. **Graph Ownership**: Natural fit for coordinator lifecycle management
4. **Backward Compatible**: Factory parameter can coexist with new approach

### Open Questions

**Actor DI Scoping** (Future Work - Updated based on PR feedback):

**Question**: How do actors resolve the graph's coordinator from DI?

**Options Evaluated**:
- ~~**Option A**: Augmented service provider~~ ❌ Creates separate container (not acceptable per PR feedback)
- **Option B**: Actors resolve from graph ⚠️ Couples actors to graph interface
- **Option C**: Use keyed services ✅ **RECOMMENDED** - Consistent with existing `DataFlowBuilder` patterns

**PR Feedback Recommendation**: 
- Use keyed services (already used in `DataFlowBuilder` for block isolation)
- Register coordinator with graph-specific key
- Actors resolve using keyed service lookup
- Maintains application DI chain and dependencies

**Additional Consideration from PR Feedback**:
- Deferred initialization pattern for `EpochCoordinator`
- Don't require `IServiceScopeFactory` in constructor
- Initialize coordinator during `Build()` when scope factory is available
- This simplifies construction and provides more flexibility

**Decision Needed**: Implementation team should prototype keyed services approach with deferred initialization.

---

## Risks and Mitigation

| Risk | Mitigation |
|------|------------|
| Tests break unexpectedly | Prototype already validated core changes |
| Performance regression | Benchmark before/after |
| Breaking existing code | Factory parameter preserved |
| Actor DI resolution | Document as known limitation for now |

---

## Acceptance Criteria

- [ ] All tests pass
- [ ] No performance regression
- [ ] Backward compatibility maintained
- [ ] Documentation updated
- [ ] Code review approved
- [ ] Research findings validated in production code

---

## Related Issues

- Research Issue: [Research] Better handling of EpochCoordinator
- Related Research: `/research/epoch-di-improvement/` (different focus, complementary)
