# Exploration Notes: DI Service Registration API Design

## Date: 2025-11-17

## Update: Extended Scope (2025-11-17)

Based on user feedback, research expanded to include:
1. **Class-based DataFlow definitions** - Separate Register and Configure methods

## .NET DI Pattern Analysis

### Common Patterns in .NET Ecosystem

**Pattern 1: AddXyz() - Simple Registration**
```csharp
services.AddDbContext<MyContext>();
services.AddHttpClient();
services.AddSingleton<IMyService, MyService>();
```
- ✅ Simple, direct
- ✅ Type-safe
- ❌ Doesn't support complex configuration

**Pattern 2: AddXyz(Action<Options>) - Configuration**
```csharp
services.AddAuthentication(options => {
    options.DefaultScheme = "Cookies";
});

services.AddDbContext<MyContext>(options => {
    options.UseSqlServer(connectionString);
});
```
- ✅ Supports configuration
- ✅ Type-safe options
- ✅ Fluent and readable

**Pattern 3: AddXyz().WithYzw() - Builder Chain**
```csharp
services.AddHealthChecks()
    .AddCheck<MyHealthCheck>("my-check")
    .AddDbContextCheck<MyContext>();
```
- ✅ Fluent chaining
- ✅ Multiple components
- ✅ Discoverable

**Pattern 4: Named/Keyed Services (.NET 8+)**
```csharp
services.AddKeyedSingleton<ICache, RedisCache>("redis");
services.AddKeyedSingleton<ICache, MemoryCache>("memory");

// Resolution
var cache = serviceProvider.GetRequiredKeyedService<ICache>("redis");
```
- ✅ Multiple implementations by name
- ✅ Type-safe at resolution
- ⚠️ String keys (not compile-time safe)

## API Design Alternatives for DataFlow

### Alternative 1: Fluent Builder with Keyed Services
```csharp
services.AddDataFlows(flows => 
{
    flows.AddBlock<ProducerBlock<int>>("producer", sp => new ProducerBlock<int>(...));
    flows.AddBlock<TransformBlock<int, string>>("transform", sp => new TransformBlock<int, string>(...));
    flows.AddStrategy<CompetingEdgeStrategy>("competing", sp => new CompetingEdgeStrategy(...));
});

// Usage in graph builder
var builder = graphBuilderFactory.Create("my-flow");
builder.UseBlock("producer")
    .UseBlock("transform")
    .Connect("producer", "transform", "competing");
```

**Pros:**
- ✅ Clean separation of registration and usage
- ✅ Follows .NET convention
- ✅ Centralized configuration

**Cons:**
- ❌ String keys not type-safe at compile time
- ❌ Graph builder needs service provider reference
- ❌ Possible duplicate names

### Alternative 2: Type-Based Registration
```csharp
services.AddDataFlows(flows => 
{
    flows.AddBlock<MyProducer>();
    flows.AddBlock<MyTransformer>();
    flows.AddStrategy<CompetingEdgeStrategy>();
});

// Usage - resolve by type
var builder = graphBuilderFactory.Create("my-flow");
builder.UseBlock<MyProducer>()
    .UseBlock<MyTransformer>()
    .Connect<MyProducer, MyTransformer, CompetingEdgeStrategy>();
```

**Pros:**
- ✅ Fully type-safe
- ✅ No string keys
- ✅ Refactoring friendly

**Cons:**
- ❌ Only one instance per type
- ❌ Can't have multiple producers of same type
- ❌ Less flexible

### Alternative 3: Hybrid - Named Types
```csharp
services.AddDataFlows(flows => 
{
    flows.AddBlock("producer", sp => sp.GetRequiredService<MyProducer>());
    flows.AddBlock("backup-producer", sp => sp.GetRequiredService<MyProducer>());
    flows.AddBlock("transform", sp => sp.GetRequiredService<MyTransformer>());
});

// Blocks themselves registered normally
services.AddTransient<MyProducer>();
services.AddTransient<MyTransformer>();

// Usage
builder.UseBlock("producer")
    .UseBlock("transform");
```

**Pros:**
- ✅ Flexible - multiple instances of same type
- ✅ Blocks get DI benefits
- ✅ Names optional - can still use types

**Cons:**
- ⚠️ Two-step registration (blocks + dataflows)
- ⚠️ More verbose

### Alternative 4: Descriptor Pattern (Like MediatR)
```csharp
// Define blocks as descriptors
public class ProducerDescriptor : IBlockDescriptor<ProducerBlock<int>>
{
    public string Name => "producer";
    public ProducerBlock<int> Create(IServiceProvider sp) => new ProducerBlock<int>(...);
}

// Register
services.AddDataFlows(flows => 
{
    flows.AddDescriptor<ProducerDescriptor>();
    flows.AddDescriptor<TransformerDescriptor>();
});

// Usage
builder.UseBlock<ProducerDescriptor>()
    .UseBlock<TransformerDescriptor>();
```

