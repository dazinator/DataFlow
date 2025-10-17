# Keyed DataFlow Registration

This document describes how to use the keyed dataflow registration pattern introduced in this library.

## Overview

The keyed dataflow registration pattern allows you to:
- Register named dataflows with dependency injection using keyed services
- Leverage DI lifetime management (Transient, Scoped, Singleton)
- Access the graph for inspection (e.g., generating Mermaid diagrams) from the dataflow instance
- Safely manage dataflow instances according to your application's needs

## Basic Usage

### Registering a Named DataFlow

```csharp
services.AddKeyedDataFlow("my-flow", builder =>
{
    builder.AddProducer("source", sp => new MyProducer())
        .AddTransform("transform", sp => new MyTransformer())
        .AddProcessor("processor", sp => new MyProcessor());
});
```

### Resolving and Executing a DataFlow

```csharp
// Get the dataflow instance by name
var dataflow = serviceProvider.GetDataFlow("my-flow");
await dataflow.ExecuteAsync(context);
```

## Advanced Usage

### Working with Different Lifetimes

The dataflow instances use standard DI lifetimes:

```csharp
// Transient (default) - new instance each time
services.AddKeyedDataFlow("transient-flow", builder => { ... });
// or use the convenience method:
services.AddTransientDataFlow("transient-flow", builder => { ... });

// Singleton - same instance every time
services.AddKeyedDataFlow("singleton-flow", builder => { ... }, 
    ServiceLifetime.Singleton);
// or use the convenience method:
services.AddSingletonDataFlow("singleton-flow", builder => { ... });

// Scoped - same instance within a scope
services.AddKeyedDataFlow("scoped-flow", builder => { ... }, 
    ServiceLifetime.Scoped);
// or use the convenience method:
services.AddScopedDataFlow("scoped-flow", builder => { ... });
```

#### Transient DataFlows
Best for per-request or per-message processing:
```csharp
// Option 1: Manual resolution in a controller or handler
var dataflow = serviceProvider.GetDataFlow("request-processor");
await dataflow.ExecuteAsync(context);

// Option 2: Constructor injection using [FromKeyedServices] attribute
// Note: This attribute is only supported in endpoints, MVC controllers, and routing scenarios
public class MyEndpoint
{
    private readonly IDataFlow _dataflow;
    
    public MyEndpoint([FromKeyedServices("request-processor")] IDataFlow dataflow)
    {
        _dataflow = dataflow;
    }
    
    public async Task ProcessRequest(HttpContext context)
    {
        var flowContext = new DataFlowContext { CancellationToken = context.RequestAborted };
        await _dataflow.ExecuteAsync(flowContext);
    }
}
```

#### Singleton DataFlows
Best for background services or long-running flows:
```csharp
// Constructor injection pattern for IHostedService and other services
// Note: [FromKeyedServices] is NOT supported outside of endpoints/MVC controllers
// Use IKeyedServiceProvider instead
public class DataFlowBackgroundService : BackgroundService
{
    private readonly IDataFlow _dataflow;
    
    // Option 1: Inject IKeyedServiceProvider and resolve in constructor
    public DataFlowBackgroundService(IKeyedServiceProvider keyedServiceProvider)
    {
        _dataflow = keyedServiceProvider.GetRequiredKeyedService<IDataFlow>("background-processor");
    }
    
    // Option 2: Inject IServiceProvider and use GetDataFlow extension
    public DataFlowBackgroundService(IServiceProvider serviceProvider)
    {
        _dataflow = serviceProvider.GetDataFlow("background-processor");
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var context = new DataFlowContext { CancellationToken = stoppingToken };
        await _dataflow.ExecuteAsync(context);
    }
        await _dataflow.ExecuteAsync(context);
    }
}
```

#### Scoped DataFlows
Best for per-scope operations (e.g., per HTTP request):
```csharp
using (var scope = serviceProvider.CreateScope())
{
    var dataflow = scope.ServiceProvider.GetDataFlow("scoped-processor");
    await dataflow.ExecuteAsync(context);
}
```

### Accessing the Graph for Diagram Generation

```csharp
var dataflow = serviceProvider.GetDataFlow("my-flow");

// Access the graph directly from the dataflow
var graph = dataflow.Graph;
Console.WriteLine($"Flow: {graph.Name}");
Console.WriteLine($"Blocks: {graph.BlockDefinitions.Count}");

// Or use the helper extension method to render a diagram
var mermaid = serviceProvider.RenderDataFlowMermaidDiagram("my-flow");
Console.WriteLine(mermaid);
```

