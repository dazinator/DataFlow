# Implementation Plan: Plain Blocks Consolidation

## Implementation Status

**Current Phase**: Phase 3 Complete - Ready for Phase 4  
**Issue**: #155  
**Original Handover**: `/research/flow-composability-unification/handover/github-issue-consolidate-plain-blocks.md`

## Objective

Consolidate TransformerBlock and ProcessorBlock around the ActorBlock pattern to provide DI scope safety by default.

## Phases

### ✅ Phase 1: Baseline Performance Capture (COMPLETE)

**Status**: Complete  
**Completed**: 2025-11-06  
**PR**: [Link to PR]

**Deliverables**:
- Created comprehensive benchmarks for TransformerBlock and ProcessorBlock
- Documented results in `/poc/docs/benchmarks/plain-blocks-baseline-results.md`
- Established baseline metrics for all scenarios

**Key Metrics**:
- TransformerBlock-1to1: 611,598 items/sec
- TransformerBlock-1toMany: 476,007 items/sec
- TransformerBlock-Filtering: 556,041 items/sec
- ProcessorBlock-Simple: 664,037 items/sec
- ProcessorBlock-Async: 876 items/sec (with 1ms I/O delay)

**Files Created**:
- `/poc/DataFlow.POC.Benchmarks/PlainBlocksBaselineBenchmark.cs`
- `/poc/docs/benchmarks/plain-blocks-baseline-results.md`

---

### ✅ Phase 2: Mark Blocks as Obsolete (COMPLETE)

**Status**: Complete  
**Completed**: 2025-11-06  
**PR**: [Link to PR]

**Deliverables**:
- Added `[Obsolete]` attributes to TransformerBlock, SimpleTransformerBlock, ProcessorBlock
- Created comprehensive migration guide
- 100+ obsolete warnings now guide developers to ActorBlock
- No breaking changes - code still compiles with warnings

**Files Modified**:
- `/poc/DataFlow.POC/Blocks/TransformerBlock.cs` (added [Obsolete] attribute)
- `/poc/DataFlow.POC/Blocks/ProcessorBlock.cs` (added [Obsolete] attribute)

**Files Created**:
- `/poc/docs/migrations/actor-block-migration.md`

---

### ✅ Phase 3: ActorBlock Performance Validation (COMPLETE)

**Status**: Complete  
**Completed**: 2025-11-06  
**PR**: [Link to PR]

**Deliverables**:
- Created ActorBlock equivalent benchmarks
- Validated I/O-bound scenarios meet performance targets
- Documented findings and analysis

**Results**:
- ✅ I/O-bound scenario (most realistic): 0.69% improvement (PASS)
- ⚠️ CPU-bound microbenchmarks: High variance due to measurement noise
- ✅ No catastrophic performance degradation
- **Decision**: Proceed with consolidation - safety benefits outweigh potential costs

**Files Created**:
- `/poc/DataFlow.POC.Benchmarks/ActorBlockPerformanceValidation.cs`
- `/poc/docs/benchmarks/actor-block-performance-validation.md`

**Supporting Documentation**:
- `/poc/docs/plain-blocks-consolidation-status.md` (detailed status and guidance)
- `.github/workflow-improvements.md` (self-improvement evaluation)

---

### 🚧 Phase 4: Migrate and Consolidate Tests (IN PROGRESS)

**Status**: Not Started - Ready to Begin  
**Estimated Effort**: 4-8 hours  
**Next Action**: Systematic test migration using documented patterns

**Objectives**:
1. Migrate all tests using TransformerBlock and ProcessorBlock to ActorBlock
2. Consolidate redundant tests (target: 20-40% reduction)
3. Ensure all tests pass after migration
4. Document test consolidation decisions

**Approach**:
1. **Identify Tests**: ~17 test files with 100+ obsolete warnings
   ```bash
   grep -r "TransformerBlock\|ProcessorBlock" /poc/DataFlow.POC.Tests --include="*.cs" -l
   ```

2. **Migration Pattern** (see `/poc/docs/migrations/actor-block-migration.md` for details):
   - Create actor classes implementing `IStreamActor<TIn, TOut>`
   - Register actors as scoped services in DI
   - Replace block instantiation with ActorBlock
   - Update tests to set up service collection

