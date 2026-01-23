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
6. [Passing Trigger Context](#passing-trigger-context)
7. [Real-World Example](#real-world-example)
8. [Next Steps](#next-steps)

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

### Setting Up Global Usings

To simplify your code and make future namespace changes easier, add these global usings to your project. Create a `GlobalUsings.cs` file:

```csharp
// GlobalUsings.cs
global using DataFlow.POC.Builder;
global using DataFlow.POC.Blocks;
global using DataFlow.POC.Core;
global using DataFlow.POC.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection;
```

**Why Global Usings?**
- Reduces repetitive `using` statements in every file
- Makes future namespace refactoring easier (one place to update)
- Cleaner, more readable code examples

All code examples in this guide assume these global usings are configured.

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

This guide shows the **idiomatic, production-ready approach** using dependency injection and the canonical registration API.

### Step 1: Create the Project

```bash
dotnet new console -n MyFirstDataFlow
cd MyFirstDataFlow
dotnet add package Uniun.DataFlow
```

Create the `GlobalUsings.cs` file as shown in the [Installation](#installation) section.

### Step 2: Define Your Actors

Actors contain the logic that runs in your blocks. Create these classes in your project:

```csharp
// UppercaseActor.cs
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

// ConsoleWriterActor.cs  
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

### Step 3: Define a Source Actor

For the producer, create a source actor that reads from console:

```csharp
// ConsoleInputSource.cs
public class ConsoleInputSource : IPlainSourceActor<string>
{
    public async IAsyncEnumerable<string> ProduceAsync(
        IActorExecutionContext context)
    {
        Console.WriteLine("Enter text (type 'quit' to exit):");
        while (!context.CancellationToken.IsCancellationRequested)
        {
            var line = Console.ReadLine();
            if (line == "quit" || string.IsNullOrEmpty(line))
                break;
            yield return line;
            await Task.CompletedTask; // For async iterator
        }
    }
}
```

### Step 4: Register with Dependency Injection (Program.cs)

**This is the idiomatic way to use DataFlow in production applications.**

```csharp
// Program.cs
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Register actors
builder.Services.AddScoped<ConsoleInputSource>();
builder.Services.AddScoped<UppercaseActor>();
builder.Services.AddScoped<ConsoleWriterActor>();

// Register DataFlow components
builder.Services.AddDataFlows("app", df =>
{
    // Register source block
    df.AddBlock("input", sp =>
    {
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new PlainSourceAdapter<string, ConsoleInputSource>(
            new BlockContext("input"),
            scopeFactory,
            sourceName: "console-input");
    });
    
    // Register actor blocks (transform and processor)
    df.AddActorBlock<string, string, UppercaseActor>("uppercase");
    df.AddActorBlock<string, object, ConsoleWriterActor>("writer");
    
    // Define the graph
    df.AddGraph("main", g =>
    {
        g.UseBlock("input")
         .UseBlock("uppercase")
         .UseBlock("writer")
         .Connect("input", "uppercase")
         .Connect("uppercase", "writer");
    });
});

var app = builder.Build();

// Resolve and execute the graph
var graph = app.Services.GetKeyedService<DataFlowGraph>("app:main");
var context = new ExecutionContext(app.Services, CancellationToken.None);
await graph!.ExecuteAsync(context);

Console.WriteLine("Pipeline completed!");
```

### What Just Happened?

Let's break down the key patterns:

**1. Actor Registration**
```csharp
builder.Services.AddScoped<UppercaseActor>();
```
Actors are registered as scoped services, allowing them to use DI.

**2. Block Registration**
```csharp
df.AddActorBlock<string, string, UppercaseActor>("uppercase");
```
The `AddActorBlock` method registers a block with a unique name. The block wraps your actor and manages its lifecycle.

**3. Graph Definition**
```csharp
df.AddGraph("main", g =>
{
    g.UseBlock("input")
     .UseBlock("uppercase")
     .UseBlock("writer")
     .Connect("input", "uppercase")
     .Connect("uppercase", "writer");
});
```
`UseBlock` references blocks by name, keeping the graph definition clean and readable.

**4. Graph Resolution**
```csharp
var graph = app.Services.GetKeyedService<DataFlowGraph>("app:main");
```
Graphs are registered as keyed services. The key format is `"{namespace}:{graphname}"`.

### Why This Approach?

✅ **Production-Ready**: Uses standard .NET hosting and DI patterns  
✅ **Testable**: All components registered with DI can be mocked  
✅ **Maintainable**: Blocks defined by name, easy to rewire  
✅ **Scalable**: Namespace support allows multiple independent flows

---

## Executing Your Graph

DataFlow graphs can be executed in different contexts. Here's how to use them in various scenarios.

### In a Console Application

We already saw the recommended DI-based approach above. Here's the pattern again:

```csharp
var app = builder.Build();

var graph = app.Services.GetKeyedService<DataFlowGraph>("app:main");
var context = new ExecutionContext(app.Services, CancellationToken.None);
await graph!.ExecuteAsync(context);
```

**Key Points:**
- Graph is resolved from the service provider using its keyed service registration
- `ExecutionContext` is created with the service provider and cancellation token
- The graph coordinates all block execution automatically

### In an ASP.NET Core Application

You can inject graphs directly into your endpoints using keyed services:

```csharp
// Program.cs - Startup configuration
var builder = WebApplication.CreateBuilder(args);

// Register actors
builder.Services.AddScoped<DataProcessorActor>();
builder.Services.AddScoped<ValidationActor>();

// Register DataFlow
builder.Services.AddDataFlows("api", df =>
{
    df.AddActorBlock<Request, Request, ValidationActor>("validator");
    df.AddActorBlock<Request, Response, DataProcessorActor>("processor");
    
    df.AddGraph("process-request", g =>
    {
        g.UseBlock("validator")
         .UseBlock("processor")
         .Connect("validator", "processor");
    });
});

var app = builder.Build();

// Endpoint that uses the graph
app.MapPost("/api/process", async (
    [FromServices] IServiceProvider services,
    [FromBody] List<Request> requests,
    CancellationToken ct) =>
{
    var graph = services.GetKeyedService<DataFlowGraph>("api:process-request");
    
    // Create a source for the requests
    var source = new PlainSourceAdapter<Request, InMemorySource>(
        new BlockContext("request-source"),
        services.GetRequiredService<IServiceScopeFactory>(),
        sourceName: "api-requests");
    
    var context = new ExecutionContext(services, ct);
    await graph!.ExecuteAsync(context);
    
    return Results.Ok();
});

app.Run();
```

### In a Background Service

For long-running background processing:

```csharp
public class DataFlowBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    
    public DataFlowBackgroundService(IServiceProvider services)
    {
        _services = services;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Resolve the graph
        var graph = _services.GetKeyedService<DataFlowGraph>("app:background-processor");
        
        if (graph == null)
        {
            throw new InvalidOperationException("Graph 'app:background-processor' not found");
        }
        
        // Execute with cancellation support
        var context = new ExecutionContext(_services, stoppingToken);
        
        try
        {
            await graph.ExecuteAsync(context);
        }
        catch (OperationCanceledException)
        {
            // Expected when service stops
        }
    }
}

// Registration in Program.cs
builder.Services.AddHostedService<DataFlowBackgroundService>();
```

### Injecting into Regular Services

For services that don't support keyed service attribute injection:

```csharp
public class MyService
{
    private readonly IServiceProvider _services;
    
    public MyService(IServiceProvider services)
    {
        _services = services;
    }
    
    public async Task ProcessDataAsync(CancellationToken ct = default)
    {
        // Resolve graph by key
        var graph = _services.GetKeyedService<DataFlowGraph>("app:data-processor");
        
        if (graph == null)
        {
            throw new InvalidOperationException("Graph not found");
        }
        
        var context = new ExecutionContext(_services, ct);
        await graph.ExecuteAsync(context);
    }
}
```

**Pattern**: Inject `IServiceProvider` and use `GetKeyedService<DataFlowGraph>(key)` to resolve graphs.

---

## Passing Trigger Context

DataFlow supports passing trigger-specific metadata (like tenant ID, message properties, or request details) through the execution context. This allows your actors to adapt their behavior based on how the dataflow was triggered.

### When to Use Trigger Context

Use trigger context when your dataflow needs to:
- Process data differently for different tenants (multi-tenancy)
- Access message metadata for retry logic
- Track correlation IDs from web requests
- Use dynamic parameters determined at runtime

### Accessing Trigger Parameters (Recommended Approach)

The **recommended way** to access trigger parameters is using the Parameter Provider, which works with any trigger context type:

```csharp
// Define trigger context with known properties
var triggerContext = new ScheduledTriggerContext
{
    JobName = "DailyReport",
    TenantId = "tenant-123",
    ScheduledTime = DateTime.UtcNow
};

// Pass to execution context
var context = new ExecutionContext(
    app.Services,
    CancellationToken.None,
    Guid.NewGuid(),
    recoveryCheckpoint: null,
    metrics: null,
    triggerContext);

await graph.ExecuteAsync(context);
```

**Access parameters in actors (recommended):**
```csharp
public class TenantAwareActor : IStreamActor<Order, ProcessedOrder>
{
    public async IAsyncEnumerable<ProcessedOrder> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        // ✅ RECOMMENDED: Use Parameter Provider (works with any trigger type)
        var tenantId = context.Parameters.GetParameter("tenantId", "default");
        var jobName = context.Parameters.GetParameter("jobName", "unknown");
        
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            yield return ProcessOrderForTenant(order, tenantId);
        }
    }
}
```

**Alternative: Direct trigger context access (not recommended):**
```csharp
// ❌ NOT RECOMMENDED: Tightly coupled to specific trigger type
string tenantId = "default";
if (context.TriggerContext is ScheduledTriggerContext scheduled)
{
    tenantId = scheduled.TenantId ?? "default";
}
```

### Dynamic Trigger Context (For Flexible/Runtime Data)

When trigger structure is determined at runtime or varies between calls, use `JsonTriggerContext`:

```csharp
// Dynamic JSON-based trigger context
var triggerContext = new JsonTriggerContext
{
    Data = new JsonObject
    {
        ["tenantId"] = "tenant-123",
        ["reportDate"] = JsonValue.Create(DateTime.UtcNow),
        ["customProperty"] = 42,
        ["metadata"] = new JsonObject
        {
            ["source"] = "scheduler",
            ["priority"] = "high"
        }
    }
};

// Or create from JSON string
var triggerContext2 = JsonTriggerContext.FromJson(@"{
    ""tenantId"": ""tenant-456"",
    ""customData"": ""value""
}");

var context = new ExecutionContext(
    app.Services,
    CancellationToken.None,
    Guid.NewGuid(),
    null,
    null,
    triggerContext);

await graph.ExecuteAsync(context);
```

**Access dynamic properties in actors (recommended):**
```csharp
public class DynamicActor : IStreamActor<Data, Result>
{
    public async IAsyncEnumerable<Result> RunAsync(
        IAsyncEnumerable<Data> input,
        IActorExecutionContext context)
    {
        // ✅ RECOMMENDED: Use Parameter Provider (works with any trigger type)
        var tenantId = context.Parameters.GetParameter("tenantId", "default");
        var customProperty = context.Parameters.GetParameter("customProperty", 0);
        
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return ProcessWithContext(item, tenantId, customProperty);
        }
    }
}
```

### Built-in Trigger Context Types

DataFlow provides several built-in trigger context types:

**ScheduledTriggerContext** - For scheduled/timer jobs
```csharp
new ScheduledTriggerContext
{
    JobName = "DailyReport",
    TenantId = "tenant-123",
    ScheduledTime = DateTime.UtcNow,
    Metadata = new Dictionary<string, string> { ["region"] = "us-west" }
}
```

**MessageQueueTriggerContext** - For message queue triggers
```csharp
new MessageQueueTriggerContext
{
    QueueName = "orders-queue",
    MessageId = "msg-456",
    CorrelationId = "corr-789",
    DeliveryCount = 2,  // For retry logic
    MessageProperties = new Dictionary<string, string>
    {
        ["tenantId"] = "tenant-123"
    }
}
```

**WebRequestTriggerContext** - For HTTP request triggers
```csharp
new WebRequestTriggerContext
{
    UserId = "user-123",
    TenantId = "tenant-456",
    RequestPath = "/api/reports",
    RequestMethod = "POST",
    RequestHeaders = new Dictionary<string, string>
    {
        ["X-Correlation-Id"] = "corr-789"
    }
}
```

**JsonTriggerContext** - For flexible/dynamic scenarios
```csharp
new JsonTriggerContext
{
    Data = new JsonObject { /* any structure */ }
}
```

### Parameter Provider Pattern (Recommended)

For better decoupling, use the **Parameter Provider** instead of checking specific trigger context types. This allows actors to work with ANY trigger context that provides the requested parameters:

**Using Parameter Provider:**
```csharp
public class DecoupledActor : IStreamActor<Order, ProcessedOrder>
{
    public async IAsyncEnumerable<ProcessedOrder> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        // Decoupled - works with ANY trigger context that provides "tenantId"
        var tenantId = context.Parameters.GetParameter("tenantId", "default");
        var priority = context.Parameters.GetParameter("priority", "normal");
        
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            yield return ProcessOrderForTenant(order, tenantId, priority);
        }
    }
}
```

**Benefits:**
- ✅ No need to check specific trigger context types
- ✅ Works automatically with new trigger types
- ✅ Cleaner, more maintainable code
- ✅ Easier to test (mock parameter provider)

**Parameter Provider Methods:**

```csharp
// Try to get a parameter (returns false if not found)
if (context.Parameters.TryGetParameter<string>("tenantId", out var tenantId))
{
    // Use tenantId
}

