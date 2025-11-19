# Tech Debt Handover: Obsolete Constructor Refactoring

**Analysis Date**: 2025-11-19  
**Tech Debt Issue**: (will be filled by issue number)  
**Analyst**: Copilot (Tech Debt Duty)

---

## Overview

This handover contains findings from the analysis of obsolete constructors following PR #480 (DI registration system). Five technical debt items have been identified and will be created as product backlog items for prioritization.

---

## Product Backlog Items Created

### High Priority (Breaking Changes)

1. **#485 - Remove Obsolete BlockBase Constructor**
   - Severity: High
   - Effort: Small (2-3 days)
   - Impact: 11 block types, 13 compiler warnings
   - Breaking: Yes
   - URL: https://github.com/uniun-technology/lib-dataflow/issues/485
   
2. **#486 - Migrate ActorBlock Instantiations to DI Pattern**
   - Severity: High
   - Effort: Medium (5-7 days with helpers, or 2-3 days with pragmas)
   - Impact: 145 instantiations (124 tests, 21 benchmarks)
   - Breaking: Yes
   - URL: https://github.com/uniun-technology/lib-dataflow/issues/486

3. **#487 - Migrate DataFlowGraphBuilder Instantiations**
   - Severity: High  
   - Effort: Medium (3-5 days)
   - Impact: 113 instantiations (91 tests, 16 benchmarks)
   - Breaking: Yes
   - URL: https://github.com/uniun-technology/lib-dataflow/issues/487

### Medium Priority (Developer Experience)

4. **#489 - Consolidate Block Instantiation Patterns**
   - Severity: Medium
   - Effort: Medium (5-7 days)
   - Impact: 300+ block instantiations, test helper creation
   - Breaking: No (enhancement)
   - URL: https://github.com/uniun-technology/lib-dataflow/issues/489

### Low Priority (Documentation)

5. **#488 - Create Migration Guide and Update Documentation**
   - Severity: Low
   - Effort: Small (2-3 days)
   - Impact: External users, upgrade path
   - Breaking: No (documentation only)
   - URL: https://github.com/uniun-technology/lib-dataflow/issues/488

---

## Migration Strategy Recommendation

See `/research/tech-debt-obsolete-constructors-2025-11/findings-report.md` for complete migration strategy.

**Recommended Approach**: Phased Migration

### Option A: All-at-Once (18-26 days)
- Implement all 5 phases sequentially
- Single breaking change release
- Comprehensive migration in one go

### Option B: Phased Deprecation (Recommended)
- **Version N** (Current): Constructors marked obsolete with warning
- **Version N+1**: Constructors marked obsolete with error (3-6 month window)
- **Version N+2**: Constructors removed entirely

**Benefit of Option B**: Gives users migration time while making progress internally

---

## Impact Summary

### Code Changes Required

| Category | Files | Instantiations | Effort |
|----------|-------|----------------|--------|
| Block types | 11 | 13 (internal) | Small |
| Test files | ~50 | 315+ | Large |
| Benchmark files | ~10 | 37+ | Medium |
| Documentation | ~5 | N/A | Small |

### Breaking Changes

**External API**: 
- `ActorBlock<TIn, TOut, TActor>(string name, IServiceScopeFactory)` - REMOVED
- `BlockBase<TIn, TOut>(string name)` - REMOVED (protected)
- `DataFlowGraphBuilder(string name, ILogger?)` - REMOVED

**Migration Path**:
- Use DI registration via `services.AddDataFlows()`
- For tests: Use helper methods or inline DI setup
- See migration guide (to be created in Finding 5)

---

## Dependencies

### Prerequisites
- Benchmark build errors fixed (unrelated `RecoveryCheckpoint` issue)
- Decision on migration strategy (Option A vs B)
- Agreement on test helper patterns

### Optional Enhancements
- Automated refactoring scripts
- Additional test helper infrastructure
- Performance benchmarking baseline

---

## Success Criteria

- [ ] All 5 product backlog items created
- [ ] Backlog items prioritized by product team
- [ ] Migration strategy agreed upon
- [ ] Timeline established for breaking changes
- [ ] External users notified if applicable

---

## Next Steps

1. **Product Prioritization Duty** will review and prioritize findings
2. High-priority items may be selected for immediate implementation
3. Medium/low-priority items may be deferred or combined
4. Migration strategy will be finalized based on business priorities

---

## Findings Report Location

Complete analysis: `/research/tech-debt-obsolete-constructors-2025-11/findings-report.md`

---

## Questions for Product Team

1. **Timing**: When should breaking changes be released?
2. **Strategy**: Prefer all-at-once or phased deprecation?
3. **Priority**: Should this block other feature development?
4. **Support**: How long should we maintain obsolete constructors?
5. **External users**: Are there external users we need to notify?

---

## Related Work

- **PR #480**: Add dependency injection registration system
- **Research**: `/research/di-service-registration/`
- **Test Helpers**: `/poc/DataFlow.POC.Tests/TestHelpers/`
- **Current warnings**: 13 in POC library (build poc/DataFlow.POC/DataFlow.POC.csproj)
