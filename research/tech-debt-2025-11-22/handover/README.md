# Tech Debt Handover - Plain Block Benchmarks Removal

**Date**: 2025-11-22
**Analysis Issue**: [Tech Debt] Remove Plain Block Benchmarks - Complete Epoch Migration

## Overview

Comprehensive analysis of deprecated plain block usage in POC benchmarks. Identified selective removal strategy balancing technical debt reduction with benchmark utility preservation.

## Product Backlog Items Created

Will be created after reviewer approval of findings report.

**Planned Backlog Items** (Updated with Reviewer Suggestion):

1. **[HIGH]** Remove Plain Block Baselines from EpochAwareBlockBenchmark
   - Remove 2 baseline methods using deprecated blocks
   - Make first epoch benchmark the new baseline
   - Effort: Small (2-4 hours)

2. **[MEDIUM]** **Move ActorBlock & PlainSourceBlock to Benchmark Project** - **NEW - RECOMMENDED FIRST**
   - **Reviewer suggestion**: Move deprecated blocks from main codebase to benchmarks
   - Move to `poc/DataFlow.POC.Benchmarks/DeprecatedBlocks/`
   - Update namespace and all benchmark imports
   - Achieves main goal: removes deprecated code from production codebase
   - Effort: Small (2-3 hours)

3. **[MEDIUM]** Migrate ActorBlockBenchmark to EpochActorBlock
   - Migration from plain `ActorBlock` to `EpochActorBlock`
   - Rotation concept remains valid - important for memory management in high-volume streams
   - Preserves rotation profiling value while using current architecture
   - Effort: Medium (4-6 hours)
   - Alternative: Remove if rotation profiling deemed unnecessary (1-2 hours)

4. **[MEDIUM]** Document Plain Blocks in Architectural Comparison Benchmarks
   - Add clarifying comments to BatchBlockComparisonBenchmark
   - Add clarifying comments to DecoupledEpochBenchmark
   - Document as "architectural comparison, not baseline"
   - Effort: Small (1 hour)

5. **[LOW]** Future: Migrate ETL POCs to Epoch Blocks
   - Create epoch-based ComplexEtlPOC
   - Create epoch-based SimpleEtlPOC
   - Update all benchmark references
   - Update concurrency scaling tests
   - Effort: Large (2-3 weeks) - Future work

## Findings Summary

**Total Findings**: 7 (6 original + 1 new from reviewer)
- High Priority: 1 (baseline removal)
- Medium Priority: 5 (rotation migration + valid comparisons + **move blocks**)
- Low Priority: 1 (helper utility)

**Recommendation**: Selective removal + reorganization - remove deprecated baselines, **move deprecated blocks to benchmark project** (isolates technical debt), optionally migrate rotation benchmarks.

## Key Decisions (Updated with Reviewer Suggestion)

### ✅ Remove:
- Plain block baseline benchmarks (Finding 1) - ~150 lines

### ✅ **Reorganize (NEW - RECOMMENDED):**
- **Move ActorBlock & PlainSourceBlock to benchmark project** (Finding 7)
- Removes deprecated code from main codebase
- Isolates technical debt to benchmark-only context
- ~280 lines moved from `DataFlow.POC/Blocks/` to `DataFlow.POC.Benchmarks/DeprecatedBlocks/`
- **Best compromise solution - achieves main goal**

### 🔄 Migrate (Optional):
- Actor rotation profiling benchmarks (Finding 2) - Migrate to EpochActorBlock
- ~150 lines updated
- Rotation remains important for memory management in high-volume streams

### ✅ Keep:
- BatchBlockComparisonBenchmark plain sources (Finding 3) - Valid comparison
- DecoupledEpochBenchmark plain sources (Finding 4) - Valid comparison
- ETL POC files (Finding 5) - Actively used, future migration

### Block File Status (Updated):
- `ActorBlock.cs` and `PlainSourceBlock.cs` **will be moved** to benchmark project (Finding 7)
- Main codebase (`DataFlow.POC`) becomes free of deprecated blocks ✅
- Still available for benchmarks after move
- Can be eventually deleted when benchmarks migrate (medium-large future work)

## Documentation

- **Findings Report**: `/research/tech-debt-2025-11-22/findings-report.md`
- **Analysis Notes**: `/research/tech-debt-2025-11-22/notes/plain-block-usage-analysis.md`

## Next Steps

1. ✅ Complete findings report
2. ⏳ Get reviewer approval
3. ⏳ Create product backlog items
4. ⏳ Hand over to Product Prioritization Duty

## Files Analyzed

1. `EpochAwareBlockBenchmark.cs` - 8 plain block usages
2. `BatchBlockComparisonBenchmark.cs` - 3 plain block usages
3. `DecoupledEpochBenchmark.cs` - 3 plain block usages
4. `ActorBlockBenchmark.cs` - 9 plain block usages
5. `ComplexEtlPOC.cs` - 18 plain block usages
6. `SimpleEtlPOC.cs` - 7 plain block usages
7. `BenchmarkActorHelpers.cs` - 1 plain block usage (utility)

**Total**: 49 plain block references across 7 files

## Verification Commands

```bash
# Count plain block usages
cd /home/runner/work/lib-dataflow/lib-dataflow
grep -r "ActorBlock<" poc/DataFlow.POC.Benchmarks/ --include="*.cs" | wc -l
grep -r "PlainSourceBlock<" poc/DataFlow.POC.Benchmarks/ --include="*.cs" | wc -l

# Find baseline benchmarks
grep -n "Baseline = true" poc/DataFlow.POC.Benchmarks/EpochAwareBlockBenchmark.cs

# Check ETL POC usage
grep -r "ComplexEtlPOC.BuildDataFlow" poc/DataFlow.POC.Benchmarks/ --include="*.cs"
grep -r "SimpleEtlPOC.BuildDataFlow" poc/DataFlow.POC.Benchmarks/ --include="*.cs"

# Check test dependencies
grep -r "ComplexEtlPOC" poc/DataFlow.POC.Tests/ --include="*.cs"
```

## Impact

**Immediate Removal (High Priority)**:
- Reduction: ~150 lines of deprecated baseline code
- Files affected: 1 (EpochAwareBlockBenchmark.cs)
- Benchmarks removed: 2 baseline methods

**Migration Recommended (Medium Priority)**:
- Migration: ~150 lines updated (ActorBlockBenchmark → EpochActorBlock)
- Files affected: 1 (ActorBlockBenchmark.cs)
- Preserves rotation profiling value
- Demonstrates proper rotation usage in epoch architecture

**Future Work (Large Effort)**:
- Reduction: ~600 lines of code (ETL POCs)
- Files affected: 2
- Requires: Migration to epoch blocks

**Architecture Improvement**:
- Clearer epoch-first architecture
- Removed unmaintained baseline comparisons
- Migration demonstrates proper rotation in epoch context
- Retained valuable architectural comparisons
- Clear path for future complete migration
