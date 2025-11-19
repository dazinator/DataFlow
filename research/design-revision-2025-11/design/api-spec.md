# API Design Specification: Revised DI Service Registration

**Version**: 2.0  
**Date**: 2025-11-19  
**Supersedes**: Version 1.0 (Issue #473)  
**Status**: Validated through prototype

---

## Overview

This specification defines the revised canonical dependency injection service registration API for DataFlow, addressing concerns identified during implementation of the previous design.

**Key Principles**:
- Single enhanced builder (no parallel structures)
- Scoped default lifetime (safe for most scenarios)
- Explicit duplicate detection
- Unified registration API

---

## API Surface

### 1. Service Registration Extension

```csharp
namespace DataFlow.POC.DependencyInjection;

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

### 2. DataFlow Builder

```csharp
namespace DataFlow.POC.DependencyInjection;

public class DataFlowBuilder
{
    // Block Registration (Scoped Default)
    public DataFlowBuilder AddBlock<TBlock>(
        string name, 
        Func<IServiceProvider, TBlock> factory) where TBlock : IBlock;
    
    public DataFlowBuilder AddBlock(string name, IBlock block);
    
    // Explicit Lifetime Methods
    public DataFlowBuilder AddScopedBlock<TBlock>(
        string name, 
        Func<IServiceProvider, TBlock> factory) where TBlock : IBlock;
    
    public DataFlowBuilder AddSingletonBlock<TBlock>(
        string name, 
        Func<IServiceProvider, TBlock> factory) where TBlock : IBlock;
    
    public DataFlowBuilder AddTransientBlock<TBlock>(
        string name, 
        Func<IServiceProvider, TBlock> factory) where TBlock : IBlock;
    
    // Strategy Registration (Scoped Default)
    public DataFlowBuilder AddStrategy<TStrategy>(
        string name, 
        Func<IServiceProvider, TStrategy> factory) where TStrategy : EdgeStrategy;
    
    public DataFlowBuilder AddStrategy(string name, EdgeStrategy strategy);
    
    public DataFlowBuilder AddScopedStrategy<TStrategy>(
        string name, 
        Func<IServiceProvider, TStrategy> factory) where TStrategy : EdgeStrategy;
    
    public DataFlowBuilder AddSingletonStrategy<TStrategy>(
        string name, 
        Func<IServiceProvider, TStrategy> factory) where TStrategy : EdgeStrategy;
    
    // Graph Registration
    public DataFlowBuilder AddGraph(
        string name, 
        Action<DataFlowGraphBuilder> configure);
    
    public DataFlowBuilder AddGraphDefinition<TDefinition>(string name) 
        where TDefinition : class, IDataFlowDefinition;
}
```

### 3. Enhanced Graph Builder

```csharp
namespace DataFlow.POC.Builder;

public class DataFlowGraphBuilder
{
    // Constructors
    public DataFlowGraphBuilder(
        string name, 
        ILogger<DataFlowGraph>? logger = null);  // Original
    
    public DataFlowGraphBuilder(
        string name, 
        IServiceProvider? serviceProvider,  // NEW: DI support
        ILogger<DataFlowGraph>? logger = null);
    
    // Block Methods
    public DataFlowGraphBuilder AddBlock(IBlock block);  // Unchanged
    public DataFlowGraphBuilder UseBlock(string name);   // NEW: Resolve from DI
    
    // ... all other existing methods unchanged ...
}
```

### 4. Graph Definition Interface

```csharp
namespace DataFlow.POC.DependencyInjection;

public interface IDataFlowDefinition
{
    void Configure(DataFlowGraphBuilder builder);
}
```

---

## Usage Patterns

### Basic Registration

```csharp
services.AddDataFlows(df => 
{
    df.AddBlock("producer", sp => new ProducerBlock<int>(...));
    df.AddBlock("transformer", sp => new ActorBlock<int, string, MyActor>(...));
});
```

### Explicit Lifetimes

```csharp
services.AddDataFlows(df => 
{
    // Scoped (default) - safe for DbContext, etc.
    df.AddBlock("scoped", sp => new ScopedBlock(...));
    df.AddScopedBlock("explicit-scoped", sp => new ScopedBlock(...));
    
    // Singleton - for stateless blocks
    df.AddSingletonBlock("singleton", sp => new StatelessBlock(...));
    
    // Transient - new instance each time
    df.AddTransientBlock("transient", sp => new TransientBlock(...));
});
```

### Integrated Graph Registration

```csharp
services.AddDataFlows(df => 
{
    // Register blocks
    df.AddBlock("producer", sp => new ProducerBlock<int>(...));
    df.AddBlock("transformer", sp => new TransformBlock<int, string>(...));
    
    // Register graph with topology
    df.AddGraph("main", g =>
    {
        g.UseBlock("producer")
         .UseBlock("transformer")
         .Connect("producer", "transformer");
    });
});

// Resolve graph
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("main");
```

### Class-Based Graph Definition

```csharp
public class MyGraphDefinition : IDataFlowDefinition
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

// Registration
services.AddDataFlows(df => 
{
    df.AddBlock("producer", ...);
    df.AddBlock("transformer", ...);
    df.AddBlock("processor", ...);
    
    df.AddGraphDefinition<MyGraphDefinition>("main");
});
```

### Dynamic Runtime Graphs

```csharp
// Still supported for runtime configuration
var builder = new DataFlowGraphBuilder("dynamic", serviceProvider);
builder.UseBlock("producer")
    .UseBlock(GetRuntimeTransformer())
    .Connect(...);
var graph = builder.Build();
```

### Hybrid Approach

```csharp
var builder = new DataFlowGraphBuilder("hybrid", serviceProvider);
builder.UseBlock("producer")        // From DI
    .AddBlock(new MyBlock())         // Direct instance
    .UseBlock("transformer")         // From DI
    .Connect("producer", "MyBlock")
    .Connect("MyBlock", "transformer");
```

---

## Runtime Resolution and Execution

### Resolving Registered Graphs

Graphs registered via `AddGraph()` or `AddGraphDefinition<T>()` are stored in the DI container as keyed services and can be resolved at runtime:

```csharp
// Registration
services.AddDataFlows(df => 
{
    df.AddBlock("producer", sp => new ProducerBlock<int>(...));
    df.AddBlock("transformer", sp => new TransformBlock<int, string>(...));
    
    df.AddGraph("main", g =>
    {
        g.UseBlock("producer")
         .UseBlock("transformer")
         .Connect("producer", "transformer");
    });
});

// Runtime Resolution
var serviceProvider = services.BuildServiceProvider();
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("main");

if (graph != null)
{
    // Execute the graph
    await graph.ExecuteAsync(cancellationToken);
}
```

### Resolving Namespace-Prefixed Graphs

When using namespace prefixes, include the full key when resolving:

```csharp
// Registration with namespace
services.AddDataFlows("moduleA", df => 
{
    df.AddBlock("producer", sp => new ProducerBlock<int>(...));
    df.AddGraph("main", g => g.UseBlock("producer"));
});

// Resolution - use full key
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("moduleA:main");
```

### Resolution Patterns

**Pattern 1: Direct Resolution**
```csharp
// Simple resolution in application startup or controller
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("main");
await graph.ExecuteAsync(cancellationToken);
```

**Pattern 2: Scoped Resolution**
```csharp
// Resolution within a scope (e.g., per-request)
using (var scope = serviceProvider.CreateScope())
{
    var graph = scope.ServiceProvider.GetKeyedService<DataFlowGraph>("main");
    await graph.ExecuteAsync(cancellationToken);
}
// Scoped blocks are disposed after scope ends
```

**Pattern 3: Factory Pattern**
```csharp
// Create a factory service for graph access
public class DataFlowGraphFactory
{
    private readonly IServiceProvider _serviceProvider;
    
    public DataFlowGraphFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public DataFlowGraph GetGraph(string name)
    {
        return _serviceProvider.GetKeyedService<DataFlowGraph>(name)
            ?? throw new InvalidOperationException($"Graph '{name}' not found");
    }
    
    public DataFlowGraph GetNamespacedGraph(string ns, string name)
    {
        var fullKey = $"{ns}:{name}";
        return _serviceProvider.GetKeyedService<DataFlowGraph>(fullKey)
            ?? throw new InvalidOperationException($"Graph '{fullKey}' not found");
    }
}

// Registration
services.AddSingleton<DataFlowGraphFactory>();

// Usage
public class MyService
{
    private readonly DataFlowGraphFactory _graphFactory;
    
    public MyService(DataFlowGraphFactory graphFactory)
    {
        _graphFactory = graphFactory;
    }
    
    public async Task ProcessAsync(CancellationToken ct)
    {
        var graph = _graphFactory.GetGraph("main");
        await graph.ExecuteAsync(ct);
    }
}
```

**Pattern 4: Dependency Injection**
```csharp
// Inject graph directly (requires factory registration)
services.AddScoped(sp => 
    sp.GetKeyedService<DataFlowGraph>("main") 
    ?? throw new InvalidOperationException("Main graph not found"));

// Usage
public class MyService
{
    private readonly DataFlowGraph _graph;
    
    public MyService(DataFlowGraph graph)
    {
        _graph = graph;
    }
    
    public async Task ProcessAsync(CancellationToken ct)
    {
        await _graph.ExecuteAsync(ct);
    }
}
```

### Execution Considerations

**Lifetime Management**:
- Graphs registered with `AddGraph()` are scoped by default
- Scoped blocks within the graph are created per scope
- Singleton blocks are shared across all graph executions
- Dispose scopes properly to release scoped resources

**Concurrency**:
- Multiple scopes can execute different instances of the same graph simultaneously
- Singleton blocks must be thread-safe
- Scoped blocks are isolated per scope

**Error Handling**:
```csharp
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("main");
if (graph == null)
{
    throw new InvalidOperationException(
        "Graph 'main' not found. Ensure it was registered with AddGraph().");
}

try
{
    await graph.ExecuteAsync(cancellationToken);
}
catch (OperationCanceledException)
{
    // Handle cancellation
}
catch (Exception ex)
{
    // Handle execution errors
    _logger.LogError(ex, "Graph execution failed");
    throw;
}
```

---

## Behavior Specifications

### Default Lifetimes

| Method | Lifetime | Use Case |
|--------|----------|----------|
| `AddBlock()` | Scoped | Default - safe for most cases |
| `AddScopedBlock()` | Scoped | Explicit scoped |
| `AddSingletonBlock()` | Singleton | Stateless blocks |
| `AddTransientBlock()` | Transient | Per-request creation |

### Duplicate Registration

**Behavior**: Throws `InvalidOperationException` when duplicate name is registered.

**Error Message**:
```
Block 'name' is already registered. Each block, strategy, and graph must have 
a unique name. If you intended to override the registration, remove the 
previous registration first.
```

**Applies To**:
- Blocks (all lifetime methods)
- Strategies (all lifetime methods)
- Graphs (AddGraph, AddGraphDefinition)

### Name Resolution

**UseBlock() Behavior**:
- Requires service provider in constructor
- Throws if service provider not provided
- Throws if block name not found in DI
- Returns resolved block instance

**Error Messages**:
```
// No service provider
"Cannot use UseBlock() without a service provider. Pass an IServiceProvider 
to the constructor, or use AddBlock() with a block instance instead."

// Block not found
"Block 'name' not found in service collection. Make sure it was registered 
using services.AddDataFlows(df => df.AddBlock(...))."
```

---

## Migration from Previous Design

### Breaking Changes

**Lifetime Default**:
- **Before**: Singleton
- **After**: Scoped
- **Migration**: Use `AddSingletonBlock()` to preserve singleton behavior

**Builder Class**:
- **Before**: Use `DataFlowGraphBuilderEx` for DI
- **After**: Use `DataFlowGraphBuilder` (same class, enhanced)
- **Migration**: Change class name, remove "Ex"

### Compatible Changes

- `AddDataFlows()` method signature unchanged
- `AddBlock()` method signature unchanged (only default lifetime changed)
- Block registration patterns unchanged
- Direct block usage (no DI) unchanged

### Migration Example

**Before**:
```csharp
services.AddDataFlows(df => {
    df.AddBlock("producer", sp => new ProducerBlock<int>(...)); // Singleton
});

var builder = new DataFlowGraphBuilderEx("flow", serviceProvider);
builder.UseBlock("producer").Connect(...);
```

**After**:
```csharp
services.AddDataFlows(df => {
    df.AddSingletonBlock("producer", sp => new ProducerBlock<int>(...)); // Explicit
    // Or just AddBlock for scoped (recommended)
    
    df.AddGraph("flow", g => {
        g.UseBlock("producer").Connect(...);
    });
});

var builder = new DataFlowGraphBuilder("flow", serviceProvider);
builder.UseBlock("producer").Connect(...);
```

---

## Validation

**Test Coverage**: 29/29 tests passing

**Test Categories**:
1. Single builder pattern
2. Lifetime scopes
3. Duplicate detection
4. Graph integration
5. Namespace support
6. Backward compatibility
7. Error handling

**Reference**: `/poc/DataFlow.POC.Tests/RevisedDiDesignTests.cs`

---

## References

- **Research**: `/research/design-revision-2025-11/README.md`
- **ADR**: `/poc/docs/adr/2025-11-19-revised-di-service-registration.md`
- **Previous Design**: `/research/di-service-registration/` (Issue #473)
- **Prototype**: `/poc/DataFlow.POC/DependencyInjection/` (to be reverted)