### Multiple Named DataFlows

```csharp
// Register multiple flows with different purposes
services.AddKeyedDataFlow("user-import", builder => 
{
    builder.AddProducer("csv-reader", sp => sp.GetRequiredService<CsvUserReader>())
        .AddProcessor("user-writer", sp => sp.GetRequiredService<UserWriter>());
});

services.AddKeyedDataFlow("order-processing", builder => 
{
    builder.AddProducer("order-queue", sp => sp.GetRequiredService<OrderQueueReader>())
        .AddProcessor("order-writer", sp => sp.GetRequiredService<OrderWriter>());
});

// Resolve different flows
var userFlow = serviceProvider.GetDataFlow("user-import");
var orderFlow = serviceProvider.GetDataFlow("order-processing");
```

### Working with Scoped Dependencies

```csharp
services.AddScoped<IMyDbContext, MyDbContext>();

services.AddKeyedDataFlow("db-flow", builder =>
{
    // Blocks can use scoped dependencies
    // Note: When blocks create concurrent "actors" (e.g., with max concurrency > 1),
    // each actor runs in its own separate DI scope, allowing safe use of scoped services
    // like DbContext across multiple concurrent operations.
    builder.AddProducer("source", sp => new DbProducer(
            sp.GetRequiredService<IMyDbContext>()))
        .AddProcessor("processor", sp => new DbProcessor(
            sp.GetRequiredService<IMyDbContext>()));
}, ServiceLifetime.Scoped); // Register as scoped

// Create a scope for execution
using (var scope = serviceProvider.CreateScope())
{
    var dataflow = scope.ServiceProvider.GetDataFlow("db-flow");
    await dataflow.ExecuteAsync(context);
}
```

## API Reference

### Extension Methods

#### `AddKeyedDataFlow`
Registers a named dataflow with dependency injection.

```csharp
IServiceCollection AddKeyedDataFlow(
    this IServiceCollection services,
    string name,
    Action<IStructuredDataFlowBuilder> configure,
    ServiceLifetime lifetime = ServiceLifetime.Transient)
```

#### `AddTransientDataFlow`
Registers a named dataflow as a transient service (convenience method).

```csharp
IServiceCollection AddTransientDataFlow(
    this IServiceCollection services,
    string name,
    Action<IStructuredDataFlowBuilder> configure)
```

#### `AddScopedDataFlow`
Registers a named dataflow as a scoped service (convenience method).

```csharp
IServiceCollection AddScopedDataFlow(
    this IServiceCollection services,
    string name,
    Action<IStructuredDataFlowBuilder> configure)
```

#### `AddSingletonDataFlow`
Registers a named dataflow as a singleton service (convenience method).

```csharp
IServiceCollection AddSingletonDataFlow(
    this IServiceCollection services,
    string name,
    Action<IStructuredDataFlowBuilder> configure)
```

#### `GetDataFlow`
Resolves a dataflow instance by name.

```csharp
IDataFlow GetDataFlow(
    this IServiceProvider serviceProvider,
    string name)
```

#### `RenderDataFlowMermaidDiagram`
Renders a Mermaid diagram for a dataflow.

```csharp
string RenderDataFlowMermaidDiagram(
    this IServiceProvider serviceProvider,
    string name)
```

### IDataFlow Interface

```csharp
public interface IDataFlow
{
    string Name { get; set; }
    DataFlowGraph? Graph { get; }
    Task ExecuteAsync(IDataFlowContext context);
}
```

**Properties:**
- `Name` - The name of the dataflow
- `Graph` - The graph representation for inspection (blocks, connections, types)

**Methods:**
- `ExecuteAsync()` - Executes the dataflow

## Benefits

1. **Standardized Registration**: Consistent pattern for registering dataflows with DI
2. **Named Resolution**: Easy to manage multiple dataflows in the same application
3. **DI Lifetime Management**: Leverage standard transient/scoped/singleton patterns
4. **Thread Safety**: Each instance manages its own state, safe for concurrent execution when using appropriate lifetimes
5. **Graph Inspection**: Access to graph structure for documentation/visualization
6. **Testability**: Easy to inject and test dataflows
7. **Flexibility**: Support for different service lifetimes based on your needs