// Get with default value
var pageSize = context.Parameters.GetParameter("pageSize", 50);

// Get required parameter (throws if missing)
var tenantId = context.Parameters.GetRequiredParameter<string>("tenantId");
```

**Works with all trigger context types:**

```csharp
// Scheduled job trigger
var triggerContext = new ScheduledTriggerContext { TenantId = "tenant-123" };

// Message queue trigger
var triggerContext = new MessageQueueTriggerContext 
{ 
    MessageProperties = new Dictionary<string, string> { ["tenantId"] = "tenant-123" }
};

// Web request trigger
var triggerContext = new WebRequestTriggerContext { TenantId = "tenant-123" };

// JSON trigger
var triggerContext = new JsonTriggerContext 
{ 
    Data = new JsonObject { ["tenantId"] = "tenant-123" }
};

// Actor code is the SAME for all cases:
var tenantId = context.Parameters.GetParameter("tenantId", "default");
```

### Best Practices

**1. Prefer Parameter Provider over direct trigger context checks:**
```csharp
// ✅ RECOMMENDED - Decoupled, works with any trigger type
var tenantId = context.Parameters.GetParameter("tenantId", "default");

// ❌ NOT RECOMMENDED - Tightly coupled to specific trigger types
if (context.TriggerContext is ScheduledTriggerContext scheduled)
    tenantId = scheduled.TenantId;