3. **Test Consolidation Guidelines**:
   - Identify duplicate scenarios across different block types
   - Consolidate overlapping edge case tests
   - Create test coverage matrix to identify redundancies
   - Document consolidation decisions in `/implementation/plain-blocks-consolidation/test-consolidation-report.md`

4. **Files to Migrate** (estimated):
   - BasicFlowTests.cs
   - BatchFlowTests.cs
   - RoutingFlowTests.cs
   - BroadcastFlowTests.cs
   - ActorBlockTests.cs
   - AsyncLocalPropagationTests.cs
   - ComplexFlowTests.cs
   - EdgeStrategyTests.cs
   - BufferNodeTests.cs
   - EnvelopeBlocksTests.cs
   - (and ~7 more test files)

**Success Criteria**:
- [ ] All tests using ActorBlock pattern
- [ ] Test count reduced by 20-40%
- [ ] All tests passing
- [ ] Test consolidation report created

**Migration Commands**:
```bash
# Build and test iteratively
cd /home/runner/work/lib-dataflow/lib-dataflow
dotnet build poc/DataFlow.POC.Tests/DataFlow.POC.Tests.csproj
dotnet test poc/DataFlow.POC.Tests/DataFlow.POC.Tests.csproj
```

---

### ⏳ Phase 5: Remove Obsolete Blocks (PENDING)

**Status**: Blocked on Phase 4  
**Estimated Effort**: 1-2 hours

**Prerequisites**:
- [ ] Phase 4 complete
- [ ] All tests migrated to ActorBlock
- [ ] All tests passing
- [ ] No remaining usages in codebase

**Actions**:
1. **Verify No Remaining Usages**:
   ```bash
   grep -r "new TransformerBlock\|new ProcessorBlock\|new SimpleTransformerBlock" /poc --include="*.cs"
   ```

2. **Delete Files**:
   - `/poc/DataFlow.POC/Blocks/TransformerBlock.cs`
   - `/poc/DataFlow.POC/Blocks/ProcessorBlock.cs`

3. **Update Documentation**:
   - Remove references to obsolete blocks
   - Update POC README to promote ActorBlock
   - Add to CHANGELOG: "BREAKING: TransformerBlock/ProcessorBlock removed, use ActorBlock"

4. **Final Validation**:
   - All tests pass
   - Benchmarks still run
   - Documentation builds successfully

**Success Criteria**:
- [ ] Obsolete blocks deleted
- [ ] No compilation errors
- [ ] All tests passing
- [ ] Documentation updated

---

## How to Continue This Implementation

If you need to continue this implementation in a future session:

1. **Check Current Status**: Review this plan to see which phase is current
2. **Read Phase Documentation**: Each completed phase has detailed documentation in `/poc/docs/`
3. **Follow Phase Instructions**: The current phase section above has specific steps to execute
4. **Update This Plan**: Mark phases as complete and update status as you progress

### For Copilot Agents

When asked to "continue the implementation" or similar:

1. Read this plan document first: `/implementation/plain-blocks-consolidation/plan.md`
2. Identify the current phase from the status markers (✅ complete, 🚧 in progress, ⏳ pending)
3. Read the handover document: `/research/flow-composability-unification/handover/github-issue-consolidate-plain-blocks.md`
4. Review completed phase documentation in `/poc/docs/`
5. Execute the next phase following the instructions above
6. Update this plan as phases complete

## References

- **Original Handover**: `/research/flow-composability-unification/handover/github-issue-consolidate-plain-blocks.md`
- **Research Analysis**: `/research/flow-composability-unification/notes/actor-block-consolidation-analysis.md`
- **Migration Guide**: `/poc/docs/migrations/actor-block-migration.md`
- **Status Document**: `/poc/docs/plain-blocks-consolidation-status.md`
- **Benchmark Results**: `/poc/docs/benchmarks/`
- **Workflow Improvements**: `.github/workflow-improvements.md`

## Notes

- This is POC code that has not been released externally - breaking changes are acceptable
- Performance validation shows safety benefits justify consolidation
- Migration patterns are well-documented for straightforward execution
- Test consolidation guidelines help reduce redundancy while maintaining coverage
