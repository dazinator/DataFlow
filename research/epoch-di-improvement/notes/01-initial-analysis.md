# Initial Analysis - Epoch DI Improvement

**Date**: 2025-11-25  
**Researcher**: Copilot (Research Duty)

## Current Implementation Analysis

### The Problem

The current `ConfigureEpochs` API requires developers to manually provide a factory function for creating the `EpochCoordinator`:

```csharp
builder.ConfigureEpochs(config =>
{
    config.SetPolicy(EpochPolicy.ByCount(1000));
    config.AddProcessor("order-processor");
},
sp => new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
//^^ Awkward - developer must understand EpochCoordinator internals
```

This is problematic because:
1. **Leaky abstraction** - developer must know `EpochCoordinator` constructor signature
2. **Verbose** - requires extra boilerplate on every `ConfigureEpochs` call
3. **Error-prone** - easy to forget or get wrong
4. **Not "batteries included"** - should work out-of-the-box with sensible defaults

### Key Findings

#### 1. DataFlowGraphBuilder Has IServiceProvider

`DataFlowGraphBuilder` already receives an `IServiceProvider` in its constructor:

```csharp
public DataFlowGraphBuilder(
    string name, 
    IServiceProvider serviceProvider,
    IBlockTypeRegistry registry,
    string? namespacePrefix = null,
    ILogger<DataFlowGraph>? logger = null)
{
    _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    // ...
}
```

**Current Access**: `_serviceProvider` is a private field
**Opportunity**: Can be exposed or used internally for default factory

#### 2. EpochCoordinator Constructor

```csharp
public EpochCoordinator(
    IServiceScopeFactory scopeFactory, 
    int operationsQueueCapacity = 100, 
    ICheckpointStrategy? checkpointStrategy = null)
```

Requires:
- `IServiceScopeFactory` - available from `IServiceProvider.GetRequiredService<IServiceScopeFactory>()`
- `operationsQueueCapacity` - has default value (100)
- `checkpointStrategy` - optional, comes from `EpochConfiguration`

#### 3. ConfigureEpochs Implementation

Located in `EpochConfigurationExtensions.cs`:

```csharp
public static DataFlowGraphBuilder ConfigureEpochs(
    this DataFlowGraphBuilder builder,
    Action<EpochConfiguration> configure,
    Func<ICheckpointStrategy?, IEpochCoordinator>? coordinatorFactory = null)
{
    // ...
    if (coordinatorFactory == null)
    {
        throw new ArgumentNullException(nameof(coordinatorFactory),
            "Epoch coordinator factory must be provided...");
    }
    var coordinator = coordinatorFactory(config.CheckpointStrategy);
    // ...
}
```

**Current Behavior**: Throws if factory is null
**Problem**: No default factory provided

#### 4. IEpochCoordinator Implementations

Searched codebase - only one implementation found:
- `EpochCoordinator` - the standard implementation

**Conclusion**: While extensibility is good, we should optimize for the 99% case

## Solution Approaches

### Approach A: Make Factory Optional with Internal Access

**Idea**: Expose `IServiceProvider` internally and use it in extension method

```csharp
// Add internal accessor to DataFlowGraphBuilder
internal IServiceProvider? GetServiceProvider() => _serviceProvider;

// Update ConfigureEpochs
public static DataFlowGraphBuilder ConfigureEpochs(
    this DataFlowGraphBuilder builder,
    Action<EpochConfiguration> configure,
    Func<ICheckpointStrategy?, IEpochCoordinator>? coordinatorFactory = null)
{
    // ... config setup ...
    
    // Default factory if not provided
    if (coordinatorFactory == null)
    {
        var sp = builder.GetServiceProvider();
        if (sp == null)
        {
            throw new InvalidOperationException(
                "Cannot use ConfigureEpochs without a service provider. " +
                "Either pass a service provider to DataFlowGraphBuilder, " +
                "or provide a coordinatorFactory.");
        }
        
        coordinatorFactory = checkpointStrategy =>
        {
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            return new EpochCoordinator(scopeFactory, checkpointStrategy: checkpointStrategy);
        };
    }
    
    var coordinator = coordinatorFactory(config.CheckpointStrategy);
    // ...
}
```

**Pros**:
- ✅ Backward compatible (factory still optional parameter)
- ✅ Works with existing code
- ✅ Clean API for new code
- ✅ Minimal changes

**Cons**:
- ❌ Requires exposing internal accessor
- ❌ Still requires factory for legacy (non-DI) builder

### Approach B: Register EpochCoordinator in DI

**Idea**: Register `IEpochCoordinator` as a service, resolve from DI

```csharp
// In service registration
services.AddTransient<IEpochCoordinator>(sp =>
{
    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
    return new EpochCoordinator(scopeFactory);
});

// In ConfigureEpochs
if (coordinatorFactory == null)
{
    var sp = builder.GetServiceProvider();
    if (sp != null)
    {
        coordinatorFactory = _ => sp.GetRequiredService<IEpochCoordinator>();
    }
}
```

**Pros**:
- ✅ Follows DI best practices
- ✅ Allows injection of custom coordinators
- ✅ Clean separation

**Cons**:
- ❌ Requires service registration changes
- ❌ Coordinator lifetime management unclear (is it per-graph? per-flow?)
- ❌ More complex solution

### Approach C: Overload Method

**Idea**: Create second overload without factory parameter

```csharp
// New overload without factory
public static DataFlowGraphBuilder ConfigureEpochs(
    this DataFlowGraphBuilder builder,
    Action<EpochConfiguration> configure)
{
    return ConfigureEpochs(builder, configure, null);
}

// Original overload with default implementation
public static DataFlowGraphBuilder ConfigureEpochs(
    this DataFlowGraphBuilder builder,
    Action<EpochConfiguration> configure,
    Func<ICheckpointStrategy?, IEpochCoordinator>? coordinatorFactory)
{
    // ... setup config ...
    
    if (coordinatorFactory == null)
    {
        var sp = builder.GetServiceProvider() 
            ?? throw new InvalidOperationException("Service provider required...");
        
        coordinatorFactory = checkpointStrategy =>
        {
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            return new EpochCoordinator(scopeFactory, checkpointStrategy: checkpointStrategy);
        };
    }
    // ...
}
```

**Pros**:
- ✅ Clear API - two distinct methods
- ✅ Backward compatible
- ✅ Easy to understand

**Cons**:
- ❌ Method overload might be confusing
- ❌ Still requires internal accessor

## Recommended Approach

**Approach A (Make Factory Optional)** is recommended because:

1. **Minimal Changes**: Only requires exposing internal accessor and updating one method
2. **Backward Compatible**: Existing code continues to work
3. **Clean API**: New code doesn't need factory
4. **Progressive Enhancement**: Legacy (non-DI) builder still requires factory (appropriate)

## Next Steps

1. ✅ Document findings
2. ⬜ Create prototype implementation
3. ⬜ Test with existing test suite
4. ⬜ Create new test demonstrating improved API
5. ⬜ Document API changes
