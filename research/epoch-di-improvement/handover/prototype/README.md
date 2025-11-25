# Prototype Code

This directory contains the working prototype code that validates the improved `ConfigureEpochs` API.

## Purpose

This prototype demonstrates that the factory parameter can be made optional, providing a cleaner API while maintaining full backward compatibility.

## Key Changes

### 1. DataFlowGraphBuilder.cs

**Added internal accessor for service provider**:
```csharp
/// <summary>
/// Gets the service provider for DI resolution.
/// Used internally by extension methods to resolve dependencies.
/// </summary>
internal IServiceProvider? GetServiceProvider() => _serviceProvider;
```

**Location**: After constructors, before `AddBlock` method

### 2. EpochConfigurationExtensions.cs

**Made factory parameter optional**:
- Parameter changed to nullable: `Func<ICheckpointStrategy?, IEpochCoordinator>?`
- Added default value: `= null`

**Added default factory creation logic**:
```csharp
if (coordinatorFactory == null)
{
    var serviceProvider = builder.GetServiceProvider();
    if (serviceProvider == null)
    {
        throw new InvalidOperationException(
            "Cannot use ConfigureEpochs without a coordinatorFactory...");
    }
    
    coordinatorFactory = checkpointStrategy =>
    {
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        return new EpochCoordinator(scopeFactory, checkpointStrategy: checkpointStrategy);
    };
}
```

**Added using directive**:
```csharp
using Microsoft.Extensions.DependencyInjection;
```

### 3. EpochGraphIntegrationTests.cs (Test Updates)

**Replaced test**: `ConfigureEpochs_RequiresCoordinatorFactory`

**With two new tests**:
1. `ConfigureEpochs_WorksWithoutFactoryWhenServiceProviderAvailable` - Tests new default behavior
2. `ConfigureEpochs_ThrowsWhenNoServiceProviderAndNoFactory` - Tests legacy behavior

### 4. EpochConfigurationApiDemoTests.cs (New File)

**Demonstration tests**:
- `ImprovedApi_ConfigureEpochs_WithoutFactory` - Shows clean API
- `BackwardCompatibility_ConfigureEpochs_WithExplicitFactory` - Shows backward compatibility
- `Comparison_BeforeAndAfter` - Side-by-side comparison

## Testing

All tests pass:
- ✅ 15/15 tests in `EpochGraphIntegrationTests`
- ✅ 3/3 tests in `EpochConfigurationApiDemoTests`
- ✅ No regressions in other epoch tests

## Usage Examples

### Clean API (New)

```csharp
var services = new ServiceCollection();
var serviceProvider = services.BuildServiceProvider();
var registry = new BlockTypeRegistry();
var builder = new DataFlowGraphBuilder("demo", serviceProvider, registry);

builder.ConfigureEpochs(config =>
{
    config.SetPolicy(EpochPolicy.ByCount(100));
    config.AddProcessor("processor1");
    config.OnBeginEpoch(async (epoch, ct) => { /* tx */ });
    config.OnCommitEpoch(async (epoch, ct) => { /* commit */ });
});
```

### Backward Compatible (Explicit Factory Still Works)

```csharp
builder.ConfigureEpochs(
    config =>
    {
        config.SetPolicy(EpochPolicy.ByCount(100));
        config.AddProcessor("processor1");
    },
    checkpointStrategy =>
    {
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        return new EpochCoordinator(scopeFactory, operationsQueueCapacity: 200);
    });
```

## Implementation Notes

1. **Backward Compatibility**: All existing code continues to work without changes
2. **Progressive Enhancement**: New code benefits from cleaner API
3. **Error Handling**: Clear error messages when service provider not available
4. **No Performance Impact**: Same execution path, just less boilerplate

## Files Included

- `DataFlowGraphBuilder.cs` - Builder with internal service provider accessor
- `EpochConfigurationExtensions.cs` - Updated ConfigureEpochs with default factory
- `EpochGraphIntegrationTests.cs` - Updated integration tests
- `EpochConfigurationApiDemoTests.cs` - New demonstration tests showing clean API

## Next Steps for Implementation

1. Review prototype code
2. Apply changes to production codebase
3. Update documentation with new API examples
4. Add migration notes for developers
