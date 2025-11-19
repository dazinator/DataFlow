# Product Backlog Item Templates

These templates will be used to create product backlog issues for each finding.

## Item 1: Remove Obsolete BlockBase Constructor

**Title**: Tech Debt: Remove obsolete BlockBase(string name) constructor

**Type**: tech-debt

**Labels**: tech-debt, workflow:product-backlog, breaking-change

**Description**:
```markdown
# Tech Debt: Remove Obsolete BlockBase Constructor

**Discovered**: Tech debt analysis 2025-11-19 (issue TBD)  
**Category**: Code quality  
**Severity**: High

## Issue Description

The protected `BlockBase<TIn, TOut>(string name)` constructor is marked obsolete but still used by 11 block type constructors. This creates confusion and generates 13 compiler warnings in the POC library build.

## Impact

- **Maintainability**: Blocks have TWO constructors (obsolete + new), creating confusion
- **Code quality**: 13 compiler warnings in POC library build
- **Developer experience**: Unclear which pattern to use for new blocks

## Current State

11 block types internally call `base(name)`:
- BatchBlock.cs
- BroadcastBlock.cs
- EnvelopeBlocks.cs (2 classes)
- EpochActorBlock.cs
- EpochBatchBlock.cs
- EpochSegmenterBlock.cs
- EpochSourceBlock.cs
- PlainSourceBlock.cs
- ProducerBlock.cs (2 constructors)
- RouterBlock.cs (2 classes)

## Verification

```bash
cd /home/runner/work/lib-dataflow/lib-dataflow
grep -r ": base(name)" --include="*.cs" poc/DataFlow.POC/Blocks/
# Should show 13 usages

dotnet build poc/DataFlow.POC/DataFlow.POC.csproj 2>&1 | grep "warning CS0618" | wc -l  
# Should show 13 warnings
```

## Proposed Solution

1. Update all 11 block types to:
   - Remove obsolete constructor with `base(name)`
   - Keep parameterless constructor with `base()`
   - Update XML documentation

2. **Breaking change**: Inline block construction will no longer work
3. **Alternative**: Users must use DI registration or create blocks with separate name setting

## Effort Estimate

**Small** (2-3 days)
- Simple find-replace operation across 11 files
- Each file needs 1-2 constructor removals
- Low risk - tests will catch any issues

## References

- Findings report: `/research/tech-debt-obsolete-constructors-2025-11/findings-report.md`
- Related: PR #480 (DI registration system)
```

---

## Item 2: Migrate ActorBlock Instantiations

**Title**: Tech Debt: Migrate ActorBlock instantiations to DI pattern

**Type**: tech-debt

**Labels**: tech-debt, workflow:product-backlog, breaking-change, tests

**Description**:
```markdown
# Tech Debt: Migrate ActorBlock Instantiations to DI Pattern

**Discovered**: Tech debt analysis 2025-11-19 (issue TBD)  
**Category**: Code quality, testing  
**Severity**: High

## Issue Description

145 instantiations of `ActorBlock<TIn, TOut, TActor>(string name, IServiceScopeFactory)` use the obsolete constructor. This is the most common pattern in tests and benchmarks.

## Impact

- **Test maintenance**: 124 test instantiations need migration
- **Benchmark maintenance**: 21 benchmark instantiations need migration
- **Breaking change**: Removing constructor breaks all existing code

## Current State

```
Total ActorBlock instantiations: 145
  - In tests: 124
  - In benchmarks: 21
```

## Verification

```bash
cd /home/runner/work/lib-dataflow/lib-dataflow
grep -r "new ActorBlock<" --include="*.cs" poc/DataFlow.POC.Tests/ | wc -l
# Should show 124

grep -r "new ActorBlock<" --include="*.cs" poc/DataFlow.POC.Benchmarks/ | wc -l
# Should show 21
```

## Proposed Solution

**Option A: Test Helper Methods** (Recommended)

Create test helper methods that encapsulate DI registration:

```csharp
// TestHelpers/BlockHelpers.cs
public static class BlockHelpers
{
    public static ActorBlock<TIn, TOut, TActor> CreateActor<TIn, TOut, TActor>(
        string name,
        IServiceScopeFactory? scopeFactory = null)
        where TActor : class, IStreamActor<TIn, TOut>
    {
        scopeFactory ??= TestServiceBuilder.Create()
            .WithActor<TActor>()
            .BuildScopeFactory();
        return new ActorBlock<TIn, TOut, TActor>(scopeFactory);
    }
}
```

Migration example:
```csharp
// Before
var actor = new ActorBlock<int, string, MyActor>("actor", scopeFactory);

