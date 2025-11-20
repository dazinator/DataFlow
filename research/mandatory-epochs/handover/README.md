# Implementation Handover: Mandatory Epochs

**Research Reference**: `/research/mandatory-epochs/`  
**Target Codebase**: POC (`/poc/DataFlow.POC/`)  
**Implementation Ready**: Yes

---

## Objective

Implement unified epoch-based architecture by making epochs mandatory for all blocks, treating plain sources as single-epoch sequences, and removing duplicate block implementations.

---

## Approach (Validated by Research)

### Core Pattern: Single-Epoch Wrapper

Plain streams are wrapped in a single epoch automatically:

```csharp
IAsyncEnumerable<T> plainStream = GetItems();
IAsyncEnumerable<IEpochStream<T>> singleEpoch = 
    plainStream.WrapInSingleEpoch("source-name");
```

**Performance**: 4.08% overhead (within acceptable <5% threshold)

### Architecture Changes

1. **Add Single-Epoch Extensions**: Helper methods for wrapping plain streams
2. **Add Plain Source Adapter**: Adapter for legacy plain sources
3. **Deprecate Plain Blocks**: Mark with `[Obsolete]` and migration guidance
4. **Update Graph Builder**: Convenience methods for automatic wrapping
5. **Update Documentation**: Migration guide and examples

---

## Success Criteria

### Functional Requirements

- [x] `WrapInSingleEpoch()` extension method works correctly
- [x] `PlainSourceAdapter` wraps plain sources automatically
- [ ] Plain block variants marked as `[Obsolete]` with clear messages
- [ ] Graph builder provides `AddPlainSource()` convenience method
- [ ] Existing tests pass with unified blocks
- [ ] New tests cover single-epoch scenarios

### Performance Requirements

- [ ] Performance overhead <5% vs plain streams (benchmark validates 4.08%)
- [ ] No memory leaks in epoch wrapping
- [ ] Epoch metadata properly disposed

### Documentation Requirements

- [ ] Migration guide created
- [ ] Examples updated to use unified API
- [ ] API documentation updated
- [ ] Deprecation warnings are clear

---

## Test Scenarios

### Critical Test Scenarios

1. **Single-Epoch Wrapper**:
   ```csharp
   // Plain stream → single epoch
   var plain = GetPlainStream();
   var epochs = plain.WrapInSingleEpoch("test-source");
   
   // Validate:
   // - Single epoch is yielded
   // - Epoch vector has correct source name and sequence
   // - All items are preserved
   // - Disposal works correctly
   ```

2. **Plain Source Adapter**:
   ```csharp
   // Legacy plain source
   var adapter = new PlainSourceAdapter<int, MyPlainSource>(
       blockContext, scopeFactory, "source");
   
   // Validate:
   // - Produces single epoch stream
   // - All items from source are included
   // - DI scope is properly managed
   // - Cancellation works
   ```

3. **Unified Block with Single Epoch**:
   ```csharp
   // EpochActorBlock processes single-epoch input
   var input = plainStream.WrapInSingleEpoch("source");
   var output = actorBlock.ExecuteAsync(input, context);
   
   // Validate:
   // - Single epoch is processed
   // - Epoch metadata is preserved
   // - Actor rotation works (if requested)
   // - Output has correct epoch vector
   ```

4. **Mixed Sources (Plain + Epoch)**:
   ```csharp
   // One plain source, one epoch source
   builder
       .AddPlainSource<int, PlainSource>("plain")
       .AddEpochSource<int, EpochSource>("epoch")
       .AddMerge<int>("merge")
       .ReceiveFrom("plain", "epoch");
   
   // Validate:
   // - Plain source wrapped automatically
   // - Both sources produce epoch streams
   // - Merge works correctly
   ```

---

## Performance Requirements

### Benchmarks to Run

1. **Single-Epoch Overhead** (Already Completed):
   - Result: 4.08% overhead ✅
   - Location: `/research/mandatory-epochs/benchmarks/single-epoch-overhead-results.md`

2. **Real Pipeline Benchmark** (To Implement):
   - Plain source → transform → batch → process
   - Measure with and without single-epoch wrapping
   - Include realistic I/O (database, file system)
   - Target: <2% overhead in realistic scenario

3. **Memory Benchmark** (To Implement):
   - Validate epoch metadata doesn't leak
   - Check disposal of `EpochStream<T>` instances
   - Measure allocation rate

---

## Design References

### Research Documentation

- **Main README**: `/research/mandatory-epochs/README.md`
- **Block Analysis**: `/research/mandatory-epochs/notes/block-pair-analysis.md`
- **Architecture Design**: `/research/mandatory-epochs/design/unified-architecture.md`
- **Benchmark Results**: `/research/mandatory-epochs/benchmarks/single-epoch-overhead-results.md`

### Prototype Code

Location: `/research/mandatory-epochs/handover/prototype/`

Files:
1. `SingleEpochAdapter.cs` - Extension methods and adapter (copy to POC)
2. `SingleEpochOverheadBenchmark.cs` - Benchmark code (reference only)

**Note**: Prototype code has been validated and is ready for production use after code review.

---

## Implementation Checklist

### Phase 1: Core Implementation

- [ ] Copy `SingleEpochExtensions` to `/poc/DataFlow.POC/Core/SingleEpochExtensions.cs`
- [ ] Copy `PlainSourceAdapter` to `/poc/DataFlow.POC/Blocks/PlainSourceAdapter.cs`
- [ ] Add unit tests for `WrapInSingleEpoch()`
- [ ] Add unit tests for `PlainSourceAdapter`
- [ ] Validate performance with benchmark
- [ ] Code review and merge

