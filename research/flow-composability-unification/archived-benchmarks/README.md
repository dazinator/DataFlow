# Archived Baseline Benchmarks

**Status**: HISTORICAL ARTIFACTS - DO NOT COMPILE

## Purpose

These benchmarks were created during the plain blocks consolidation project (Phases 1-3) to:
1. Establish baseline performance metrics for TransformerBlock and ProcessorBlock
2. Validate that ActorBlock had acceptable performance compared to plain blocks
3. Justify the decision to consolidate around ActorBlock pattern for DI scope safety

## Why Archived

These benchmarks use TransformerBlock and ProcessorBlock, which were removed from the codebase in Phase 5 after successful migration to ActorBlock pattern.

They are preserved here as historical artifacts to document:
- The performance analysis methodology used
- The baseline measurements that justified the migration
- The comparison results that validated ActorBlock performance

## Files

- **PlainBlocksBaselineBenchmark.cs** - Baseline performance measurements for TransformerBlock and ProcessorBlock
- **ActorBlockPerformanceValidation.cs** - Comparison benchmarks validating ActorBlock performance

## Results Documentation

The results from these benchmarks are documented in:
- `/poc/docs/benchmarks/plain-blocks-baseline-results.md` - Baseline measurements
- `/poc/docs/benchmarks/actor-block-performance-validation.md` - Validation results and decision rationale

## Note

**These files will not compile** with the current codebase as TransformerBlock and ProcessorBlock have been removed. They remain as reference documentation only.

For current benchmarks using ActorBlock, see `/poc/DataFlow.POC.Benchmarks/`.
