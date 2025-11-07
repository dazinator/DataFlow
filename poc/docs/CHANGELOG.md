# POC Changelog

All notable changes to the POC implementation will be documented in this file.

## [Unreleased]

### Breaking Changes

#### November 2025 - Plain Blocks Consolidation

**BREAKING**: Removed `TransformerBlock` and `ProcessorBlock` in favor of `ActorBlock`.

**Rationale**: Consolidation around ActorBlock provides DI scope safety by default, preventing common concurrency bugs related to dependency sharing and memory leaks.

**Migration**:
- All transformation and processing operations now use `ActorBlock<TIn, TOut, TActor>`
- See `/poc/docs/migrations/actor-block-migration.md` for detailed migration guide
- Performance validation shows <2% overhead in realistic I/O-bound scenarios

**Impact**:
- **Tests**: All 174 tests migrated and passing
- **Benchmarks**: All active benchmarks migrated (27 usages updated)
- **Historical Artifacts**: Baseline performance benchmarks archived to `/research/flow-composability-unification/archived-benchmarks/`

**Related Documentation**:
- Migration Guide: `/poc/docs/migrations/actor-block-migration.md`
- Performance Validation: `/poc/docs/benchmarks/actor-block-performance-validation.md`
- Baseline Results: `/poc/docs/benchmarks/plain-blocks-baseline-results.md`

---

## Notes

- This POC has not been released externally, so breaking changes are acceptable
- All changes focus on improving safety and maintainability
- Performance impact is validated before major architectural changes
