# API Design Specification: DI Service Registration

**Version**: 1.0  
**Date**: 2025-11-17  
**Status**: Validated through prototype

---

## Overview

This document specifies the API design for canonical dependency injection service registration in DataFlow, validated through research prototyping.

---

## API Surface

### 1. Service Registration Extension

**Namespace**: `DataFlow.POC.DependencyInjection`

```csharp
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add DataFlow components to the service collection.
    /// </summary>
    public static IServiceCollection AddDataFlows(
        this IServiceCollection services,
        Action<DataFlowBuilder> configure);
}
```

**Usage**:
```csharp
services.AddDataFlows(df => 
{
    df.AddBlock("my-producer", sp => new ProducerBlock<int>(...));
    df.AddStrategy("competing", sp => new CompetingEdgeStrategy(...));
});
```

---

### 2. DataFlow Builder

**Namespace**: `DataFlow.POC.DependencyInjection`

```csharp
public class DataFlowBuilder
{
    // Block registration with factory
    public DataFlowBuilder AddBlock<TBlock>(
        string name, 
        Func<IServiceProvider, TBlock> factory)
        where TBlock : IBlock;
    
    // Block registration with instance
    public DataFlowBuilder AddBlock(
        string name, 
        IBlock block);
    
    // Strategy registration with factory
    public DataFlowBuilder AddStrategy<TStrategy>(
        string name, 
        Func<IServiceProvider, TStrategy> factory)
        where TStrategy : EdgeStrategy;
    
    // Strategy registration with instance
    public DataFlowBuilder AddStrategy(
        string name, 
        EdgeStrategy strategy);
}
```

**Characteristics**:
- Fluent API (returns `this` for chaining)
- Validates name is not null/whitespace
- Registers as keyed singletons
- Type constraints ensure valid registrations

---

### 3. Extended Graph Builder

**Namespace**: `DataFlow.POC.Builder`

```csharp
public class DataFlowGraphBuilderEx
{
    // Constructor with optional service provider
    public DataFlowGraphBuilderEx(
        string name, 
        IServiceProvider? serviceProvider = null,
        ILogger<DataFlowGraph>? logger = null);
    
    // Use block registered in DI
    public DataFlowGraphBuilderEx UseBlock(string name);
    
    // Traditional: Add block instance
    public DataFlowGraphBuilderEx AddBlock(IBlock block);
    
    // All other methods from DataFlowGraphBuilder
    // (Connect, ConnectMany, ConnectCompeting, etc.)
}
```

**Key Features**:
- Extends original builder functionality
- Optional service provider (backward compatible)
- `.UseBlock(name)` resolves from DI
- `.AddBlock(instance)` works as before
- Can mix both approaches in same graph

---

## Registration Patterns

### Pattern 1: Simple Block Registration

```csharp
services.AddDataFlows(df => 
{
    df.AddBlock("producer", sp => 
        new ProducerBlock<int>("producer", _ => GenerateData()));
});
```

### Pattern 2: Block with Dependencies

```csharp
services.AddDataFlows(df => 
{
    df.AddBlock("processor", sp => 
    {
        var logger = sp.GetRequiredService<ILogger<MyBlock>>();
        var config = sp.GetRequiredService<IConfiguration>();
        return new MyBlock("processor", logger, config);
    });
});
```

### Pattern 3: Actor Block with Scoped Dependencies

```csharp
// Register actor
services.AddScoped<MyActor>();

// Register block that uses actor
services.AddDataFlows(df => 
{
    df.AddBlock("actor-block", sp =>
    {
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new ActorBlock<int, string, MyActor>("actor-block", scopeFactory);
    });
});
```

### Pattern 4: Strategy Registration

```csharp
services.AddDataFlows(df => 
{
    df.AddStrategy("competing", sp => 
        new CompetingEdgeStrategy(BufferMode.Bounded, 100));
    
    df.AddStrategy("broadcast", sp => 
        new BroadcastEdgeStrategy(BufferMode.Bounded, 100));
});
```

---

## Graph Building Patterns

### Pattern 1: Full DI Approach

```csharp
// Registration
services.AddDataFlows(df => 
{
    df.AddBlock("source", sp => ...);
    df.AddBlock("transform", sp => ...);
    df.AddBlock("sink", sp => ...);
});

// Graph building
var builder = new DataFlowGraphBuilderEx("my-flow", serviceProvider);
builder.UseBlock("source")
    .UseBlock("transform")
    .UseBlock("sink")
    .Connect("source", "transform")
    .Connect("transform", "sink");

var graph = builder.Build();
```

### Pattern 2: Hybrid Approach

```csharp
// Partial DI registration
services.AddDataFlows(df => 
{
    df.AddBlock("transform", sp => ...);
});

// Graph building - mix DI and inline
var builder = new DataFlowGraphBuilderEx("my-flow", serviceProvider);
builder.AddBlock(new ProducerBlock<int>(...))  // Inline
    .UseBlock("transform")                      // From DI
    .AddBlock(new ActorBlock<...>(...))        // Inline
    .Connect("source", "transform")
    .Connect("transform", "sink");
```

### Pattern 3: Traditional (No DI)

```csharp
// No DI registration needed

// Graph building - all inline
var builder = new DataFlowGraphBuilderEx("my-flow");
builder.AddBlock(new ProducerBlock<int>(...))
    .AddBlock(new ActorBlock<...>(...))
    .Connect("source", "sink");
```

