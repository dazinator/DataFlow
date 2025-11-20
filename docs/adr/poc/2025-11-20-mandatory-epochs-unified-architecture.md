# ADR: Mandatory Epochs - Unified Architecture with Single-Epoch Wrapper Pattern

**Date**: 2025-11-20  
**Status**: Accepted  
**Context**: Issue #506 - Make Epochs Mandatory - Unified Architecture  
**Research**: Issue #504, `/research/mandatory-epochs/`

---

## Context

DataFlow originally maintained two parallel implementations for most block types:
- **Plain blocks**: `ActorBlock`, `BatchBlock`, `ProducerBlock`, `PlainSourceBlock` - process plain streams without epoch awareness
- **Epoch blocks**: `EpochActorBlock`, `EpochBatchBlock`, `EpochSourceBlock` - process epoch-aware streams

This duality created:
- **Maintenance burden**: ~120 lines of duplicated code across 4 block pairs
- **Conceptual complexity**: Users must choose between plain vs epoch variants
- **API surface bloat**: ~12 block types when only ~6 are needed
- **Testing overhead**: Must test both plain and epoch code paths

The key architectural question: **Can we unify on epochs without sacrificing plain source support?**

---

## Key Insight: Plain Sources as Implicit Single-Epoch Sequences

The fundamental insight enabling this unification:

> **A plain stream with no explicit epoch boundaries is semantically equivalent to a single, continuous epoch.**

This equivalence means:
- A source producing 1000 items without segmentation = one epoch containing 1000 items
- There's no semantic difference between a plain stream and a single-epoch stream
- Plain sources can be automatically wrapped without changing their behavior

**Implication**: We can treat all blocks as epoch-aware and automatically wrap plain sources, eliminating the need for duplicate implementations.

---

## Options Considered

### Option 1: Maintain Dual Implementations (Status Quo)

**Architecture:**
- Keep both plain and epoch variants of all blocks
- Users choose between variants based on their needs
- Plain and epoch blocks remain separate code paths

**Pros:**
- No breaking changes
- Explicit choice between plain/epoch processing
- Each variant optimized for its use case

**Cons:**
- **Maintenance burden**: ~120 lines of duplicated code
- **Conceptual complexity**: Users confused about when to use which variant
- **Testing overhead**: Must test both code paths
- **API bloat**: Twice as many block types
- **Future scalability**: Every new block type requires two variants

**Verdict**: ❌ Not recommended - complexity outweighs benefits

### Option 2: Unified Epoch-Based Architecture (Chosen)

**Architecture:**
- Single implementation for each block type (epoch-aware)
- Plain sources automatically wrapped in single-epoch streams
- `SingleEpochExtensions.WrapInSingleEpoch()` - wraps plain streams
- `PlainSourceAdapter<T, TActor>` - adapts legacy plain sources
- Deprecate plain block variants with clear migration path

**Pros:**
- **Single mental model**: Everything is epoch-based
- **Code reduction**: Eliminate ~120 lines of duplicated code
- **Simplified API**: 50% reduction in block types
- **Improved maintainability**: One code path to maintain and test
- **Acceptable performance**: <5% overhead (research validated: 4.08%)
- **Backward compatible**: Legacy sources work via adapter

**Cons:**
- **Breaking change**: Requires migration (mitigated with deprecation period)
- **Slight overhead**: 4.08% performance cost for wrapping (acceptable)
- **Migration effort**: Users must update code (clear path provided)

**Verdict**: ✅ **RECOMMENDED** - Benefits far outweigh costs

### Option 3: Hybrid Approach (Considered but Rejected)

**Architecture:**
- Keep plain source blocks but use epoch blocks everywhere else
- Automatic conversion at source boundaries only

**Pros:**
- Minimal breaking changes
- Plain sources remain explicit

**Cons:**
- **Incomplete solution**: Still have conceptual split
- **Partial code duplication**: Some blocks still duplicated
- **Confusing API**: Mixed plain/epoch model harder to understand

**Verdict**: ❌ Not recommended - doesn't fully solve the problem

---

## Decision

**Adopt unified epoch-based architecture** using the single-epoch wrapper pattern.

### Core Pattern