### Phase 2: Deprecation

- [ ] Mark `ActorBlock<TIn, TOut>` (plain) as `[Obsolete]`
- [ ] Mark `BatchBlock<T>` (plain) as `[Obsolete]`
- [ ] Mark `ProducerBlock<T>` as `[Obsolete]`
- [ ] Mark `PlainSourceBlock<T>` as `[Obsolete]`
- [ ] Add deprecation messages with migration guidance
- [ ] Update XML documentation

### Phase 3: Graph Builder Integration

- [ ] Add `AddPlainSource<T, TActor>()` method
- [ ] Add optional segmentation parameter to builder
- [ ] Update builder examples
- [ ] Add integration tests

### Phase 4: Documentation

- [ ] Create migration guide (`MIGRATION_GUIDE.md`)
- [ ] Update README with unified architecture
- [ ] Update examples to use unified API
- [ ] Add performance benchmarks to documentation
- [ ] Document deprecation timeline

### Phase 5: Testing

- [ ] Run existing test suite (should pass)
- [ ] Add single-epoch scenario tests
- [ ] Add mixed-source tests (plain + epoch)
- [ ] Performance regression tests
- [ ] Memory leak tests

---

## Migration Guide (Draft)

### For Users with Plain Sources

**Before**:
```csharp
builder
    .AddActorBlock<int, string, MyActor>("transform")
    .ReceiveFrom("plain-source");
```

**After (Option 1: Automatic Wrapping)**:
```csharp
builder
    .AddPlainSource<int, MySource>("plain-source")  // Auto-wraps in single epoch
    .AddActorBlock<int, string, MyActor>("transform")  // Now uses EpochActorBlock
    .ReceiveFrom("plain-source");
```

**After (Option 2: Make Source Epoch-Aware)**:
```csharp
// Convert source to epoch-aware
public class MySource : ISourceActor<int>
{
    public async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(...)
    {
        var items = GetItems();
        yield return items.WrapInSingleEpoch("my-source");
    }
}

builder
    .AddEpochSource<int, MySource>("source")
    .AddActorBlock<int, string, MyActor>("transform")
    .ReceiveFrom("source");
```

### For Users with Epoch Sources

**No changes needed** - already using epochs.

### Deprecation Timeline

- **v2.0** (Q1 2026): Unified blocks introduced, plain variants deprecated
- **v2.1-v2.x** (Q1-Q4 2026): Both APIs supported, migration encouraged
- **v3.0** (Q1 2027): Plain variants removed

---

## Code Locations

### Files to Create

1. `/poc/DataFlow.POC/Core/SingleEpochExtensions.cs`
   - Contains `WrapInSingleEpoch()` extension methods
   - Source: `/research/mandatory-epochs/handover/prototype/SingleEpochAdapter.cs`

2. `/poc/DataFlow.POC/Blocks/PlainSourceAdapter.cs`
   - Contains `PlainSourceAdapter<T, TActor>` class
   - Source: `/research/mandatory-epochs/handover/prototype/SingleEpochAdapter.cs`

3. `/docs/MIGRATION_GUIDE.md`
   - Migration guide for users
   - New file

### Files to Modify

1. `/poc/DataFlow.POC/Blocks/ActorBlock.cs`
   - Add `[Obsolete]` attribute to plain variant

2. `/poc/DataFlow.POC/Blocks/BatchBlock.cs`
   - Add `[Obsolete]` attribute to plain variant

3. `/poc/DataFlow.POC/Blocks/ProducerBlock.cs`
   - Add `[Obsolete]` attribute

4. `/poc/DataFlow.POC/Blocks/PlainSourceBlock.cs`
   - Add `[Obsolete]` attribute

5. `/poc/DataFlow.POC/Builder/DataFlowGraphBuilder.cs`
   - Add `AddPlainSource()` method

### Tests to Create

1. `/poc/DataFlow.POC.Tests/SingleEpochExtensionsTests.cs`
   - Unit tests for wrapper methods

2. `/poc/DataFlow.POC.Tests/PlainSourceAdapterTests.cs`
   - Unit tests for adapter

3. `/poc/DataFlow.POC.Tests/UnifiedBlockTests.cs`
   - Integration tests for unified blocks

---

## Dependencies

### Internal Dependencies

- `IEpochStream<T>` interface (already exists)
- `EpochVector` class (already exists)
- `EpochStream<T>` implementation (already exists, internal)
- `IPlainSourceActor<T>` interface (already exists)

### External Dependencies

None - uses existing .NET and Microsoft.Extensions.DependencyInjection

---

## Rollback Plan

If unification causes issues:

1. **Phase 1-2**: Remove deprecation attributes, continue with dual implementation
2. **Phase 3**: Revert builder changes, keep manual API
3. **Phase 4-5**: Revert new tests, keep existing tests

**Note**: Research validates approach is sound, rollback unlikely needed.

---

## Questions for Implementation

1. Should we provide automatic migration tool (code fixer)?
2. What should deprecation timeline be? (recommended: 12 months)
3. Should we keep "Epoch" prefix during transition or rename immediately?
4. Do we need additional performance benchmarks beyond single-epoch overhead?

---

## Contact

**Research Contact**: GitHub Copilot (Research Duty)  
**Research Location**: `/research/mandatory-epochs/`  
**Implementation Issues**: Create issue tagged with `workflow:implementation`

---

## Approval

This implementation is ready to proceed pending:

- [ ] Code review of research findings
- [ ] Approval of unified architecture approach
- [ ] Agreement on deprecation timeline
- [ ] Revert of exploratory prototype code

Once approved, implementation duty can proceed with checklist above.
