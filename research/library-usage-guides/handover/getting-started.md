# Getting Started with DataFlow

**Audience**: New users  
**Prerequisites**: .NET 8.0+, basic C# knowledge  
**Time to Complete**: 20-30 minutes

---

## Table of Contents

1. [What is DataFlow?](#what-is-dataflow)
2. [Installation](#installation)
3. [Core Concepts](#core-concepts)
4. [Your First DataFlow Graph](#your-first-dataflow-graph)
5. [Executing Your Graph](#executing-your-graph)
6. [Real-World Example](#real-world-example)
7. [Next Steps](#next-steps)

---

## What is DataFlow?

DataFlow is a high-performance, pull-based data processing library built on `System.Threading.Channels`. It provides a fluent API for building concurrent data processing pipelines with efficient backpressure handling.

### Key Benefits

- **Pull-Based Architecture**: Downstream blocks pull data when ready, providing natural backpressure
- **First-Class DI Support**: Built-in dependency injection with scoped services
- **Flexible Topologies**: Broadcast, competing consumers, selective routing, and more
- **Transaction Support**: Epoch-based boundaries for transactional workflows
- **Type-Safe**: Strongly-typed blocks and transformations

### When to Use DataFlow

✅ **Use DataFlow when you need:**
- Concurrent data processing pipelines
- Backpressure handling for data streams
- Integration with Entity Framework Core or other scoped services
- Transaction boundaries and checkpointing
- Complex routing and fan-out patterns

❌ **Consider alternatives when:**
- You only need simple sequential processing
- Your data flow is trivial (single transform)
- You don't need concurrency or backpressure

---

## Installation

Add the DataFlow package to your project:

```bash
dotnet add package Uniun.DataFlow
```

**Note**: During POC phase, use the POC project directly. The package name shown above is for illustration.

---

## Core Concepts

Before diving in, understand these three core concepts:

### 1. Blocks

Blocks are the building blocks of your pipeline. They process data:

- **Producer Blocks**: Generate data (source of the pipeline)
- **Transform Blocks**: Transform data (input → output)
- **Processor Blocks**: Consume data (end of pipeline)
- **Actor Blocks**: DI-aware blocks with automatic scope management

### 2. Edges

Edges connect blocks and define how data flows:

- Control buffering behavior (bounded, unbounded)
- Manage backpressure
- Define routing strategies (broadcast, competing, selective)

### 3. Graphs

Graphs orchestrate the entire pipeline:

- Define topology (which blocks connect to which)
- Manage execution lifecycle
- Coordinate concurrent execution

```
┌────────────────────────────────────────────────┐
│                    Graph                        │
│                                                 │
│  Producer ──[Edge]──▶ Transform ──[Edge]──▶ Processor
│            BufferMode                BufferMode │
│            Capacity                  Capacity   │
└────────────────────────────────────────────────┘
```

---

## Your First DataFlow Graph

Let's build a simple pipeline that processes console input, transforms it to uppercase, and writes the result.

### Step 1: Create the Project

```bash
dotnet new console -n MyFirstDataFlow
cd MyFirstDataFlow
# Add DataFlow package here
```

### Step 2: Define Your Actors

Actors are the logic that runs in your blocks. They're simple classes that implement an interface.

```csharp
using DataFlow.POC.Core;

// Transform actor: converts strings to uppercase
public class UppercaseActor : IStreamActor<string, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<string> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return item.ToUpperInvariant();
        }
    }
}

// Processor actor: writes to console
public class ConsoleWriterActor : IStreamActor<string, object>
{
    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<string> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            Console.WriteLine($"Output: {item}");
            // Processor doesn't yield output
        }
    }
}
```

### Step 3: Build the Graph

Now let's connect everything together:

```csharp
using DataFlow.POC.Builder;
using DataFlow.POC.Blocks;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;

// Create a producer that reads from console
var producer = BlockHelpers.CreateProducer<string>("input", async ctx => ReadLinesAsync());

// Create transformer and processor blocks using actors
var transformer = BlockHelpers.CreateActor<string, string, UppercaseActor>(
    "uppercase",
    new UppercaseActor());

var processor = BlockHelpers.CreateActor<string, object, ConsoleWriterActor>(
    "writer",
    new ConsoleWriterActor());

// Build the graph
var graph = GraphHelpers.CreateGraphBuilder("my-first-flow")
    .AddBlock(producer)
    .AddBlock(transformer)
    .AddBlock(processor)
    .Connect(producer, transformer)     // input → uppercase
    .Connect(transformer, processor)    // uppercase → writer
    .Build();

// Helper function to read console lines
async IAsyncEnumerable<string> ReadLinesAsync()
{
    Console.WriteLine("Enter text (type 'quit' to exit):");
    while (true)
    {
        var line = Console.ReadLine();
        if (line == "quit" || string.IsNullOrEmpty(line))
            break;
        yield return line;
    }
}
```

### Step 4: Execute the Graph

```csharp
// Create execution context
var serviceProvider = new ServiceCollection().BuildServiceProvider();
var context = new ExecutionContext(serviceProvider, CancellationToken.None);

// Execute!
await graph.ExecuteAsync(context);

Console.WriteLine("Pipeline completed!");
```

### Complete Example

Here's the full `Program.cs`:

```csharp
using DataFlow.POC.Builder;
using DataFlow.POC.Blocks;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;

// Define actors
public class UppercaseActor : IStreamActor<string, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<string> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return item.ToUpperInvariant();
        }
    }
}

public class ConsoleWriterActor : IStreamActor<string, object>
{
    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<string> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            Console.WriteLine($"Output: {item}");
        }
    }
}

// Helper for console input
static async IAsyncEnumerable<string> ReadLinesAsync()
{
    Console.WriteLine("Enter text (type 'quit' to exit):");
    while (true)
    {
        var line = Console.ReadLine();
        if (line == "quit" || string.IsNullOrEmpty(line))
            break;
        yield return line;
    }
}

// Build and execute the graph
var producer = BlockHelpers.CreateProducer<string>("input", ctx => ReadLinesAsync());
var transformer = BlockHelpers.CreateActor<string, string, UppercaseActor>("uppercase", new UppercaseActor());
var processor = BlockHelpers.CreateActor<string, object, ConsoleWriterActor>("writer", new ConsoleWriterActor());

var graph = GraphHelpers.CreateGraphBuilder("my-first-flow")
    .AddBlock(producer)
    .AddBlock(transformer)
    .AddBlock(processor)
    .Connect(producer, transformer)
    .Connect(transformer, processor)
    .Build();

var serviceProvider = new ServiceCollection().BuildServiceProvider();
var context = new ExecutionContext(serviceProvider, CancellationToken.None);

await graph.ExecuteAsync(context);
Console.WriteLine("Pipeline completed!");
```

---

## Executing Your Graph

DataFlow graphs can be executed in different contexts. Here's how to use them in various scenarios.

### In a Console Application

We already saw this above. The key pattern is:

```csharp
var serviceProvider = new ServiceCollection().BuildServiceProvider();
var context = new ExecutionContext(serviceProvider, CancellationToken.None);
await graph.ExecuteAsync(context);
```

### With Dependency Injection

The recommended approach is to register your blocks and graphs with DI:

```csharp
using DataFlow.POC.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Register DataFlow components
builder.Services.AddDataFlows("global", df =>
{
    // Register blocks with names
    df.AddBlock("producer", sp => BlockHelpers.CreateProducer<string>(
        "input", 
        ctx => ReadLinesAsync()));
    
    df.AddActorBlock<string, string, UppercaseActor>("transformer");
    df.AddActorBlock<string, object, ConsoleWriterActor>("writer");
    
    // Register a complete graph
    df.AddGraph("main", g =>
    {
        g.UseBlock("producer")
         .UseBlock("transformer")
         .UseBlock("writer")
         .Connect("producer", "transformer")
         .Connect("transformer", "writer");
    });
});

var app = builder.Build();

// Resolve and execute
var graph = app.Services.GetKeyedService<DataFlowGraph>("global:main");
var context = new ExecutionContext(app.Services, CancellationToken.None);
await graph!.ExecuteAsync(context);
```

### In an ASP.NET Core Endpoint

You can inject graphs directly into your endpoints using keyed services:

```csharp
using Microsoft.AspNetCore.Mvc;
using DataFlow.POC.Core;

var builder = WebApplication.CreateBuilder(args);

// Register DataFlow (same as above)
builder.Services.AddDataFlows("global", df =>
{
    // ... block and graph registration
});

var app = builder.Build();

// Inject using [FromKeyedServices]
app.MapPost("/process", async (
    [FromKeyedServices("global:main")] DataFlowGraph graph,
    HttpContext httpContext) =>
{
    var context = new ExecutionContext(
        httpContext.RequestServices, 
        httpContext.RequestAborted);
    
    await graph.ExecuteAsync(context);
    return Results.Ok("Processing complete");
});

app.Run();
```

### In a Service Class

For regular services that don't support `[FromKeyedServices]`, use `IServiceProvider`:

```csharp
public class DataProcessingService
{
    private readonly IServiceProvider _serviceProvider;
    
    public DataProcessingService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public async Task ProcessDataAsync(CancellationToken cancellationToken)
    {
        // Resolve the graph by key
        var graph = _serviceProvider.GetKeyedService<DataFlowGraph>("global:main");
        
        if (graph == null)
            throw new InvalidOperationException("Graph 'global:main' not found");
        
        // Execute
        var context = new ExecutionContext(_serviceProvider, cancellationToken);
        await graph.ExecuteAsync(context);
    }
}

// Register the service
builder.Services.AddScoped<DataProcessingService>();
```

---

## Real-World Example

Let's build a more realistic example: processing orders from a database.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using DataFlow.POC.DependencyInjection;

// Domain model
public class Order
{
    public int Id { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "Pending";
}

// DbContext
public class OrderDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
    
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }
}

// Actors
public class OrderProcessor : IStreamActor<Order, Order>
{
    public async IAsyncEnumerable<Order> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            // Process order
            Console.WriteLine($"Processing order {order.Id} - Total: ${order.Total}");
            order.Status = "Processed";
            yield return order;
        }
    }
}

public class OrderSaver : IStreamActor<Order, object>
{
    private readonly OrderDbContext _dbContext;
    
    public OrderSaver(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            _dbContext.Orders.Update(order);
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            Console.WriteLine($"Saved order {order.Id}");
        }
    }
}

// Setup
var services = new ServiceCollection();

// Register DbContext
services.AddDbContext<OrderDbContext>(options =>
    options.UseInMemoryDatabase("OrdersDb"));

// Register DataFlow
services.AddDataFlows("orders", df =>
{
    // Producer: fetch orders from database
    df.AddBlock("order-source", sp =>
    {
        var dbContext = sp.GetRequiredService<OrderDbContext>();
        return BlockHelpers.CreateProducer<Order>("order-source", async ctx =>
        {
            var orders = await dbContext.Orders
                .Where(o => o.Status == "Pending")
                .ToListAsync(ctx.CancellationToken);
            
            foreach (var order in orders)
                yield return order;
        });
    });
    
    // Processor and Saver
    df.AddActorBlock<Order, Order, OrderProcessor>("processor");
    df.AddBlock("saver", sp =>
    {
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return BlockHelpers.CreateActor<Order, object, OrderSaver>("saver", scopeFactory);
    });
    
    // Graph
    df.AddGraph("process-orders", g =>
    {
        g.UseBlock("order-source")
         .UseBlock("processor")
         .UseBlock("saver")
         .Connect("order-source", "processor")
         .Connect("processor", "saver");
    });
});

var serviceProvider = services.BuildServiceProvider();

// Execute
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("orders:process-orders");
var context = new ExecutionContext(serviceProvider, CancellationToken.None);
await graph!.ExecuteAsync(context);
```

---

## Next Steps

Congratulations! You've built your first DataFlow pipeline. Here's where to go next:

### Essential Reading

1. **[Working with Blocks](./working-with-blocks.md)** - Deep dive into block types and patterns
2. **[Dependency Injection Registration](./dependency-injection-registration.md)** - Master the DI system
3. **[Testing Guide](./testing-guide.md)** - Learn how to test your pipelines

### Learn About Topologies

4. **[Broadcast Topology](./topology-broadcast.md)** - Fan-out to multiple consumers
5. **[Competing Consumers](./topology-competing-consumers.md)** - Load balancing patterns
6. **[Selective Routing](./topology-selective-routing.md)** - Content-based routing

### Advanced Features

7. **[Using Epochs](./using-epochs.md)** - Transaction boundaries and coordination
8. **[Epoch Actor Block](./epoch-actor-block.md)** - Scope rotation for memory management
9. **[Checkpointing](./checkpointing.md)** - Resume processing from where you left off
10. **[EF Core with Epochs](./ef-core-epochs.md)** - Database transactions with DataFlow

### Patterns and Best Practices

11. **[Business Logic Decoupling](./business-logic-decoupling.md)** - Separate concerns
12. **[Source Blocks](./source-blocks.md)** - Creating custom data sources

---

## Common Patterns

### Pattern: UseBlock for Cleaner Code

**Recommended**: Register blocks with names, then use `UseBlock` when building graphs:

```csharp
services.AddDataFlows("global", df =>
{
    // Register blocks with meaningful names
    df.AddActorBlock<string, string, UppercaseActor>("uppercase");
    df.AddActorBlock<string, object, ConsoleWriterActor>("writer");
    
    // Build graph using names
    df.AddGraph("main", g =>
    {
        g.UseBlock("uppercase")
         .UseBlock("writer")
         .Connect("uppercase", "writer");
    });
});
```

**Why?** This keeps your graph building code clean and separates block creation from topology definition.

### Pattern: Scoped Services for Database Access

Always use scoped lifetime for blocks that access databases:

```csharp
services.AddDataFlows("global", df =>
{
    // ✅ Good: Scoped lifetime (default)
    df.AddBlock("db-reader", sp => 
    {
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return BlockHelpers.CreateActor<int, Order, OrderReaderActor>("db-reader", scopeFactory);
    });
});
```

### Pattern: Cancellation Token

Always respect the cancellation token:

```csharp
public class MyActor : IStreamActor<int, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        // ✅ Good: Pass cancellation token
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return $"Item-{item}";
        }
    }
}
```

---

## Troubleshooting

### "Block not found" when using UseBlock

**Problem**: `InvalidOperationException: Block 'my-block' not found`

**Solution**: Make sure you registered the block with AddDataFlows:

```csharp
services.AddDataFlows("global", df =>
{
    df.AddBlock("my-block", sp => new MyBlock()); // Register first!
});

