# Research Plan: Design Revision for DI Service Registration

**Date**: 2025-11-19  
**Research Objective**: Revise the DI service registration design from issue #473 to address identified concerns and provide clearer guidance

---

## Research Context

The previous research in issue #473 produced a design for canonical DI service registration that was successfully validated through prototyping. However, when attempting to implement this design, several architectural concerns were identified that need to be addressed:

**Previous Design Deliverables**:
- Research documentation in `/research/di-service-registration/`
- Prototype code in `/research/di-service-registration/handover/prototype/`
- ADR in `/poc/docs/adr/2025-11-17-di-service-registration.md`

**Related Issues**:
- Issue #473: Original research that created `DataFlowGraphBuilderEx` and `AddDataFlows()` API
- Issue #475: Implementation issue created from #473 research (not yet started)

---

## Research Questions

### Problem 1: Parallel Builder Structures (Confusing Architecture)

**Issue**: The design introduced `DataFlowGraphBuilderEx` while keeping the existing `DataFlowGraphBuilder`, creating two alternative paradigms for the same use cases.

**Research Question**: How can we consolidate registration and graph building into canonical APIs without parallel/alternative structures?

**Success Criteria**:
- Single, clear builder pattern for graph construction
- No duplicate or competing APIs for the same functionality
- Clear migration path from old to new API (or enhanced existing API)

### Problem 2: Default Lifetime Scope (Safety Concern)

**Issue**: The original design registered blocks and flows as singletons, which may not be safe when executing multiple instances of a flow in separate scopes.

**Research Question**: Should the default registration lifetime be scoped instead of singleton? What are the implications?

**Success Criteria**:
- Safe default behavior for multi-instance flow execution
- Clear guidance on when to use different lifetimes
- Flexibility to override lifetime when needed

### Problem 3: Registration Idempotence (Duplicate Registrations)

**Issue**: The design didn't specify use of `TryAdd` variants, allowing duplicate registrations when `AddDataFlows()` is called multiple times.

**Research Question**: How should we handle duplicate registrations to ensure idempotency?

**Success Criteria**:
- Calling `AddDataFlows()` multiple times doesn't create duplicate core service registrations
- Calling `AddBlock()` multiple times with the same name has defined behavior
- Clear error messages or overwrite behavior

### Problem 4: Graph Builder Integration (Disjointed API)

**Issue**: `DataFlowGraphBuilderEx` usage is disjointed from `AddDataFlows()`, detracting from the clarity of a single registration entry point.

**Current Pattern** (Disjointed):
```csharp
services.AddDataFlows(df => 
{
    df.AddBlock("producer", sp => new ProducerBlock<int>(...));
    df.AddBlock("transformer", sp => new ActorBlock<int, string, MyActor>(...));
});

// Separate, disconnected graph building
var builder = new DataFlowGraphBuilderEx("flow", serviceProvider);
builder.UseBlock("producer")
    .UseBlock("transformer")
    .Connect("producer", "transformer");
```

**Desired Pattern** (Integrated):
```csharp
services.AddDataFlows(df => 
{
    df.AddGraph("graph-a", BuildGraph);
    // Or some other way to integrate graph topology with registration
});
```

**Research Question**: How can we integrate graph building into the `AddDataFlows()` registration flow?

**Success Criteria**:
- Unified registration entry point
- Clear separation between block/strategy registration and graph topology
- Maintains flexibility for dynamic graph building at runtime

---

## Validation Approach

### Phase 1: Analyze Concerns
1. Review original design documentation
2. Analyze prototype code to understand current implementation
3. Document specific issues with each concern
4. Identify trade-offs and constraints

### Phase 2: Explore Solutions
1. **Builder Consolidation**: Research approaches to unify or enhance existing builder
2. **Lifetime Management**: Prototype different lifetime scopes and test safety
3. **Idempotence**: Design TryAdd approach and test duplicate registration scenarios
4. **API Integration**: Explore patterns for integrating graph topology into registration

### Phase 3: Prototype & Validate
1. Create working prototypes for each solution
2. Test edge cases and error scenarios
3. Validate performance implications
4. Ensure backward compatibility where possible

### Phase 4: Document Findings
1. Document recommended approach for each concern
2. Create updated API specification
3. Update or create new ADR
4. Provide clear implementation guidance

---

## Expected Outcomes

### Deliverables
- [ ] Research documentation in `/research/design-revision-2025-11/`
- [ ] Revised API design specification
- [ ] Updated or new ADR documenting decisions
- [ ] Implementation-ready work item with complete specifications
- [ ] Prototype code demonstrating solutions (to be reverted)
- [ ] Clear migration guidance from previous design

### Success Metrics

**Architectural Clarity**:
- Single canonical pattern for DI registration and graph building
- No confusing parallel structures
- Clear mental model for developers

**Safety**:
- Default lifetime behavior is safe for common scenarios
- Clear guidance on lifetime selection

**Usability**:
- Idempotent registration behavior
- Clear error messages
- Integrated API surface

**Compatibility**:
- Viable migration path from current POC code
- Doesn't break existing patterns unnecessarily

---

## Timeline

**Estimated Duration**: 5-7 days

**Breakdown**:
- Day 1: Analyze concerns and review existing design (this document)
- Days 2-3: Explore solutions and create prototypes
- Days 4-5: Validate approaches and refine design
- Days 6-7: Document findings and create implementation handover

---

## Constraints

1. **POC Target**: This is for the POC codebase, not production
2. **Backward Compatibility**: Should consider existing POC usage where reasonable
3. **.NET 8 Features**: Can leverage modern .NET features (keyed services, etc.)
4. **Research Outcome**: Code will be reverted; deliverable is documentation + specs

---

## References

- **Previous Research**: `/research/di-service-registration/`
- **Previous ADR**: `/poc/docs/adr/2025-11-17-di-service-registration.md`
- **Prototype Code**: `/research/di-service-registration/handover/prototype/`
- **Current Builder**: `/poc/DataFlow.POC/Builder/DataFlowGraphBuilder.cs`
- **Research Duty Procedure**: `.team/duties/RESEARCH_DUTY.md`
