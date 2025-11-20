# POC Changelog

All notable changes to the POC implementation will be documented in this file.

## [Unreleased]

### Breaking Changes

#### November 2025 - Epoch-Only Architecture (v3.0)

**BREAKING**: Removed plain block variants in favor of unified epoch-based architecture.

**Removed Classes**:
- `ActorBlock<TIn, TOut, TActor>` → Use `EpochActorBlock`
- `BatchBlock<T>` → Use `EpochBatchBlock`
- `ProducerBlock<T>` → Use `EpochSourceBlock` or `PlainSourceAdapter`
- `PlainSourceBlock<T, TActor>` → Use `PlainSourceAdapter`

**Rationale**: Unified epoch-based architecture provides transactional boundaries, checkpointing support, and consistent data processing guarantees across all blocks.

**Migration**:
- Test helpers provide backward-compatible wrappers for easier migration
- `PlainSourceAdapter` enables legacy plain sources to work with epoch pipeline
- All blocks now work with `IAsyncEnumerable<IEpochStream<T>>`
- See `/poc/docs/migrations/v3-epoch-only.md` for detailed migration guide

**Impact**:
- **Tests**: All 371+ tests migrated and passing with wrapper helpers
- **Build**: 0 errors, backward compatibility maintained through test helpers
- **DI Registration**: `AddActorBlock()` now registers `EpochActorBlock`

**Related Documentation**:
- Migration Guide: `/poc/docs/migrations/v3-epoch-only.md`
- Updated Glossary: `/poc/docs/POC_GLOSSARY.md`
- Block Documentation: `/poc/docs/design/blocks/`

---

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