// After
var actor = BlockHelpers.CreateActor<int, string, MyActor>("actor", scopeFactory);
```

**Option B: Full DI Migration**

Migrate all tests to use `services.AddDataFlows()` - more extensive refactoring.

**Option C: Suppress Warnings**

Least invasive but defeats purpose of cleanup.

## Effort Estimate

**Medium**
- Option A: 5-7 days (create helper, update 145 call sites, verify tests)
- Option B: 7-10 days (redesign test patterns)
- Option C: 1 hour (not recommended)

## References

- Findings report: `/research/tech-debt-obsolete-constructors-2025-11/findings-report.md`
- Test helpers: `/poc/DataFlow.POC.Tests/TestHelpers/`
```

---

## Item 3: Migrate DataFlowGraphBuilder Instantiations

**Title**: Tech Debt: Migrate DataFlowGraphBuilder instantiations to DI pattern

**Type**: tech-debt

**Labels**: tech-debt, workflow:product-backlog, breaking-change, tests

**Description**:
```markdown
# Tech Debt: Migrate DataFlowGraphBuilder Instantiations

**Discovered**: Tech debt analysis 2025-11-19 (issue TBD)  
**Category**: Code quality, testing  
**Severity**: High

## Issue Description

113 instantiations of `DataFlowGraphBuilder(string name, ILogger?)` use the obsolete constructor for inline graph building.

## Impact

- **Test patterns**: 91 test graphs use inline building
- **Benchmark patterns**: 16 benchmarks use inline building
- **Breaking change**: Removing constructor breaks existing test/benchmark code

## Current State

```
Total DataFlowGraphBuilder instantiations: 113
  - In tests: 91
  - In benchmarks: 16
```

## Verification

```bash
cd /home/runner/work/lib-dataflow/lib-dataflow
grep -r "new DataFlowGraphBuilder(" --include="*.cs" poc/DataFlow.POC.Tests/ | wc -l
# Should show 91

grep -r "new DataFlowGraphBuilder(" --include="*.cs" poc/DataFlow.POC.Benchmarks/ | wc -l
# Should show 16
```

## Proposed Solution

**Option A: Test Helper Factory** (Recommended)

```csharp
public static class GraphHelpers
{
    public static DataFlowGraphBuilder CreateBuilder(
        string name,
        IServiceProvider? serviceProvider = null)
    {
        serviceProvider ??= new ServiceCollection().BuildServiceProvider();
        return new DataFlowGraphBuilder(name, serviceProvider);
    }
}
```

**Option B: Suppress for Tests/Benchmarks**

Accept simplified patterns for tests using `#pragma warning disable CS0618`.

**Option C: Full DI Migration**

Migrate all test graphs to `services.AddGraph()` pattern.

## Effort Estimate

**Medium**
- Option A: 3-5 days (create helper, update 113 call sites, verify)
- Option B: 1 hour (add pragmas, document)
- Option C: 5-7 days (extensive refactoring)

## References

- Findings report: `/research/tech-debt-obsolete-constructors-2025-11/findings-report.md`
- DI registration: `/poc/DataFlow.POC/DependencyInjection/`
```

---

## Item 4: Consolidate Block Instantiation Patterns

**Title**: Tech Debt: Consolidate block instantiation patterns with test helpers

**Type**: tech-debt

**Labels**: tech-debt, workflow:product-backlog, developer-experience, tests

**Description**:
```markdown
# Tech Debt: Consolidate Block Instantiation Patterns

**Discovered**: Tech debt analysis 2025-11-19 (issue TBD)  
**Category**: Developer experience, testing  
**Severity**: Medium

## Issue Description

Tests instantiate various block types directly (ProducerBlock: 96, BatchBlock: 7, BroadcastBlock: 7, plus others). Removing obsolete constructors creates opportunity to establish consistent test helper patterns.

## Impact

- **Test consistency**: Multiple patterns for creating blocks
- **Developer experience**: No clear "recommended" pattern
- **Maintainability**: Changes to block construction impact many files

## Current State

```
Direct block instantiations in tests/benchmarks:
  - ProducerBlock: 96
  - BatchBlock: 7
  - BroadcastBlock: 7
  - ActorBlock: 145
  - Other blocks: ~50
  
Total: 300+ instantiations
```

