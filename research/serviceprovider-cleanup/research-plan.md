# Research Plan: DataFlowGraphBuilder Service Provider Cleanup

**Research Issue**: [Research] clean approach for how DataFlowGraphBuilder manages service provider  
**Date**: 2026-01-23  
**Researcher**: Copilot (Research Duty)  
**Status**: 🔄 In Progress

---

## Research Objective

Clean up how `DataFlowGraphBuilder` manages the `IServiceProvider` dependency by:

1. Removing `IServiceProvider` as a constructor parameter
2. Passing `IServiceProvider` to the `Build()` method instead
3. Removing the obsolete constructor
4. Ensuring `EpochConfigurationExtensions.ConfigureEpochs` can still access the service provider
5. Maintaining all existing functionality without breaking changes (except removing obsolete constructor)

---

## Problem Statement

### Current State (Problematic)

**Tension**: Some code paths require access to `IServiceProvider` before `Build()` is called.

**Current Implementation**:
```csharp
public class DataFlowGraphBuilder
{
    private readonly IServiceProvider? _serviceProvider;
    
    // Obsolete constructor (to be removed)
    [Obsolete]
    public DataFlowGraphBuilder(string name, ILogger<DataFlowGraph>? logger = null)
    
    // Current constructor with service provider
    public DataFlowGraphBuilder(string name, IServiceProvider serviceProvider, ...)
    
    internal IServiceProvider? GetServiceProvider() => _serviceProvider;
    
    public DataFlowGraph Build() { ... }
}
```

**The Issue**:
- `IServiceProvider` is a constructor dependency but can be `null` (if obsolete constructor used)
- Only needed by `EpochConfigurationExtensions.ConfigureEpochs` which:
  - Calls `builder.GetServiceProvider()` to get the service provider
  - Creates a coordinator factory using the service provider
  - Calls `builder.SetEpochCoordinator(coordinator)`
- The coordinator factory needs to be managed differently

### Proposed State (Cleaner)

**Changes**:

1. **Remove obsolete constructor** - no longer supported
2. **Remove `IServiceProvider` from constructor** - not a constructor dependency
3. **Change `Build()` signature**:
   ```csharp
   public DataFlowGraph Build(IServiceProvider serviceProvider)
   ```
4. **Update `ConfigureEpochs`** - handle coordinator factory differently

**Key Challenge**: 
- How to manage the `coordinatorFactory` when the service provider isn't available until `Build()` is called?
- The coordinator needs to be created and set before `Build()` is called (current design)

---

## Research Questions

1. **Can we defer coordinator creation until `Build()` time?**
   - Store the configuration instead of the coordinator
   - Create coordinator in `Build()` when service provider is available

2. **What's the cleanest API for `ConfigureEpochs`?**
   - Should it store configuration or coordinator?
   - How to handle the coordinator factory?

3. **Are there other dependencies on early `IServiceProvider` access?**
   - Review all uses of `GetServiceProvider()`
   - Identify any hidden dependencies

4. **What's the impact on existing tests and usage?**
   - How many places need updating?
   - Can we maintain backward compatibility?

5. **Does this align with previous research?**
   - Review epoch-di-improvement research
   - Review epoch-coordinator-handling research

---

## Validation Approach

### Phase 1: Code Analysis
- ✅ Review current implementation
- ✅ Review related research (epoch-di-improvement, epoch-coordinator-handling)
- ⬜ Identify all uses of `GetServiceProvider()`
- ⬜ Analyze `EpochConfigurationExtensions.ConfigureEpochs` in detail
- ⬜ Document current dependencies and call patterns

### Phase 2: Design Options
- ⬜ Design Option A: Defer coordinator creation to Build()
- ⬜ Design Option B: Store coordinator factory function
- ⬜ Design Option C: Alternative approaches
- ⬜ Evaluate trade-offs for each option
- ⬜ Select recommended approach

### Phase 3: Prototyping
- ⬜ Implement recommended approach
- ⬜ Remove obsolete constructor
- ⬜ Update `Build()` method signature
- ⬜ Update `ConfigureEpochs` implementation
- ⬜ Verify all existing tests pass (or update as needed)

### Phase 4: Validation
- ⬜ Run all tests
- ⬜ Verify backward compatibility (except obsolete constructor)
- ⬜ Check for any performance impact
- ⬜ Validate with integration tests

---

## Success Metrics

### Quantitative
- **Constructor parameters**: Reduce from 5 to 4 (remove `IServiceProvider`)
- **Nullable fields**: Remove `_serviceProvider?` field
- **Obsolete code**: Remove 1 obsolete constructor
- **API calls**: `Build()` gains 1 parameter
- **Breaking changes**: Minimal (only obsolete constructor removal)

### Qualitative
- **Cleaner dependency management**: Service provider not needed until build time
- **Clear API**: Build-time dependencies passed to Build()
- **Maintainability**: Less conditional logic around nullable service provider
- **Architectural clarity**: Dependencies required at each phase are explicit

### Baseline vs. Target

| Aspect | Baseline (Current) | Target (Proposed) |
|--------|-------------------|-------------------|
| Constructor params | `IServiceProvider` required | No service provider |
| `Build()` params | None | `IServiceProvider` |
| Obsolete constructors | 1 | 0 |
| Nullable service provider | Yes (`_serviceProvider?`) | No |
| Early SP access | Required for ConfigureEpochs | Deferred to Build() |

---

## Expected Outcomes

1. **Research documentation** in `/research/serviceprovider-cleanup/`
   - Analysis of current code
   - Design options evaluation
   - Recommended approach with rationale

2. **Implementation-ready specification** for implementation team
   - Clear API changes
   - Migration path
   - Test scenarios

3. **Prototype code** demonstrating the approach
   - Working implementation
   - Updated tests
   - Saved in `/research/serviceprovider-cleanup/handover/prototype/`

4. **Formal documentation**
   - ADR documenting the design decision
   - Updated API documentation

---

## Timeline

**Estimated Duration**: 2-3 days

- **Day 1**: Code analysis, design options (Phases 1-2)
- **Day 2**: Prototyping, validation (Phases 3-4)
- **Day 3**: Documentation, handover preparation

---

## Related Research

This research builds on and relates to:

1. **epoch-di-improvement** - Made coordinator factory optional
   - Relevant: Shows how `GetServiceProvider()` is currently used
   - Alignment: This research continues the cleanup trajectory

2. **epoch-coordinator-handling** - Moved coordinator ownership to graph
   - Relevant: Changed how coordinator is managed
   - Alignment: This research further improves the builder API

3. **tech-debt-obsolete-constructors-2025-11** - Addressed obsolete constructors
   - Relevant: This research removes another obsolete constructor
   - Alignment: Continues technical debt cleanup

---

## Next Steps

1. ⬜ Deep dive into current code (Phase 1)
2. ⬜ Design and evaluate options (Phase 2)
3. ⬜ Prototype recommended approach (Phase 3)
4. ⬜ Validate and test (Phase 4)
5. ⬜ Document findings
6. ⬜ Create implementation handover
7. ⬜ Save prototype code
8. ⬜ Revert exploratory changes (after approval)
9. ⬜ Submit self-improvement feedback