**Pros:**
- ✅ Type-safe
- ✅ Named
- ✅ Clean separation

**Cons:**
- ❌ More boilerplate
- ❌ Extra classes for each block
- ❌ Overkill for simple scenarios

## Initial Assessment

**Most Promising: Alternative 1 (Fluent Builder with Keyed Services)**

Why:
1. Balances simplicity and flexibility
2. Follows standard .NET patterns (similar to AddHealthChecks)
3. Leverages .NET 8 keyed services feature
4. Allows multiple instances of same type
5. Clear intent in registration

**Trade-offs to Accept:**
- String keys are not compile-time safe (but this is standard in .NET for keyed services)
- Runtime validation needed for missing keys
- Graph builder needs service provider access

## Next Steps

1. ✅ Build minimal prototype of Alternative 1
2. Test with realistic scenarios from existing POC tests
3. Measure performance impact of keyed service resolution
4. Validate backward compatibility story
5. Consider Alternative 3 as fallback if Alternative 1 has issues

## Open Questions

1. Should blocks/strategies auto-register themselves, or require explicit registration?
2. How to handle block dependencies (other services they need)?
3. Should graph builder validate all referenced names exist at build time or runtime?
4. What happens if someone calls UseBlock() with unregistered name?

---

## Extended Features: Class-Based DataFlows

### Feature: Class-Based DataFlow Definition

**Pattern**: Separate class implementing `IDataFlowDefinition` interface

```csharp
public interface IDataFlowDefinition
{
    void RegisterServices(DataFlowBuilder builder);
    void ConfigureGraph(DataFlowGraphBuilderEx builder);
}

// Usage
services.AddDataFlowDefinition<MyDataFlowDefinition>("my-flow");

public class MyDataFlowDefinition : IDataFlowDefinition
{
    public void RegisterServices(DataFlowBuilder builder)
    {
        builder.AddBlock("producer", sp => ...);
        builder.AddBlock("transformer", sp => ...);
    }
    
    public void ConfigureGraph(DataFlowGraphBuilderEx builder)
    {
        builder.UseBlock("producer")
            .UseBlock("transformer")
            .Connect("producer", "transformer");
    }
}
```

**Pros**:
- ✅ Clean separation of concerns (registration vs graph structure)
- ✅ Reusable across applications
- ✅ Testable in isolation
- ✅ Type-safe class references
- ✅ Can use constructor injection for configuration

**Cons**:
- ⚠️ More files (one class per DataFlow)
- ⚠️ Slightly more verbose than inline

**When to Use**:
- Complex DataFlows with many blocks
- Reusable DataFlow patterns
- When DataFlow needs to be tested independently
- When DataFlow configuration is complex

### Note on Service Isolation

For scenarios requiring service isolation (e.g., multi-tenancy, different service lifetimes per DataFlow):

**Recommended Approach**: The host application can create its own `IServiceCollection`, use the canonical registration APIs (class-based or lambda), and build a separate service provider:

```csharp
// Create isolated service collection
var isolatedServices = new ServiceCollection();

// Use canonical registration
isolatedServices.AddDataFlows(df => 
{
    df.AddBlock("producer", sp => new ProducerBlock<int>(...));
});

// Or use class-based
isolatedServices.AddDataFlowDefinition<MyDataFlowDefinition>("my-flow");

// Build isolated service provider
var isolatedServiceProvider = isolatedServices.BuildServiceProvider();

// Use with graph builder
var builder = new DataFlowGraphBuilderEx("my-flow", isolatedServiceProvider);
```

This approach provides complete control without requiring additional API surface area in the DataFlow library.

## Validation Results

**Class-Based Definition**: ✅ Validated
- Test: `ClassBased_DataFlowDefinition_Should_Work`
- Demonstrates clean separation
- Interface implementation verified

**Hybrid Scenarios**: ✅ Validated
- Can mix class-based and inline
- Full backward compatibility

## API Surface Summary

### Original Features
1. `services.AddDataFlows(df => ...)` - Inline registration
2. `DataFlowBuilder` - Fluent builder for registration
3. `DataFlowGraphBuilderEx` - Extended graph builder with `.UseBlock()`

### New Features
4. `services.AddDataFlowDefinition<T>(name)` - Class-based registration
5. `IDataFlowDefinition` interface - Class-based contract

### Total Test Coverage
- Original: 7/7 tests passing
- New: 1/1 tests passing
- **Combined: 8/8 tests passing** ✅

