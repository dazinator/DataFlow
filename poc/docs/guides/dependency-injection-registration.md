# Dependency Injection Registration Guide

This guide explains how to use the DataFlow DI registration system to register blocks, strategies, and graphs with your dependency injection container.

## Quick Start

```csharp
using DataFlow.POC.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

// Configure services
var services = new ServiceCollection();

services.AddDataFlows("global", df =>
{
    // Register blocks (scoped by default)
    df.AddBlock("producer", sp => new MyProducerBlock());
    df.AddActorBlock<int, string, MyTransformActor>("transformer");
    
    // Register a complete graph
    df.AddGraph("main", g =>
    {
        g.UseBlock("producer")
         .UseBlock("transformer")
         .Connect("producer", "transformer");
    });
});

// Build and resolve
var serviceProvider = services.BuildServiceProvider();
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("global:main");
await graph.ExecuteAsync(cancellationToken);
```

## Block Registration

### Scoped Lifetime (Default and Recommended)

The `AddBlock()` method registers blocks as scoped services by default. This is the **only supported lifetime** for blocks as it provides:
- Isolation between graph executions
- Safe usage with scoped dependencies like `DbContext`
- Prevents issues with concurrent execution and state management

```csharp
services.AddDataFlows("global", df =>
{
    df.AddBlock("myBlock", sp => new MyBlock());
    // Equivalent to:
    df.AddScopedBlock("myBlock", sp => new MyBlock());
});
```

**Why only scoped?** Blocks have stateful execution semantics (they process a specific input stream). Using singleton or transient lifetimes would lead to incorrect behavior:
- **Singleton**: Same block instance would be reused across executions, causing state conflicts
- **Transient**: Creates unnecessary overhead and doesn't match block execution semantics

## Typed Helper Methods

Typed helpers eliminate name duplication and leverage DI auto-injection. They use the `IBlockContext` initialization pattern to set the block name after construction.

### AddActorBlock

For actor-based blocks with automatic DI scope management:

```csharp
services.AddDataFlows("global", df =>
{
    // Clean API - no name duplication
    df.AddActorBlock<int, string, MyActor>("transformer");
    
    // vs. the old way:
    df.AddBlock("transformer", sp => 
        new ActorBlock<int, string, MyActor>(
            "transformer",  // Name duplicated!
            sp.GetRequiredService<IServiceScopeFactory>()
        ));
});
```

**Benefits**:
- No name duplication
- All dependencies auto-injected via DI
- Name guaranteed to match registration key
- Type-safe
- Proper scoped service disposal tracking

## Strategy Registration

Register edge strategies with the same lifetime options as blocks:

```csharp
services.AddDataFlows("global", df =>
{
    // Default scoped
    df.AddStrategy("competing", sp => 
        new CompetingEdgeStrategy(BufferMode.Bounded, 100));
    
    // Singleton
    df.AddSingletonStrategy("broadcast", sp => 
        new BroadcastEdgeStrategy(BufferMode.Bounded, 50));
});
```

## Graph Registration

### Inline Graph Configuration

Register a graph with inline topology configuration:

```csharp
services.AddDataFlows("global", df =>
{
    df.AddBlock("producer", sp => new ProducerBlock());
    df.AddBlock("processor", sp => new ProcessorBlock());
    
    df.AddGraph("pipeline", g =>
    {
        g.UseBlock("producer")
         .UseBlock("processor")
         .Connect("producer", "processor");
    });
});

// Resolve the graph
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("global:pipeline");
```

### Class-Based Graph Definitions

For complex graphs, use a class-based definition:

```csharp
public class MyPipelineDefinition : IDataFlowDefinition
{
    public void Configure(DataFlowGraphBuilder builder)
    {
        builder.UseBlock("producer")
            .UseBlock("transformer")
            .UseBlock("processor")
            .Connect("producer", "transformer")
            .Connect("transformer", "processor");
    }
}

services.AddDataFlows("global", df =>
{
    df.AddBlock("producer", sp => new ProducerBlock());
    df.AddBlock("transformer", sp => new TransformerBlock());
    df.AddBlock("processor", sp => new ProcessorBlock());
    
    df.AddGraphDefinition<MyPipelineDefinition>("pipeline");
});
```

**Benefits**:
- Graph topology is reusable and testable
- Can inject dependencies into the definition class
- Cleaner separation of concerns

## Namespace Support

Namespaces allow multiple modules to register components with the same logical names without conflicts. This is useful for modular monolith architectures.

**All components must be registered with an explicit namespace.** This makes the code more explicit and prevents ambiguity.

### Global Namespace

Use "global" as the namespace for shared components:

```csharp
services.AddDataFlows("global", df =>
{
    df.AddBlock("producer", sp => new ProducerBlock());
    // Registered as "global:producer"
});

var block = serviceProvider.GetKeyedService<IBlock>("global:producer");
```

### Custom Namespaces

Specify a namespace prefix to isolate components:

```csharp
// Module A
services.AddDataFlows("moduleA", df =>
{
    df.AddBlock("producer", sp => new ModuleAProducer());
    df.AddBlock("transformer", sp => new ModuleATransformer());
    
    df.AddGraph("pipeline", g =>
    {
        g.UseBlock("producer")       // Resolves "moduleA:producer"
         .UseBlock("transformer")    // Resolves "moduleA:transformer"
         .Connect("producer", "transformer");
    });
});

// Module B can use same logical names
services.AddDataFlows("moduleB", df =>
{
    df.AddBlock("producer", sp => new ModuleBProducer());
    df.AddBlock("transformer", sp => new ModuleBTransformer());
});

// Resolve specific module's components
var moduleAGraph = serviceProvider.GetKeyedService<DataFlowGraph>("moduleA:pipeline");
var moduleBProducer = serviceProvider.GetKeyedService<IBlock>("moduleB:producer");
```