else if (context.TriggerContext is MessageQueueTriggerContext queue)
    tenantId = queue.MessageProperties?["tenantId"];
// ... more type checks
```

**2. Provide defaults for missing parameters:**
```csharp
// Good - always has a valid value
var tenantId = context.Parameters.GetParameter("tenantId", "default");
var pageSize = context.Parameters.GetParameter("pageSize", 50);
```

**3. Validate required parameters early:**
```csharp
public async IAsyncEnumerable<Result> RunAsync(
    IAsyncEnumerable<Data> input,
    IActorExecutionContext context)
{
    // Validate upfront before processing - throws if missing
    var tenantId = context.Parameters.GetRequiredParameter<string>("tenantId");
    
    await foreach (var item in input)
        yield return ProcessForTenant(item, tenantId);
}
```

**4. Document parameter requirements:**
```csharp
/// <summary>
/// Processes reports with tenant-specific logic.
/// </summary>
/// <remarks>
/// Required Parameters:
/// - tenantId (string): Tenant identifier
/// 
/// Optional Parameters:
/// - reportDate (DateTime): Report generation date
/// </remarks>
public class TenantAwareReportActor : IStreamActor<ReportData, Report>
{
    // Implementation
}
```

### Backward Compatibility

Trigger context is **completely optional**. Existing code continues to work without any changes:

```csharp
// No trigger context - works perfectly
var context = new ExecutionContext(app.Services, CancellationToken.None);
await graph.ExecuteAsync(context);