```csharp
// Plain stream automatically wrapped in single epoch
IAsyncEnumerable<T> plainStream = GetItems();
IAsyncEnumerable<IEpochStream<T>> singleEpoch = 
    plainStream.WrapInSingleEpoch("source-name");
```

### Implementation Strategy

**Phase 1: Add Unified Blocks (v2.0)**
1. Implement `SingleEpochExtensions.WrapInSingleEpoch()` extension method
2. Implement `PlainSourceAdapter<T, TActor>` for legacy sources
3. Keep epoch-aware block implementations (EpochActorBlock, etc.)
4. Mark plain variants as `[Obsolete]` with migration guidance

**Phase 2: Migration Period (v2.1-v2.x)**
1. Support both APIs with deprecation warnings
2. Provide comprehensive migration documentation
3. Update examples to use unified API
4. Encourage migration through warnings and docs

**Phase 3: Remove Plain Variants (v3.0)**
1. Remove deprecated plain block types
2. Optionally rename blocks (drop "Epoch" prefix since all are epoch-based)
3. Update all documentation

### Semantic Justification

The single-epoch wrapper is **semantically correct** because:

1. **No information loss**: All items from the plain stream are preserved in the epoch
2. **Ordering preserved**: Items maintain their original order
3. **Natural epoch boundary**: Stream completion marks epoch end (natural boundary)
4. **Compatible with segmentation**: Can still segment into multiple epochs if needed
5. **Cancellation propagation**: Cancellation tokens work correctly through the wrapper

A plain source has no explicit epoch breaks, which is semantically identical to having one continuous epoch that ends when the stream completes.

---

## Consequences

### Positive

1. **Simplified Architecture**
   - Single mental model: everything is epoch-based
   - 50% reduction in block types (from ~12 to ~6)
   - Eliminated ~120 lines of duplicated code

2. **Improved Developer Experience**
   - No confusion about when to use plain vs epoch blocks
   - Clearer API with fewer choices
   - Unified patterns across all blocks

3. **Better Maintainability**
   - Single code path to maintain
   - Single code path to test
   - Easier to add new features (only one implementation needed)

4. **Future Scalability**
   - New block types only need epoch-aware implementation
   - Consistent epoch handling across all blocks
   - Foundation for advanced epoch features

5. **Acceptable Performance**
   - 4.08% overhead (within <5% threshold)
   - Research-validated through benchmarking
   - Real-world pipelines see <2% impact due to I/O dominance

### Negative

1. **Breaking Change**
   - Requires migration from plain to epoch blocks
   - **Mitigation**: Full deprecation cycle (v2.0 → v2.x → v3.0)
   - **Mitigation**: Comprehensive migration guide with examples
   - **Mitigation**: Both APIs work during transition period (12+ months)

2. **Performance Overhead**
   - 4.08% overhead for single-epoch wrapping
   - **Mitigation**: Overhead is acceptable (<5% threshold)
   - **Mitigation**: Real pipelines with I/O see minimal impact
   - **Mitigation**: Users can opt for native epoch sources for zero overhead

3. **Migration Effort**
   - Users must update code to use new blocks
   - **Mitigation**: Multiple migration pathways provided
   - **Mitigation**: PlainSourceAdapter is drop-in replacement (no source changes)
   - **Mitigation**: Clear before/after examples for all scenarios

### Neutral

1. **API Evolution**
   - "Epoch" prefix may be dropped in v3.0 (all blocks are epoch-based)
   - Block naming becomes simpler (ActorBlock instead of EpochActorBlock)
   - No semantic change, just naming evolution

2. **Conceptual Shift**
   - Requires understanding that plain = single epoch
   - May need education for existing users
   - New users won't face this (they'll only know unified model)

---

## Performance Validation

### Research Benchmarks

**Test**: Plain stream vs single-epoch wrapped stream  
**Results**:
- Plain stream throughput: 20.5M items/sec
- Single-epoch wrapped: 19.7M items/sec
- **Overhead: 4.08%** (within acceptable <5% threshold)

**Location**: `/research/mandatory-epochs/benchmarks/single-epoch-overhead-results.md`

### Real-World Impact

In production pipelines with I/O (database reads, file processing, network calls):
- Overhead typically <2% (I/O dominates processing time)
- Negligible impact on end-to-end pipeline performance
- Memory allocation: only epoch metadata (minimal)

