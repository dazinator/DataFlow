# Research Findings: Alternative Epoch DI Scope Management

**Research Question**: Can DI scope for epoch-scoped services be kept on `IEpochStream` and propagated to output streams rather than managed by `EpochManager`?

**Answer**: **No** - The alternative approach is not viable as a general solution.

**Created**: 2025-11-14  
**Status**: Complete  
**Recommendation**: **Retain current EpochManager approach**

---

## Executive Summary

This research evaluated an alternative architecture for epoch DI scope management where the `IServiceScope` would be coupled directly to `IEpochStream` and propagated through the pipeline, rather than being managed by a centralized `IEpochManager` with lazy resolution.

**Key Findings**:

1. ✅ **Alternative works for linear pipelines** - Simpler propagation, less framework coordination
2. ❌ **Alternative fails for fan-in scenarios** - No clean solution for scope merging
3. ❌ **Alternative doesn't support subsume semantics** - Cannot extend scope lifetime to cover subsumed vectors
4. ❌ **Alternative breaks service sharing guarantee** - Different scopes at merge points

**Recommendation**: **Do not revise the design**. Proceed with current EpochManager approach as documented in `/docs/design/epoch-scoped-services/`. The alternative approach is fundamentally incompatible with fan-in requirements.

**Impact on Issue #415**: No changes needed to Phase 1 implementation plan. Continue with EpochManager design as specified.

---

## Research Overview

### Background

The existing design (documented in `/docs/design/epoch-scoped-services/README.md`) proposes:
- `IEpochManager` manages epoch objects with DI scopes
- Blocks access epochs via `IBlockContext.CurrentEpoch` (lazy resolution)
- Reference counting determines scope lifetime
- Explicit subsume notifications extend epoch lifetimes

The question raised was whether a simpler approach could:
- Couple DI scope directly to `IEpochStream`
- Create scope eagerly at source
- Propagate scope through pipeline with streams
- Eliminate centralized manager and reference counting

### Research Methodology

1. **Documented Current Approach**: Analyzed EpochManager design in detail
2. **Documented Alternative Approach**: Designed stream-coupled scope architecture
3. **Comparative Analysis**: Systematically compared across 10 dimensions
4. **Use Case Testing**: Evaluated linear pipelines, fan-in, and concurrent access

---

## Detailed Findings

### Finding 1: Fan-In Scope Merging is Unsolvable

**Problem**: When two epoch streams merge, their scopes must also merge.

**Scenario**:
```
Source A: Creates stream {vector={A=1}, scope=scopeA}
Source B: Creates stream {vector={B=1}, scope=scopeB}
BufferNode: Merges to {vector={A=1,B=1}, scope=???}
```

**Core Issue**: Two different epochs (with different scopes) become one unified epoch. Which scope should the merged stream use?

**Options Evaluated**:

1. **Pick scopeA arbitrarily**
   - ❌ Services resolved from scopeB are lost
   - ❌ Breaks semantic: "same epoch = same services"
   
2. **Pick scopeB arbitrarily**
   - ❌ Services resolved from scopeA are lost
   - ❌ Same semantic breakage
   
3. **Create new scope**
   - ❌ Fresh services, neither scopeA nor scopeB
   - ❌ Breaks continuity - services resolved before merge differ from after
   
4. **Merge service providers**
   - ❌ Not supported by DI containers
   - ❌ Arbitrary resolution order
   - ❌ If same service type in both scopes, only one accessible
   
5. **Reintroduce centralized registry**
   - ❌ Defeats the purpose of the alternative approach
   - ❌ Back to EpochManager pattern

**Conclusion**: No clean solution exists for fan-in with stream-coupled scopes.

**Why This Matters**: The existing design **explicitly requires** fan-in support:
- BufferNode explicitly implements fan-in
- Design includes 6 test scenarios for fan-in cases
- Element-wise max vector merging is fundamental to the epoch model
- Service sharing across merge points is a stated requirement

**Current Approach Solution**: EpochManager handles this via `NotifyEpochSubsumed()`:
```csharp
void NotifyEpochSubsumed(EpochVector from, EpochVector to)
{
    if (_activeEpochs.TryGetValue(from, out var epoch))
    {
        // Extend epoch lifetime to cover subsumed vector
        _activeEpochs.TryAdd(to, epoch); // Reuse one scope
        epoch.AddReference();
    }
}
```