// Actors handle null context gracefully
if (context.TriggerContext is ScheduledTriggerContext scheduled)
{
    // Use trigger context
}
else
{
    // Use defaults
}
```

---

## Real-World Example

Let's build a more realistic example: processing orders from a database using Entity Framework Core.

This example demonstrates:
- Integration with EF Core
- Scoped services in actors
- Complete DI-based setup

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

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
    public DbSet<Order> Orders { get; set; } = null!;
    
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }
}

// Source actor: Reads orders from database
public class OrderSourceActor : IPlainSourceActor<Order>
{
    private readonly OrderDbContext _db;
    
    public OrderSourceActor(OrderDbContext db)
    {
        _db = db;
    }
    
    public async IAsyncEnumerable<Order> ProduceAsync(IActorExecutionContext context)
    {
        var orders = _db.Orders
            .Where(o => o.Status == "Pending")
            .AsAsyncEnumerable();
        
        await foreach (var order in orders.WithCancellation(context.CancellationToken))
        {
            yield return order;
        }
    }
}

// Processor: Applies business logic
public class OrderProcessorActor : IStreamActor<Order, Order>
{
    public async IAsyncEnumerable<Order> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            // Apply business logic
            Console.WriteLine($"Processing order {order.Id} - Total: ${order.Total}");
            order.Status = "Processed";
            yield return order;
        }
    }
}

// Saver: Persists changes back to database
public class OrderSaverActor : IStreamActor<Order, object>
{
    private readonly OrderDbContext _db;
    
    public OrderSaverActor(OrderDbContext db)
    {
        _db = db;
    }
    
    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            _db.Orders.Update(order);
            await _db.SaveChangesAsync(context.CancellationToken);
            // Processor doesn't yield output
        }
    }
}

// Program.cs - Complete setup
var builder = Host.CreateApplicationBuilder(args);

// Register EF Core
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseInMemoryDatabase("Orders"));

// Register actors
builder.Services.AddScoped<OrderSourceActor>();
builder.Services.AddScoped<OrderProcessorActor>();
builder.Services.AddScoped<OrderSaverActor>();

// Register DataFlow
builder.Services.AddDataFlows("orders", df =>
{
    // Source block
    df.AddBlock("source", sp =>
    {
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new PlainSourceAdapter<Order, OrderSourceActor>(
            new BlockContext("order-source"),
            scopeFactory,
            sourceName: "database");
    });
    
    // Processing blocks
    df.AddActorBlock<Order, Order, OrderProcessorActor>("processor");
    df.AddActorBlock<Order, object, OrderSaverActor>("saver");
    
    // Define graph
    df.AddGraph("process-orders", g =>
    {
        g.UseBlock("source")
         .UseBlock("processor")
         .UseBlock("saver")
         .Connect("source", "processor")
         .Connect("processor", "saver");
    });
});

var app = builder.Build();

// Seed some test data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Orders.AddRange(
        new Order { Id = 1, Total = 100.00m },
        new Order { Id = 2, Total = 200.00m },
        new Order { Id = 3, Total = 300.00m }
    );
    await db.SaveChangesAsync();
}

// Execute the graph
var graph = app.Services.GetKeyedService<DataFlowGraph>("orders:process-orders");
var context = new ExecutionContext(app.Services, CancellationToken.None);
await graph!.ExecuteAsync(context);

Console.WriteLine("All orders processed!");
```