---

## Migration Support

### Migration Timeline

| Version | Status | Timeline | Support |
|---------|--------|----------|---------|
| v2.0 | Current | Q4 2025 | Both APIs work, deprecation warnings |
| v2.x | Migration | Q1-Q4 2026 | Both APIs work, migration encouraged |
| v3.0 | Unified | Q1 2027 | Plain blocks removed, epoch-only |

**Users have 12+ months to migrate** from v2.0 to v3.0.

### Migration Pathways

**Option 1: Use PlainSourceAdapter (Recommended)**
```csharp
// Before
var source = new PlainSourceBlock<int, MySource>(context, scopeFactory);

// After - No source code changes needed!
var source = new PlainSourceAdapter<int, MySource>(context, scopeFactory, "source");
```

**Option 2: Make Source Epoch-Aware**
```csharp
public class MySource : ISourceActor<int>
{
    public async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(...)
    {
        yield return GetItems().WrapInSingleEpoch("my-source");
    }
}
```

**Option 3: Use Extension Method**
```csharp
var epochs = plainStream.WrapInSingleEpoch("source-name");
```

### Documentation

- **Migration Guide**: `/poc/MIGRATION_GUIDE.md`
- **Research**: `/research/mandatory-epochs/`
- **Deprecation messages**: All plain blocks have XML docs with migration guidance

---

## Architectural Principles Established

This decision establishes several key principles:

1. **Epochs as First-Class Concept**
   - All data in DataFlow flows through epochs
   - Epochs are not optional, they're fundamental to the architecture
   - Plain sources are just a special case (single epoch)

2. **Semantic Equivalence of Plain and Single-Epoch**
   - A stream with no breaks = one continuous epoch
   - This is not a workaround, it's a semantic truth
   - Wrapping is automatic, transparent, and correct

3. **Unified Mental Model**
   - Users don't choose between plain and epoch
   - All blocks process epochs (some sources just produce single epochs)
   - Simpler conceptual model for the entire system

4. **Backward Compatibility Through Adaptation**
   - Legacy code continues to work via adapters
   - Graceful deprecation period for migration
   - No sudden breaking changes

---

## Related

- **Research Issue**: uniun-technology/lib-dataflow#504
- **Implementation Issue**: uniun-technology/lib-dataflow#506
- **Future Work Issue**: uniun-technology/lib-dataflow#508 (Remove obsolete blocks in v3.0)
- **Research Documentation**: `/research/mandatory-epochs/`
- **Migration Guide**: `/poc/MIGRATION_GUIDE.md`
- **Related ADR**: `2025-11-16-formalized-epoch-system.md` (Establishes epoch fundamentals)
- **Related ADR**: `2025-11-05-epoch-aware-block-pattern.md` (Original epoch pattern)

---

## References

### Code Locations

- **Core Implementation**: 
  - `/poc/DataFlow.POC/Core/SingleEpochExtensions.cs`
  - `/poc/DataFlow.POC/Blocks/PlainSourceAdapter.cs`

- **Tests**:
  - `/poc/DataFlow.POC.Tests/SingleEpochExtensionsTests.cs`
  - `/poc/DataFlow.POC.Tests/PlainSourceAdapterTests.cs`

- **Deprecated Blocks** (marked `[Obsolete]`):
  - `/poc/DataFlow.POC/Blocks/ActorBlock.cs`
  - `/poc/DataFlow.POC/Blocks/BatchBlock.cs`
  - `/poc/DataFlow.POC/Blocks/ProducerBlock.cs`
  - `/poc/DataFlow.POC/Blocks/PlainSourceBlock.cs`

### Research Artifacts

- **Main README**: `/research/mandatory-epochs/README.md`
- **Block Analysis**: `/research/mandatory-epochs/notes/block-pair-analysis.md`
- **Architecture Design**: `/research/mandatory-epochs/design/unified-architecture.md`
- **Benchmark Results**: `/research/mandatory-epochs/benchmarks/single-epoch-overhead-results.md`
- **Implementation Handover**: `/research/mandatory-epochs/handover/README.md`

---

## Revision History

| Date | Change | Author |
|------|--------|--------|
| 2025-11-20 | Initial ADR created | Copilot |