One scope "wins" and covers the merged vector space. Clean, explicit, working.

---

### Finding 2: Subsume Semantics Are Unclear

**Current Approach**: Explicit support for epoch vector subsume operations:
- When vector A is subsumed into vector B, epoch lifetime extends
- `NotifyEpochSubsumed(from, to)` provides clear extension point
- Can implement various merge strategies
- Scope remains valid for both vectors during transition

**Alternative Approach**: No clear way to represent subsume semantics:
- Scope is embedded in stream, tied to specific vector
- How to extend scope lifetime to cover subsumed vector?
- No notification mechanism
- No explicit lifecycle management

**Example**:
```
Stream A: {vector={s1=5}, scope=scopeA}
Stream B: {vector={s1=6}, scope=???}
```

Should B reuse scopeA (subsume semantics) or create new scope?
With propagation, this is implicit and unclear.

**Conclusion**: Alternative doesn't provide clear representation of subsume semantics that are fundamental to the epoch model.

---

### Finding 3: Service Sharing Guarantee Breaks

**Requirement**: "Multiple concurrent blocks processing the same epoch can resolve the same service instance"

**Current Approach**:
```
Block A processes vector {s1=5} → EpochManager.GetOrCreateEpoch({s1=5}) → Epoch{scopeA}
Block B processes vector {s1=5} → EpochManager.GetOrCreateEpoch({s1=5}) → SAME Epoch{scopeA}
```

Dictionary lookup guarantees same epoch = same scope = same service instances.

**Alternative Approach**:
- **Linear pipelines**: Works via propagation
  ```
  Stream → Block A → Stream (same scope) → Block B
  Both blocks access same scope ✅
  ```

- **Fan-in**: Breaks
  ```
  Stream A (scopeA) → serviceX from scopeA
  Stream B (scopeB) → serviceY from scopeB
  Merged Stream (scope???) → Which service???
  ```

**Conclusion**: Alternative breaks the fundamental guarantee at fan-in points.

---

### Finding 4: Alternative Works for Linear Pipelines

**Positive Finding**: For simple, linear topologies without fan-in, the alternative is simpler:

```
Source creates: {vector, items, scope}
   ↓
Transform propagates: {vector', items', SAME scope}
   ↓
Process propagates: {vector', items'', SAME scope}
   ↓
Sink disposes: scope.DisposeAsync()
```

**Benefits for Linear Case**:
- No manager needed
- No dictionary lookups
- Direct service resolution from stream
- Simpler block API (no IBlockContext needed)
- Scope disposal tied to stream disposal

**However**: Limiting the design to linear pipelines is unacceptable because:
- Fan-in is explicitly required
- Complex topologies are the whole point of the library
- Would severely limit use cases

---

### Finding 5: Reference Counting Still Needed

**Initial Hope**: Stream disposal would eliminate reference counting.

**Reality**: With scope propagation, multiple streams share same scope:

```
Input Stream (scope1)
   ├→ Output Stream A (scope1) - used by Block A
   └→ Output Stream B (scope1) - used by Block B
```

When can scope1 be disposed? When both Stream A and Stream B are disposed.

**Conclusion**: Still need reference tracking, just in different form (stream count instead of block count).

**Alternative doesn't eliminate complexity**, just moves it from EpochManager to streams.

---

## Comparative Analysis Summary

| Dimension | Current | Alternative | Winner |
|-----------|---------|-------------|--------|
| **Fan-In Support** | ✅ Clean solution | ❌ Unsolvable | **Current** |
| **Subsume Semantics** | ✅ Explicit | ❌ Unclear | **Current** |
| **Service Sharing** | ✅ Guaranteed | ❌ Breaks at fan-in | **Current** |
| **Linear Pipelines** | ✅ Works | ✅ Simpler | Alternative |
| **Block API** | Via context | Direct on stream | Alternative |
| **Framework Complexity** | Higher | Lower | Alternative |
| **Reference Tracking** | Required | Also required | Tie |

**Critical Dimensions (marked ✅ in "Winner")**: Fan-in, subsume semantics, service sharing

**Non-Critical Dimensions**: Block API, framework complexity (both are acceptable trade-offs)