**Key Takeaways:**
- ✅ Actors receive `DbContext` via DI (scoped)
- ✅ Source actor reads from database
- ✅ Processor applies business logic
- ✅ Saver persists changes
- ✅ Everything registered and resolved through DI

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
10. **[Source Blocks](./source-blocks.md)** - Creating custom data sources

---

## Common Patterns Reference

### Pattern: Named Block Registration

```csharp
builder.Services.AddDataFlows("app", df =>
{
    // Register blocks with descriptive names
    df.AddActorBlock<Input, Output, MyActor>("processor");
    
    // Reuse blocks in multiple graphs
    df.AddGraph("graph1", g => g.UseBlock("processor"));
    df.AddGraph("graph2", g => g.UseBlock("processor"));
});
```

**Why**: Promotes reusability and keeps graph definitions clean.

### Pattern: Namespace Organization

```csharp
// Separate concerns by namespace
builder.Services.AddDataFlows("orders", df => { /* order processing */ });
builder.Services.AddDataFlows("inventory", df => { /* inventory management */ });
builder.Services.AddDataFlows("shipping", df => { /* shipping */ });
```

**Why**: Prevents naming conflicts and organizes complex applications.

### Pattern: Scoped Dependencies

```csharp
public class MyActor : IStreamActor<Input, Output>
{
    private readonly DbContext _db;  // Scoped service
    
    public MyActor(DbContext db)
    {
        _db = db;  // Injected automatically
    }
    
    // ... implementation
}
```