---

## Error Handling

### Missing Block

```csharp
var builder = new DataFlowGraphBuilderEx("flow", serviceProvider);
builder.UseBlock("non-existent");  // Throws InvalidOperationException

// Exception message:
// "Block 'non-existent' not found in service collection. 
//  Make sure it was registered using AddDataFlows()."
```

### Missing Service Provider

```csharp
var builder = new DataFlowGraphBuilderEx("flow");  // No service provider
builder.UseBlock("some-block");  // Throws InvalidOperationException

// Exception message:
// "Cannot use UseBlock() without a service provider. 
//  Pass an IServiceProvider to the constructor or use AddBlock() instead."
```

### Invalid Name

```csharp
services.AddDataFlows(df => 
{
    df.AddBlock("", sp => ...);  // Throws ArgumentException
    df.AddBlock(null, sp => ...);  // Throws ArgumentException
});

// Exception message: "Block name cannot be null or whitespace"
```

---

## Type Constraints

### Block Registration

```csharp
// ✅ Valid - implements IBlock
df.AddBlock("ok", sp => new ProducerBlock<int>(...));

// ❌ Compile error - doesn't implement IBlock
df.AddBlock("bad", sp => new MyClass());
```

### Strategy Registration

```csharp
// ✅ Valid - inherits from EdgeStrategy
df.AddStrategy("ok", sp => new CompetingEdgeStrategy(...));

// ❌ Compile error - doesn't inherit EdgeStrategy
df.AddStrategy("bad", sp => new MyClass());
```

---

## Lifetime Management

### Singleton Blocks

All blocks registered via `AddDataFlows()` are singletons:

```csharp
var block1 = serviceProvider.GetKeyedService<IBlock>("my-block");
var block2 = serviceProvider.GetKeyedService<IBlock>("my-block");

Assert.Same(block1, block2);  // ✅ Same instance
```

**Implications**:
- Blocks must be thread-safe (already a DataFlow requirement)
- Efficient resource usage (one instance shared)
- State should be in execution context, not block instance

### Scoped Dependencies

Blocks can use scoped dependencies via `IServiceScopeFactory`:

```csharp
services.AddScoped<MyRepository>();

services.AddDataFlows(df => 
{
    df.AddBlock("processor", sp =>
    {
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new ActorBlock<int, string, MyActor>("processor", scopeFactory);
    });
});
```

**How it works**:
- Block itself is singleton
- ActorBlock creates new scope per actor instance
- Scoped services resolved fresh for each actor

---

## Integration Points

### With Existing DataFlow

```csharp
// Blocks registered via DI
var block = serviceProvider.GetKeyedService<IBlock>("my-block");

// Can be used with original DataFlowGraphBuilder
var builder = new DataFlowGraphBuilder("flow");
builder.AddBlock(block);  // Works!
```

### With Testing

```csharp
[Fact]
public async Task TestWithMockBlock()
{
    var services = new ServiceCollection();
    services.AddDataFlows(df => 
    {
        df.AddBlock("producer", sp => new MockProducerBlock());
    });
    
    var sp = services.BuildServiceProvider();
    var builder = new DataFlowGraphBuilderEx("test", sp);
    builder.UseBlock("producer");
    // ... rest of test
}
```

---

## Design Principles

### 1. Follow .NET Conventions
- `AddXyz()` pattern for service registration
- Fluent builder pattern
- Extension methods on `IServiceCollection`

### 2. Leverage Framework Features
- Keyed services (built-in .NET 8 feature)
- Standard dependency injection
- Service lifetime management

### 3. Maintain Compatibility
- Original `DataFlowGraphBuilder` unchanged
- New `DataFlowGraphBuilderEx` extends functionality
- Both approaches work together

### 4. Clear Intent
- Registration separate from topology
- Named blocks for clarity
- Type constraints prevent errors

### 5. Progressive Enhancement
- Start simple (inline blocks)
- Add DI when needed (selected blocks)
- Full DI for complex scenarios

---

## Future Enhancements

### 1. Configuration Validation

```csharp
services.AddDataFlows(df => 
{
    df.AddBlock("producer", ...);
    df.AddBlock("consumer", ...);
})
.ValidateOnBuild();  // Future: validate all blocks registered
```

### 2. Named Constants

```csharp
public static class BlockNames
{
    public const string Producer = "producer";
    public const string Transformer = "transformer";
}

// Usage
df.AddBlock(BlockNames.Producer, ...);
builder.UseBlock(BlockNames.Producer);
```

### 3. Factory Helpers

```csharp
df.AddBlockFactory<MyBlock>("my-block", (sp, config) => 
{
    // Helper that automatically resolves common dependencies
    return new MyBlock(sp.GetLogger<MyBlock>(), config);
});
```

### 4. Source Generators

```csharp
// Future: compile-time safe names
[DataFlowBlock("producer")]
partial class MyProducer : ProducerBlock<int> { }

// Generated:
public static class BlockNames { public const string MyProducer = "producer"; }
```

---

## Summary

This API design provides:
- ✅ Canonical .NET DI registration pattern
- ✅ Flexible (DI, inline, or hybrid)
- ✅ Type-safe constraints
- ✅ Clear error messages
- ✅ Backward compatible
- ✅ Future-proof extensibility

The design balances developer experience, type safety, and flexibility while maintaining compatibility with existing DataFlow code.
