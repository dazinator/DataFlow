# Implementation: DataFlowGraphBuilder Service Provider Cleanup

**Research Reference**: #[RESEARCH_ISSUE_NUMBER]  
**Research Documentation**: `/research/serviceprovider-cleanup/`

---

## Objective

Clean up `DataFlowGraphBuilder` by moving `IServiceProvider` from constructor to `Build()` method, removing pre-build service provider dependency and obsolete constructor.

---

## Approach (Validated by Research)

**Option A: Defer Resolution to Build()**

Store configuration during graph building, defer all DI resolution to `Build()` time when service provider is available.

**Key Changes**:
1. Remove `IServiceProvider` and `IBlockTypeRegistry` from constructor
2. Remove obsolete constructor
3. Change `UseBlock()` to store block names (deferred resolution)
4. Change `ConfigureEpochs()` to store configuration (deferred coordinator creation)
5. Update `Build()` to accept `IServiceProvider` and `IBlockTypeRegistry` parameters
6. Update all call sites to pass parameters to `Build()`

---

## Success Criteria

### Core Library
- [ ] Core library builds without errors
- [ ] No obsolete constructors remain
- [ ] `IServiceProvider` not a constructor parameter
- [ ] `Build()` accepts `IServiceProvider` and `IBlockTypeRegistry`
- [ ] `UseBlock()` defers block resolution
- [ ] `ConfigureEpochs()` defers coordinator creation

### Tests
- [ ] All tests compile
- [ ] All tests pass
- [ ] Test helpers updated
- [ ] ~100+ test sites updated to pass `Build()` parameters

### API Changes
- [ ] Service registration API unchanged (backward compatible)
- [ ] `ConfigureEpochs()` API unchanged (backward compatible)
- [ ] Breaking changes limited to `Build()` signature only

---

## Implementation Checklist

### Phase 1: Core Changes

#### DataFlowGraphBuilder.cs
- [ ] Remove `_serviceProvider` and `_registry` fields
- [ ] Add `_pendingBlockNames` field for deferred block resolution
- [ ] Add `_epochConfig` and `_epochCoordinatorFactory` fields for deferred coordinator creation
- [ ] Remove obsolete constructor `DataFlowGraphBuilder(string name, ILogger<DataFlowGraph>? logger = null)`
- [ ] Update remaining constructor to remove `IServiceProvider` and `IBlockTypeRegistry` parameters
- [ ] Remove `GetServiceProvider()` method
- [ ] Update `UseBlock()` to store block names instead of resolving immediately
- [ ] Change `SetEpochCoordinator()` to `SetEpochConfiguration()` to store config instead of coordinator
- [ ] Update `Build()` signature to `Build(IServiceProvider serviceProvider, IBlockTypeRegistry registry)`
- [ ] Update `Build()` implementation to:
  - Resolve blocks added via `UseBlock()` using service provider and registry
  - Create epoch coordinator from stored configuration
- [ ] Add `CreateDefaultCoordinatorFactory()` helper method
- [ ] Add `using DataFlow.POC.Checkpointing;` directive

#### EpochConfigurationExtensions.cs
- [ ] Remove `GetServiceProvider()` call
- [ ] Update to call `SetEpochConfiguration()` instead of creating coordinator immediately
- [ ] Update XML documentation

#### ServiceCollectionExtensions.cs
- [ ] Update `AddGraph()` to pass service provider and registry to `Build()`
- [ ] Update `AddGraphDefinition()` to pass service provider and registry to `Build()`

### Phase 2: Test Updates

#### TestHelpers/GraphHelpers.cs
- [ ] Update `CreateGraphBuilder()` to use new constructor
- [ ] Update `CreateGraph()` to pass service provider and registry to `Build()`

#### Individual Test Files
- [ ] Fix ~100+ test sites that call `builder.Build()` without parameters
- [ ] Pattern: Add service provider and registry parameters to `Build()` calls
- [ ] Use `GraphHelpers.CreateGraph()` helper where appropriate