---

## Recommendation

### Retain Current EpochManager Approach

**Reasons**:

1. **Fan-In is Critical**: Explicitly required, alternative has no solution
2. **Subsume Semantics are Critical**: Fundamental to epoch model, alternative unclear
3. **Service Sharing Must Work**: Core value proposition, alternative breaks it
4. **Framework Complexity is Acceptable**: Worth it for correctness
5. **Block Complexity is Minimal**: Extra indirection is negligible

### Do Not Revise Design

**Conclusion**: The question raised in the issue was:
> "Can you explore this suggestion and see if that looks like a feasible approach and whether it has any merit?"

**Answer**: 
- **Feasible**: No (fails fan-in)
- **Merit**: Some (simpler for linear pipelines, but insufficient)
- **Worth it**: No (breaks critical requirements)

**Action**: Proceed with current design as documented in `/docs/design/epoch-scoped-services/`. No changes to Phase 1 implementation (#415).

---

## Documentation Updates

### Update Design Documentation

The alternative approach should be documented in the epoch-scoped-services design as a **considered alternative** with analysis of why it was rejected. This shows due diligence and helps future readers understand the design decisions.

**Recommendation**: Add section to `/docs/design/epoch-scoped-services/alternatives/comparison.md`:

```markdown
## Alternative: Stream-Coupled DI Scopes

**Proposal**: Couple DI scope directly to IEpochStream rather than manage via EpochManager.

**Evaluation**: Rejected due to:
1. No clean solution for fan-in scope merging
2. Unclear subsume semantics
3. Breaks service sharing guarantee at merge points

**Analysis**: See `/research/epoch-scope-propagation/` for detailed analysis.
```

---

## Related Work

### Research Artifacts

- **Research Plan**: `/research/epoch-scope-propagation/research-plan.md`
- **Current Approach Analysis**: `/research/epoch-scope-propagation/design/current-approach.md`
- **Alternative Approach Analysis**: `/research/epoch-scope-propagation/design/alternative-approach.md`
- **Comparison Matrix**: `/research/epoch-scope-propagation/design/comparison.md`

### Design Documentation

- **Main Design**: `/docs/design/epoch-scoped-services/README.md`
- **Fan-In Scenarios**: `/docs/design/epoch-scoped-services/fan-in-scenarios.md`
- **Alternatives**: `/docs/design/epoch-scoped-services/alternatives/comparison.md`

---

## Next Steps

### For Implementation (Issue #415)

1. ✅ **No changes to Phase 1 plan** - Proceed with EpochManager approach
2. ✅ **Continue with existing design** - All specifications remain valid
3. ✅ **Implement as documented** - No architectural revisions needed

### For Design Documentation

1. ⏭️ **Update alternatives comparison** - Add stream-coupled approach with rejection rationale
2. ⏭️ **Reference this research** - Link to `/research/epoch-scope-propagation/` for details
3. ⏭️ **Document decision** - Show alternative was considered and evaluated

### For Future Consideration

**None recommended**. The alternative approach is fundamentally incompatible with requirements. No hybrid approach is beneficial.

---

## Conclusion

The alternative approach of coupling DI scope to `IEpochStream` was thoroughly evaluated and found to be **not viable** due to:

1. ❌ **Fan-in scope merging** - Unsolvable without centralized management
2. ❌ **Subsume semantics** - No clear representation
3. ❌ **Service sharing** - Breaks at merge points

The current EpochManager approach should be **retained** because:

1. ✅ **Supports all topologies** - Including fan-in
2. ✅ **Clear subsume semantics** - Explicit notifications
3. ✅ **Guarantees service sharing** - Via vector → epoch mapping
4. ✅ **Handles complexity** - Reference counting, lifecycle events

**No design revision needed**. Proceed with implementation per existing design.

---

## Research Metadata

**Research Duration**: 1 day (accelerated due to clear analysis results)  
**Research Phase**: Complete  
**Prototype Created**: No (analysis revealed fundamental issues before prototyping)  
**Recommendation Confidence**: High (clear architectural incompatibility)

**Related Issues**:
- uniun-technology/lib-dataflow#415 - Phase 1 implementation (no changes needed)
- This research issue - Can close after documentation update

**Research Team**: @copilot (Research Duty)
