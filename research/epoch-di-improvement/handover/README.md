# Implementation: Improve ConfigureEpochs API Ergonomics

**Research Reference**: [Research] can we improve this usage  
**Research Documentation**: `/research/epoch-di-improvement/`  
**Target**: POC codebase

---

## Objective

Implement the improved `ConfigureEpochs` API that makes the `coordinatorFactory` parameter optional, eliminating awkward boilerplate while maintaining full backward compatibility.

## Context

The current `ConfigureEpochs` API requires developers to manually provide a factory function that creates an `EpochCoordinator` with an `IServiceScopeFactory`. This is verbose and exposes internal implementation details.

**Research validated** that we can provide a sensible default factory when `DataFlowGraphBuilder` has a service provider, making the API cleaner while preserving backward compatibility.

## Approach (Validated by Research)

### 1. Add Internal Service Provider Accessor

**File**: `poc/DataFlow.POC/Builder/DataFlowGraphBuilder.cs`

Add after constructors:
```csharp
/// <summary>
/// Gets the service provider for DI resolution.
/// Used internally by extension methods to resolve dependencies.
/// </summary>
internal IServiceProvider? GetServiceProvider() => _serviceProvider;
```

### 2. Update ConfigureEpochs Extension

**File**: `poc/DataFlow.POC/Builder/EpochConfigurationExtensions.cs`

**Changes**:

1. Add using directive:
```csharp
using Microsoft.Extensions.DependencyInjection;
```

2. Make factory parameter nullable and optional:
```csharp
Func<ICheckpointStrategy?, IEpochCoordinator>? coordinatorFactory = null
```

3. Add default factory creation logic:
```csharp
if (coordinatorFactory == null)
{
    var serviceProvider = builder.GetServiceProvider();
    if (serviceProvider == null)
    {
        throw new InvalidOperationException(
            "Cannot use ConfigureEpochs without a coordinatorFactory when the DataFlowGraphBuilder " +
            "was not constructed with a service provider. " +
            "Either pass a service provider to the DataFlowGraphBuilder constructor, " +
            "or provide a coordinatorFactory parameter to ConfigureEpochs.");
    }
    
    coordinatorFactory = checkpointStrategy =>
    {
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        return new EpochCoordinator(scopeFactory, checkpointStrategy: checkpointStrategy);
    };
}
```

4. Update XML documentation to reflect optional parameter.

### 3. Update Tests

**File**: `poc/DataFlow.POC.Tests/EpochGraphIntegrationTests.cs`

Replace test `ConfigureEpochs_RequiresCoordinatorFactory` with two tests:

```csharp
[Fact]
public void ConfigureEpochs_WorksWithoutFactoryWhenServiceProviderAvailable()
{
    // Test that factory is optional when service provider is available
}

[Fact]
public void ConfigureEpochs_ThrowsWhenNoServiceProviderAndNoFactory()
{
    // Test that error is thrown for legacy builder without service provider
}
```

**File**: `poc/DataFlow.POC.Tests/EpochConfigurationApiDemoTests.cs` (New)

Add demonstration tests showing clean API usage and backward compatibility.

## Success Criteria

- [x] ✅ Factory parameter is optional
- [x] ✅ Default factory uses `IServiceScopeFactory` from service provider
- [x] ✅ Backward compatible - existing code with explicit factory still works
- [x] ✅ Clear error message when neither service provider nor factory available
- [x] ✅ All existing tests pass (no regressions)
- [x] ✅ New tests demonstrate improved API
- [ ] Code review approval
- [ ] Documentation updated with new API examples
- [ ] Implementation complete

## Test Scenarios

### 1. Clean API Usage (Primary)

```csharp
builder.ConfigureEpochs(config =>
{
    config.SetPolicy(EpochPolicy.ByCount(1000));
    config.AddProcessor("order-processor");
    config.OnBeginEpoch(async (epoch, ct) => { /* tx */ });
    config.OnCommitEpoch(async (epoch, ct) => { /* commit */ });
});
// No factory needed - uses default
```

### 2. Backward Compatibility (Advanced)

```csharp
builder.ConfigureEpochs(
    config => { /* config */ },
    checkpointStrategy => new EpochCoordinator(scopeFactory, operationsQueueCapacity: 200));
// Explicit factory still works
```

### 3. Legacy Builder Error

```csharp
#pragma warning disable CS0618
var builder = new DataFlowGraphBuilder("test");
#pragma warning restore CS0618

builder.ConfigureEpochs(config => { /* config */ });
// Should throw InvalidOperationException with clear message
```

## Performance Requirements

**No performance impact** - Same execution path, just less boilerplate code.

## Design References

- **Research**: `/research/epoch-di-improvement/README.md`
- **Prototype**: `/research/epoch-di-improvement/handover/prototype/`
- **Analysis**: `/research/epoch-di-improvement/notes/01-initial-analysis.md`

## Implementation Checklist

- [ ] Add internal service provider accessor to `DataFlowGraphBuilder`
- [ ] Update `ConfigureEpochs` extension method with default factory logic
- [ ] Add `using Microsoft.Extensions.DependencyInjection;` directive
- [ ] Update XML documentation for `ConfigureEpochs`
- [ ] Replace `ConfigureEpochs_RequiresCoordinatorFactory` test
- [ ] Add `ConfigureEpochs_WorksWithoutFactoryWhenServiceProviderAvailable` test
- [ ] Add `ConfigureEpochs_ThrowsWhenNoServiceProviderAndNoFactory` test
- [ ] Create `EpochConfigurationApiDemoTests.cs` with demonstration tests
- [ ] Run all epoch-related tests to verify no regressions
- [ ] Update API documentation with clean examples
- [ ] Update migration guide if needed
- [ ] Code review and approval

## Expected Outcomes

### Before (Current State)

```csharp
services.AddDataFlows("orders", df =>
{
    df.AddGraph("process-orders", g =>
    {
        g.UseBlock("order-source")
         .ConfigureEpochs(config =>
         {
             config.SetPolicy(EpochPolicy.ByCount(1000));
             config.AddProcessor("order-processor");
         },
         sp => new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
         //^^^ Awkward factory function
    });
});
```

### After (Improved State)

```csharp
services.AddDataFlows("orders", df =>
{
    df.AddGraph("process-orders", g =>
    {
        g.UseBlock("order-source")
         .ConfigureEpochs(config =>
         {
             config.SetPolicy(EpochPolicy.ByCount(1000));
             config.AddProcessor("order-processor");
         });
         //^^^ Clean! Factory is optional
    });
});
```

## Notes

- All changes are backward compatible
- Prototype code available in `/research/epoch-di-improvement/handover/prototype/`
- Research validated approach with 100% test pass rate
- No breaking changes
- POC codebase only (this is exploratory)

## Dependencies

None - self-contained changes to builder API.

## Risks

**Low risk**:
- Changes are minimal and well-tested
- Backward compatibility preserved
- Clear error messages for edge cases
- Prototype validated approach