// Then use it
var builder = GraphHelpers.CreateGraphBuilder("test", serviceProvider);
builder.UseBlock("my-block"); // Now it works
```

### "No service provider" when using UseBlock

**Problem**: `InvalidOperationException: Cannot use UseBlock without a service provider`

**Solution**: Pass the service provider when creating the builder:

```csharp
// ❌ Wrong
var builder = new DataFlowGraphBuilder("test");

// ✅ Correct
var builder = GraphHelpers.CreateGraphBuilder("test", serviceProvider);
```

### Pipeline doesn't complete

**Problem**: Graph execution hangs and never completes

**Solution**: Make sure your producer completes the stream:

```csharp
// ✅ Good: Producer completes
async IAsyncEnumerable<int> ProduceData()
{
    for (int i = 0; i < 10; i++)
        yield return i;
    // Stream completes after 10 items
}

// ❌ Bad: Infinite stream
async IAsyncEnumerable<int> ProduceData()
{
    while (true) // Never completes!
        yield return 1;
}
```

---

## Summary

You've learned:

- ✅ Core DataFlow concepts (blocks, edges, graphs)
- ✅ How to build your first pipeline
- ✅ How to execute graphs in different contexts
- ✅ Dependency injection patterns
- ✅ Common patterns and troubleshooting

**Next**: Explore [Working with Blocks](./working-with-blocks.md) to learn about different block types and advanced patterns.

---

**Questions or Issues?** Check the [Testing Guide](./testing-guide.md) for comprehensive testing patterns, or review the [Dependency Injection Registration Guide](./dependency-injection-registration.md) for advanced DI scenarios.