**Why**: DataFlow creates new scopes for each actor, enabling safe use of scoped services like `DbContext`.

---

## Troubleshooting

### Graph not found

**Problem**: `GetKeyedService<DataFlowGraph>("app:main")` returns null

**Solution**: Check the key format is `"{namespace}:{graphname}"`. Verify registration:
```csharp
builder.Services.AddDataFlows("app", df =>  // namespace
{
    df.AddGraph("main", g => { ... });      // graph name
});

// Resolve with: "app:main"
```

### Block not found

**Problem**: `UseBlock("my-block")` throws "Block not found"

**Solution**: Ensure block is registered in the same namespace:
```csharp
df.AddActorBlock<T1, T2, MyActor>("my-block");  // Register first
df.AddGraph("g", g => g.UseBlock("my-block"));   // Then use
```

### Type mismatch errors

**Problem**: "Type mismatch: source output doesn't match target input"

**Solution**: Verify generic type parameters match:
```csharp
// ✅ Correct - types match
df.AddActorBlock<string, int, Parser>("parser");       // string → int
df.AddActorBlock<int, Result, Processor>("processor"); // int → Result
g.Connect("parser", "processor");                       // string → int → Result

// ❌ Wrong - type mismatch
df.AddActorBlock<string, int, Parser>("parser");        // string → int
df.AddActorBlock<string, Result, Processor>("processor"); // string → Result
g.Connect("parser", "processor");                       // int ≠ string
```

---

## Summary

You've learned:

✅ How to install and configure DataFlow with global usings  
✅ The three core concepts: Blocks, Edges, and Graphs  
✅ How to define actors for your business logic  
✅ **The idiomatic, DI-based registration pattern**  
✅ How to execute graphs in different contexts  
✅ Real-world integration with Entity Framework Core  

**Next**: Dive deeper into [Working with Blocks](./working-with-blocks.md) to master block patterns and advanced registration techniques.

---

**Questions or Issues?**  
- Check the [Troubleshooting](#troubleshooting) section above
- Review the [Common Patterns](#common-patterns-reference)
- Consult the advanced guides listed in [Next Steps](#next-steps)