## Verification

```bash
cd /home/runner/work/lib-dataflow/lib-dataflow
grep -r "new.*Block<" --include="*.cs" poc/DataFlow.POC.Tests/ | wc -l
# Should show 300+
```

## Proposed Solution

Create comprehensive `BlockHelpers` test utility:

```csharp
public static class BlockHelpers
{
    // Producer helpers
    public static ProducerBlock<T> CreateProducer<T>(
        string name,
        Func<IExecutionContext, IAsyncEnumerable<T>> producer) { ... }
    
    public static ProducerBlock<T> CreateProducer<T>(
        string name,
        IEnumerable<T> items) { ... }
    
    // Batch helpers
    public static BatchBlock<T> CreateBatch<T>(
        string name,
        int maxBatchSize,
        TimeSpan? windowPeriod = null) { ... }
    
    // Actor helpers
    public static ActorBlock<TIn, TOut, TActor> CreateActor<TIn, TOut, TActor>(...) { ... }
    
    // Etc for all block types
}
```

Benefits:
- Consistent pattern across all block types
- Encapsulation of name-setting logic
- Single place to update if block construction changes
- Improved test readability

## Effort Estimate

**Medium** (5-7 days)
- Create helpers: 2-3 days
- Migrate call sites: 3-4 days
- Verify tests: 1 day

## References

- Findings report: `/research/tech-debt-obsolete-constructors-2025-11/findings-report.md`
- Existing helpers: `/poc/DataFlow.POC.Tests/TestHelpers/`
```

---

## Item 5: Migration Guide and Documentation

**Title**: Tech Debt: Create migration guide for obsolete constructor removal

**Type**: tech-debt

**Labels**: tech-debt, workflow:product-backlog, documentation

**Description**:
```markdown
# Tech Debt: Create Migration Guide and Update Documentation

**Discovered**: Tech debt analysis 2025-11-19 (issue TBD)  
**Category**: Documentation  
**Severity**: Low

## Issue Description

Documentation and examples still show obsolete constructor patterns. Need migration guide for users upgrading to version with obsolete constructors removed.

## Impact

- **External users**: No clear guidance on migrating from obsolete constructors
- **New users**: May learn obsolete patterns from old documentation
- **Upgrade path**: Breaking change needs well-documented migration

## Verification

```bash
cd /home/runner/work/lib-dataflow/lib-dataflow
grep -r "new ActorBlock\|new DataFlowGraphBuilder" --include="*.md" poc/ docs/
# Check for documentation using obsolete patterns
```

## Proposed Solution

1. **Create MIGRATION_GUIDE.md**:
   - Document all breaking changes
   - Provide before/after examples
   - Explain DI registration benefits
   - Show test helper patterns

2. **Update README.md**:
   - Update code examples to use new patterns
   - Add link to migration guide

3. **Update CHANGELOG.md**:
   - Document breaking changes in next version
   - Reference migration guide

4. **Update inline code comments**:
   - Remove obsolete constructor examples
   - Add deprecation notices

Example migration guide structure:
```markdown
# Migration Guide: v1.0 to v2.0

## Breaking Changes

### Removed: Obsolete Block Constructors

**Old Pattern:**
```csharp
var actor = new ActorBlock<int, string, MyActor>("actor", scopeFactory);
var builder = new DataFlowGraphBuilder("graph");
```

**New Pattern:**
```csharp
// Option 1: DI Registration
services.AddDataFlows(df => {
    df.AddBlock("actor", sp => new ActorBlock<int, string, MyActor>(
        sp.GetRequiredService<IServiceScopeFactory>()));
});

// Option 2: Test Helper (tests only)
var actor = BlockHelpers.CreateActor<int, string, MyActor>("actor");
var builder = GraphHelpers.CreateBuilder("graph");
```

## Migration Steps

1. Identify obsolete constructor usage
2. Choose migration approach (DI vs helpers)
3. Update instantiations
4. Verify tests pass
5. Update documentation

## Support

- GitHub issue: #TBD
- PR with examples: #TBD
```

## Effort Estimate

**Small** (2-3 days)
- 1 day: Create comprehensive migration guide
- 1 day: Update README, CHANGELOG, examples
- 1 day: Review and polish

## References

- Findings report: `/research/tech-debt-obsolete-constructors-2025-11/findings-report.md`
- DI registration docs: `/research/di-service-registration/`
```