**Files to Update** (partial list from compilation errors):
- BasicFlowTests.cs
- BatchFlowTests.cs
- BroadcastFlowTests.cs
- ComplexFlowTests.cs
- ActorBlockTests.cs
- AsyncLocalPropagationTests.cs
- BlockHelpersTests.cs
- BlockTypeRegistryTests.cs
- ConcurrencyScalingTests.cs
- Documentation/*.cs
- ... (see build errors for complete list)

### Phase 3: Validation
- [ ] Core library builds
- [ ] Test library builds
- [ ] All tests pass
- [ ] No regressions in functionality

### Phase 4: Documentation
- [ ] Update API documentation for `DataFlowGraphBuilder`
- [ ] Create migration guide in `/docs/migration/`
- [ ] Update examples in `/docs/guides/`
- [ ] Create ADR in `/docs/adr/poc/`
- [ ] Update README if needed

---

## Migration Guide

### For Production Code

**Before**:
```csharp
var builder = new DataFlowGraphBuilder(name, serviceProvider, registry);
configure(builder);
return builder.Build();
```

**After**:
```csharp
var builder = new DataFlowGraphBuilder(name);
configure(builder);
return builder.Build(serviceProvider, registry);
```

### For Test Code

**Before**:
```csharp
var serviceProvider = services.BuildServiceProvider();
var registry = new BlockTypeRegistry();
var builder = new DataFlowGraphBuilder("test", serviceProvider, registry);
var graph = builder.Build();
```

**After**:
```csharp
var serviceProvider = services.BuildServiceProvider();
var registry = new BlockTypeRegistry();
var builder = new DataFlowGraphBuilder("test");
var graph = builder.Build(serviceProvider, registry);
```

**Or Use Test Helper**:
```csharp
var graph = GraphHelpers.CreateGraph("test", builder => {
    builder.AddBlock(producer)
        .AddBlock(transformer)
        .Connect(producer, transformer);
});
```

### For Service Registration (No Change)

```csharp
services.AddDataFlows("my-flows", df => {
    df.AddGraph("my-graph", g => {
        g.UseBlock("producer")
         .UseBlock("transformer");
    });
});
```

**No Change Required**: Internal implementation handles new pattern.

---

## Test Scenarios

### Scenario 1: AddBlock() with Direct Block Instances
```csharp
[Fact]
public void AddBlock_Works()
{
    var serviceProvider = new ServiceCollection().BuildServiceProvider();
    var registry = new BlockTypeRegistry();
    var builder = new DataFlowGraphBuilder("test");
    var producer = new TestProducer();
    builder.AddBlock(producer);
    var graph = builder.Build(serviceProvider, registry);
    
    Assert.NotNull(graph);
}
```

### Scenario 2: UseBlock() with DI Resolution
```csharp
[Fact]
public void UseBlock_ResolvesFromDI()
{
    var services = new ServiceCollection();
    services.AddDataFlows("test", df => {
        df.AddProducer<TestProducer>("producer");
    });
    var serviceProvider = services.BuildServiceProvider();
    var registry = serviceProvider.GetRequiredService<IBlockTypeRegistry>();
    
    var builder = new DataFlowGraphBuilder("test");
    builder.UseBlock("producer");
    var graph = builder.Build(serviceProvider, registry);
    
    Assert.NotNull(graph);
}
```

### Scenario 3: ConfigureEpochs() with Deferred Coordinator
```csharp
[Fact]
public void ConfigureEpochs_CreatesCoordinator()
{
    var serviceProvider = new ServiceCollection().BuildServiceProvider();
    var registry = new BlockTypeRegistry();
    
    var builder = new DataFlowGraphBuilder("test");
    builder.ConfigureEpochs(config => {
        config.SetPolicy(EpochPolicy.ByCount(100));
        config.AddProcessor("proc1");
    });
    var graph = builder.Build(serviceProvider, registry);
    
    Assert.NotNull(graph.EpochCoordinator);
}
```

---

## Performance Requirements

**Baseline**: Current implementation performance

**Target**: No performance degradation

**Validation**:
- Same number of allocations
- Same resolution logic
- Same coordinator creation
- Deferred operations should not impact runtime performance

---

## Design References

- **Research**: `/research/serviceprovider-cleanup/README.md` - Complete research findings
- **Code Analysis**: `/research/serviceprovider-cleanup/notes/01-code-analysis.md`
- **Design Options**: `/research/serviceprovider-cleanup/design/options-analysis.md`
- **Prototype**: `/research/serviceprovider-cleanup/notes/02-prototype-findings.md`
- **Related Research**:
  - `/research/epoch-di-improvement/` - Made coordinator factory optional
  - `/research/epoch-coordinator-handling/` - Moved coordinator ownership to graph

---

## Implementation Notes

### Breaking Changes

**Only one breaking change**:
- `Build()` signature changes from no parameters to requiring `IServiceProvider` and `IBlockTypeRegistry`

**Not breaking**:
- Service registration API (unchanged)
- `ConfigureEpochs()` API (unchanged)
- `UseBlock()` behavior (same, just deferred)
- `AddBlock()` behavior (unchanged)

### Edge Cases

1. **Connect() before Build()**: Works because `_blocksByName` is populated during Build()
2. **Multiple Build() calls**: Not supported (blocks already added to graph)
3. **Null service provider**: Throws `ArgumentNullException` at Build() time
4. **Missing blocks in UseBlock()**: Throws at Build() time with clear error

### Common Mistakes to Avoid

- ❌ Don't try to call `Build()` without parameters
- ❌ Don't pass service provider to constructor (no longer exists)
- ❌ Don't expect blocks from `UseBlock()` to be resolved immediately
- ✅ Do pass service provider and registry to `Build()`
- ✅ Do use test helpers for consistent patterns
- ✅ Do validate Build() parameters are not null

---

## For Implementation Team

**Key Points**:
1. Core library changes are straightforward - mostly removing code
2. Test updates are mechanical - consistent pattern across all sites
3. No functional changes - behavior is identical, just deferred
4. Service registration API unchanged - internal only
5. Prototype validated - core library builds successfully

**Estimated Effort**:
- Core changes: 2-4 hours
- Test updates: 4-6 hours (mechanical but many sites)
- Validation: 2-4 hours
- Documentation: 4-6 hours

**Total**: 12-20 hours

---

## For @copilot

**Duty**: Follow `.team/duties/IMPLEMENTATION_DUTY.md` for complete implementation process.

**Quick Reference**:
- Label `workflow:implementation` ensures correct workflow routing
- See `.github/copilot-instructions.md` for workflow system
- Complete self-improvement evaluation before PR review