### Cross-Namespace References

Blocks can reference components from other namespaces using fully-qualified names:

```csharp
// Global shared logger
services.AddDataFlows("global", df =>
{
    df.AddBlock("logger", sp => new LoggerBlock());
});

// Module A uses global logger
services.AddDataFlows("moduleA", df =>
{
    df.AddBlock("producer", sp => new ProducerBlock());
    
    df.AddGraph("pipeline", g =>
    {
        g.UseBlock("producer")              // Resolves "moduleA:producer"
         .UseBlock("global:logger")         // Cross-namespace reference
         .Connect("producer", "global:logger");
    });
});
```

## Graph Resolution and Execution

### Pattern 1: Direct Resolution

```csharp
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("global:main");
await graph.ExecuteAsync(cancellationToken);
```

### Pattern 2: Scoped Resolution

For better control over scoped service lifetime:

```csharp
using (var scope = serviceProvider.CreateScope())
{
    var graph = scope.ServiceProvider.GetKeyedService<DataFlowGraph>("main");
    await graph.ExecuteAsync(cancellationToken);
}
```

### Pattern 3: Dynamic Graphs

You can still build graphs dynamically at runtime using DI-registered blocks:

```csharp
var builder = new DataFlowGraphBuilder("dynamic", serviceProvider);
builder.UseBlock("producer")
    .UseBlock("transformer")
    .Connect("producer", "transformer");
var graph = builder.Build();
```

## Hybrid Approach

You can mix DI-registered blocks with directly added blocks:

```csharp
services.AddDataFlows("global", df =>
{
    df.AddBlock("producer", sp => new ProducerBlock());
});

var serviceProvider = services.BuildServiceProvider();

var builder = new DataFlowGraphBuilder("hybrid", serviceProvider);
var directBlock = new MyCustomBlock();

builder.UseBlock("producer")           // From DI
    .AddBlock(directBlock)              // Direct instance
    .Connect("producer", "custom");
```

## Duplicate Detection

The registration system throws `InvalidOperationException` if you try to register a component with a name that's already been used:

```csharp
services.AddDataFlows("global", df =>
{
    df.AddBlock("test", sp => new Block1());
    df.AddBlock("test", sp => new Block2());  // ❌ Throws!
});

// Error: Block 'global:test' is already registered.
```

This helps catch configuration mistakes early. If you need to override a registration, you must remove the previous registration first (though this is not typically recommended).

## Best Practices

1. **Use scoped lifetime by default** - It's safe for most scenarios and works well with scoped dependencies like `DbContext`.

2. **Use typed helpers when available** - They eliminate name duplication and are more maintainable.

3. **Organize with namespaces** - For modular monolith architectures, use namespaces to avoid naming conflicts.

4. **Register graphs for complex topologies** - Use `AddGraph()` or `AddGraphDefinition<T>()` for graphs that are used repeatedly.

5. **Validate early** - The registration system throws on duplicates and missing dependencies to help you catch issues during startup.

6. **Document namespace conventions** - If using namespaces, document your naming conventions so team members know which namespace to use.

## Migration from Inline Building

If you have existing code that builds graphs inline, you can migrate incrementally:

### Before (Inline)

```csharp
var producer = new ProducerBlock();
var transformer = new TransformerBlock();

var builder = new DataFlowGraphBuilder("pipeline");
builder.AddBlock(producer)
    .AddBlock(transformer)
    .Connect(producer, transformer);
var graph = builder.Build();
```

### After (DI Registration)

```csharp
services.AddDataFlows("global", df =>
{
    df.AddBlock("producer", sp => new ProducerBlock());
    df.AddBlock("transformer", sp => new TransformerBlock());
    
    df.AddGraph("pipeline", g =>
    {
        g.UseBlock("producer")
         .UseBlock("transformer")
         .Connect("producer", "transformer");
    });
});

var graph = serviceProvider.GetKeyedService<DataFlowGraph>("global:pipeline");
```

**Benefits of migrating**:
- Better testability (can mock dependencies)
- Lifetime management handled by DI
- Supports scoped dependencies
- Configuration centralized

## Troubleshooting

### Block not found

```
InvalidOperationException: Block 'global:myblock' not found in the service provider.
```

**Solution**: Ensure the block is registered before building the graph:
```csharp
df.AddBlock("myblock", sp => new MyBlock());
```

### No service provider

```
InvalidOperationException: Cannot use UseBlock() without a service provider.
```

**Solution**: Pass a service provider to the DataFlowGraphBuilder constructor:
```csharp
var builder = new DataFlowGraphBuilder("name", serviceProvider);
```

### Duplicate registration

```
InvalidOperationException: Block 'global:test' is already registered.
```

**Solution**: Check for duplicate names in your registration code. Each block must have a unique name within its namespace.

## See Also

- **API Reference**: `/research/design-revision-2025-11/design/api-spec.md`
- **ADR**: `/poc/docs/adr/2025-11-19-revised-di-service-registration.md`
- **Test Examples**: `RevisedDiRegistrationTests.cs`
