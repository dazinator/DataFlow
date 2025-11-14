# Research Plan: Alternative Epoch DI Scope Management

**Created**: 2025-11-14  
**Research Duty**: Evaluate alternative approach for epoch DI scope management  
**Related Issue**: uniun-technology/lib-dataflow#415  
**Related Design**: `/docs/design/epoch-scoped-services/`

---

## Research Question

**Primary Question**: 
> Can the DI scope for epoch-scoped services be kept on `IEpochStream` and propagated to output EpochStreams as vectors change, rather than being lazily resolved via `EpochManager`?

**Sub-Questions**:
1. Is this approach architecturally feasible?
2. Does it simplify any aspects of the current design?
3. What are the trade-offs compared to the current approach?
4. Does it introduce any new challenges or complexities?
5. How does it affect fan-in scenarios and epoch vector subsume operations?

---

## Background

### Context from Issue

The existing design (documented in `/docs/design/epoch-scoped-services/`) proposes:
- `IEpochManager` creates and manages `IEpoch` objects
- Each `IEpoch` has its own DI scope
- Blocks access epochs via `IBlockContext.CurrentEpoch` (lazy resolution)
- EpochManager handles lifecycle, reference counting, and subsume operations

The alternative being explored:
- DI scope is a property on `IEpochStream` itself
- Scope created eagerly when epoch stream is created
- Scope propagated to output EpochStreams as the stream flows through blocks
- More tightly coupled to the vector at point of origin

### Related Work

- **Phase 5 EF Core Anchoring Demo**: Demonstrates manual epoch tracking
- **Epoch-Scoped Services Design**: Current proposal being reconsidered
- **IEpochStream Interface**: Currently has `Epoch` (vector) and `Items` properties

---

## Research Objectives

### Primary Objectives

1. **Document Current Approach**
   - How EpochManager works
   - Lifecycle management via reference counting
   - Block integration via IBlockContext
   - Subsume operation handling

2. **Document Alternative Approach**
   - How DI scope on IEpochStream would work
   - Propagation pattern through blocks
   - Lifecycle tied to stream consumption
   - Subsume operation handling

3. **Comparative Analysis**
   - Architecture implications
   - Simplicity and complexity trade-offs
   - Performance characteristics
   - Developer experience

4. **Feasibility Validation**
   - Prototype key scenarios
   - Test with fan-in operations
   - Verify subsume semantics
   - Check concurrent access patterns

### Secondary Objectives

1. Identify any hybrid approaches
2. Document edge cases and challenges
3. Create recommendation with justification

---

## Research Phases

### Phase 1: Documentation (1-2 days)

**Deliverables**:
- Document current EpochManager approach in detail
- Document alternative IEpochStream approach in detail
- Create comparison matrix

**Key Areas to Document**:
- Epoch lifecycle (creation, usage, disposal)
- DI scope management
- Reference counting vs stream lifetime
- Block integration patterns
- Fan-in and subsume semantics
- Concurrent access patterns

### Phase 2: Analysis (1-2 days)

**Deliverables**:
- Architectural implications analysis
- Trade-off analysis across key dimensions
- Edge case identification
- Performance considerations

**Key Dimensions**:
- Complexity (library vs block developer)
- Coupling (tight vs loose)
- Lifecycle clarity
- Error handling
- Testability

### Phase 3: Prototyping (2-3 days)

**Deliverables**:
- Prototype implementation of alternative approach
- Test key scenarios (especially fan-in)
- Validate subsume operation handling
- Compare with current approach

**Key Scenarios to Test**:
1. Simple linear pipeline
2. Fan-in with multiple epoch streams
3. Epoch vector subsume operations
4. Concurrent block access to same epoch
5. Block failure and cleanup
6. Multiple service resolutions

### Phase 4: Findings and Recommendation (1 day)

**Deliverables**:
- Comprehensive findings document
- Recommendation with justification
- If alternative is viable: updated design document
- Implementation handover work item

---

## Success Criteria

**Research Complete When**:
- [ ] Both approaches fully documented
- [ ] Comparative analysis complete
- [ ] Feasibility validated via prototype
- [ ] Clear recommendation provided with evidence
- [ ] Edge cases and challenges documented
- [ ] Implementation path identified (if proceeding with alternative)

**High-Quality Output Indicators**:
- Analysis considers all aspects from existing design
- Prototype demonstrates key scenarios
- Recommendation backed by concrete evidence
- Trade-offs clearly articulated
- Decision can be made with confidence

---

## Known Constraints

1. **Must maintain existing semantics**:
   - Epoch vector operations (increment, subsume)
   - Fan-in behavior (element-wise max merging)
   - Concurrent access patterns
   - Global epoch alignment

2. **Must support future requirements**:
   - Epoch-scoped EF Core transactions (#125)
   - Service participation in epoch completion events
   - Performance competitive with manual approach

3. **Must integrate with existing patterns**:
   - `IEpochLifecycleParticipant`
   - `IEpochLifecycleObserver`
   - Current block interfaces

---

## Research Artifacts

**During Research**:
- `/research/epoch-scope-propagation/notes/` - Working notes and exploration
- `/research/epoch-scope-propagation/design/` - Approach documentation
- `/research/epoch-scope-propagation/handover/prototype/` - Prototype code

**Final Deliverables**:
- `/research/epoch-scope-propagation/README.md` - Research findings
- Updated design docs (if alternative is adopted)
- Implementation handover work item
- Self-improvement feedback

---

## Next Steps

1. ✅ Research plan created
2. ⏭️ Begin Phase 1: Document current approach
3. ⏭️ Begin Phase 1: Document alternative approach
4. ⏭️ Create comparison framework
