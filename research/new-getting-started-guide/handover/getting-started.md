# Getting Started with DataFlow

**Time to Complete**: 15 minutes  
**Prerequisites**: .NET 8.0+, basic C# knowledge

---

## What is DataFlow?

DataFlow is a pull-based data processing library for building concurrent pipelines in .NET. It's built on `System.Threading.Channels` and provides natural backpressure handling with first-class dependency injection support.

### Key Benefits

- **Pull-Based**: Downstream blocks pull data when ready (automatic backpressure)
- **DI Integration**: Built-in support for scoped services (like EF Core DbContext)
- **Type-Safe**: Strongly-typed blocks prevent runtime errors
- **Concurrent**: Safe concurrent execution with proper scoping

### Core Concepts

**Blocks** - Processing units (producers, transformers, processors)  
**Graphs** - Connected blocks forming a pipeline  
**Execution Context** - Runtime environment with DI scope

---

## Your First DataFlow

Let's build a simple console app that reads input, transforms it, and writes output.

### Step 1: Create the Project

```bash
dotnet new console -n MyFirstDataFlow
cd MyFirstDataFlow
# Add DataFlow reference (adjust path to your POC location)
```

### Step 2: Add Global Usings

Create `GlobalUsings.cs`:

```csharp
global using DataFlow.POC.Builder;
global using DataFlow.POC.Blocks;
global using DataFlow.POC.Core;
global using DataFlow.POC.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection;
```

### Step 3: Define Your Blocks

Create `Program.cs`:

**Important**: In .NET 6+, top-level statements must come before class definitions. We'll add the main program logic first, then the block definitions after.

```csharp
using System.Runtime.CompilerServices;

// ===== MAIN PROGRAM (we'll add this in Step 4) =====
// ... main logic goes here ...

// ===== BLOCK DEFINITIONS (must come after top-level statements) =====

// Producer: Reads console input
public class ConsoleInputBlock : BlockBase<object, string>
{
    public ConsoleInputBlock() : base(new BlockContext("input")) { }

    public override async IAsyncEnumerable<string> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        Console.WriteLine("Enter lines (empty to quit):");
        while (!context.CancellationToken.IsCancellationRequested)
        {
            var line = Console.ReadLine();
            if (string.IsNullOrEmpty(line)) break;
            yield return line;
            await Task.CompletedTask;
        }
    }
}

// Transformer: Converts to uppercase
public class UppercaseBlock : BlockBase<string, string>
{
    public UppercaseBlock() : base(new BlockContext("uppercase")) { }

    public override async IAsyncEnumerable<string> ExecuteAsync(
        IAsyncEnumerable<string> input,
        IExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return item.ToUpperInvariant();
        }
    }
}

// Processor: Writes to console
public class ConsoleWriterBlock : BlockBase<string, object>
{
    public ConsoleWriterBlock() : base(new BlockContext("writer")) { }

    public override async IAsyncEnumerable<object> ExecuteAsync(
        IAsyncEnumerable<string> input,
        IExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            Console.WriteLine($"Output: {item}");
        }
        yield break; // Terminal block
    }
}
```

### Step 4: Add Main Program Logic

Now add the main program logic at the **top** of `Program.cs` (before the block definitions):

```csharp
using System.Runtime.CompilerServices;

// ===== MAIN PROGRAM (must be first) =====
var services = new ServiceCollection();

// Register DataFlow with namespace "app"
services.AddDataFlows("app", df =>
{
    // Register blocks with names
    df.AddBlock("input", sp => new ConsoleInputBlock());
    df.AddBlock("uppercase", sp => new UppercaseBlock());
    df.AddBlock("writer", sp => new ConsoleWriterBlock());
    
    // Define graph
    df.AddGraph("main", g =>
    {
        g.UseBlock("input")          // Reference by name
         .UseBlock("uppercase")
         .UseBlock("writer")
         .Connect("input", "uppercase")
         .Connect("uppercase", "writer");
    });
});

var serviceProvider = services.BuildServiceProvider();

// ... block definitions come after this ...
```

**Note**: If you encounter ambiguous type errors, use fully qualified names:
```csharp
var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
// ...
var context = new DataFlow.POC.Core.ExecutionContext(serviceProvider, CancellationToken.None);
```

### Step 5: Execute the Graph

Add graph execution after the DI setup (still in the main program section):

```csharp
// Resolve graph using keyed services
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("app:main");

if (graph == null)
{
    Console.WriteLine("ERROR: Graph not found!");
    return;
}

// Create execution context
var context = new ExecutionContext(serviceProvider, CancellationToken.None);

// Execute!
await graph.ExecuteAsync(context);

Console.WriteLine("Pipeline completed!");
```

### Run It!

```bash
dotnet run
```

Try typing some text - you'll see it transformed to uppercase!

---

## Understanding What Just Happened

### 1. The `AddDataFlows` Pattern

```csharp
services.AddDataFlows("app", df => { ... });
```

- **"app"** is the **namespace** - organizes your blocks and graphs
- Keeps different concerns separate (e.g., "orders", "inventory")
- Prevents naming conflicts

### 2. Named Block Registration

```csharp
df.AddBlock("input", sp => new ConsoleInputBlock());
```

- Blocks registered with **names**
- Allows **reuse** across multiple graphs
- Service provider (`sp`) available for dependency injection

### 3. The `UseBlock` Pattern

```csharp
g.UseBlock("input")
 .UseBlock("uppercase")
 .UseBlock("writer")
 .Connect("input", "uppercase");
```

- **References blocks by name** (doesn't create them)
- Fluent API for readable graph definitions
- `Connect()` defines data flow between blocks

### 4. Keyed Service Resolution

```csharp
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("app:main");
```

- Format: **`"{namespace}:{graphname}"`**
- Uses .NET's built-in keyed services
- Clean way to resolve specific graphs

---

## Reusing Blocks Across Multiple Graphs

One of DataFlow's strengths is registering blocks once and using them in multiple graphs.

### Example: Two Graphs Sharing a Transformer

```csharp
services.AddDataFlows("app", df =>
{
    // Register blocks ONCE
    df.AddBlock("uppercase", sp => new UppercaseBlock());
    df.AddBlock("input-a", sp => new ConsoleInputBlock());
    df.AddBlock("input-b", sp => new ConsoleInputBlock());
    df.AddBlock("writer-a", sp => new ConsoleWriterBlock());
    df.AddBlock("writer-b", sp => new ConsoleWriterBlock());
    
    // Graph A: Uses uppercase transformer
    df.AddGraph("pipeline-a", g =>
    {
        g.UseBlock("input-a")
         .UseBlock("uppercase")  // ← Shared block
         .UseBlock("writer-a")
         .Connect("input-a", "uppercase")
         .Connect("uppercase", "writer-a");
    });
    
    // Graph B: Also uses uppercase transformer
    df.AddGraph("pipeline-b", g =>
    {
        g.UseBlock("input-b")
         .UseBlock("uppercase")  // ← Same shared block
         .UseBlock("writer-b")
         .Connect("input-b", "uppercase")
         .Connect("uppercase", "writer-b");
    });
});

// Execute either graph
var graphA = serviceProvider.GetKeyedService<DataFlowGraph>("app:pipeline-a");
var graphB = serviceProvider.GetKeyedService<DataFlowGraph>("app:pipeline-b");
```

**Benefits**:
- Define blocks once, use many times
- DRY (Don't Repeat Yourself)
- Change block implementation in one place

---

## Namespace Organization

For larger applications, use namespaces to organize different concerns:

```csharp
// Order processing
services.AddDataFlows("orders", df =>
{
    df.AddBlock("order-reader", sp => new OrderReaderBlock());
    df.AddBlock("order-processor", sp => new OrderProcessorBlock());
    df.AddGraph("process-orders", g => { ... });
});

// Inventory management
services.AddDataFlows("inventory", df =>
{
    df.AddBlock("inventory-reader", sp => new InventoryReaderBlock());
    df.AddBlock("inventory-updater", sp => new InventoryUpdaterBlock());
    df.AddGraph("update-inventory", g => { ... });
});

// Resolve specific graphs
var orderGraph = serviceProvider.GetKeyedService<DataFlowGraph>("orders:process-orders");
var inventoryGraph = serviceProvider.GetKeyedService<DataFlowGraph>("inventory:update-inventory");
```

**Benefits**:
- Prevents naming conflicts
- Clear separation of concerns
- Each namespace is independent

---

## Execution Contexts and Scopes

Every graph execution needs an `ExecutionContext` which provides:
- Service provider (for DI)
- Cancellation token (for cancellation)

### Basic Execution

```csharp
var context = new ExecutionContext(serviceProvider, CancellationToken.None);
await graph.ExecuteAsync(context);
```

### Using Scopes (Recommended)

```csharp
using (var scope = serviceProvider.CreateScope())
{
    var context = new ExecutionContext(scope.ServiceProvider, CancellationToken.None);
    await graph.ExecuteAsync(context);
}
// Scope disposed - all scoped services cleaned up
```

**Why scopes matter**:
- Each execution gets fresh instances of scoped blocks
- Safe for EF Core `DbContext` (scoped service)
- Prevents memory leaks
- Enables safe concurrent execution

### Concurrent Execution

```csharp
// Execute same graph twice concurrently
using var scope1 = serviceProvider.CreateScope();
using var scope2 = serviceProvider.CreateScope();

var context1 = new ExecutionContext(scope1.ServiceProvider, CancellationToken.None);
var context2 = new ExecutionContext(scope2.ServiceProvider, CancellationToken.None);

await Task.WhenAll(
    graph.ExecuteAsync(context1),
    graph.ExecuteAsync(context2)
);
```

**Each execution**:
- Gets its own scope
- Gets fresh block instances
- Runs safely in parallel

---

## Common Patterns

### Pattern: Scoped Block Registration

```csharp
df.AddScopedBlock("my-block", sp => new MyBlock());
```

- Explicitly marks block as scoped lifetime
- Default lifetime is already scoped
- Useful for clarity and consistency

### Pattern: Using Cancellation

```csharp
var cts = new CancellationTokenSource();
var context = new ExecutionContext(serviceProvider, cts.Token);

// Cancel after 10 seconds
cts.CancelAfter(TimeSpan.FromSeconds(10));

try
{
    await graph.ExecuteAsync(context);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Execution cancelled");
}
```

### Pattern: ASP.NET Core Integration

```csharp
// In Program.cs (ASP.NET Core)
builder.Services.AddDataFlows("api", df =>
{
    df.AddBlock("validator", sp => new ValidationBlock());
    df.AddBlock("processor", sp => new ProcessorBlock());
    df.AddGraph("process-request", g => { ... });
});

// In an endpoint
app.MapPost("/process", async (HttpContext httpContext) =>
{
    var graph = httpContext.RequestServices
        .GetKeyedService<DataFlowGraph>("api:process-request");
    
    var context = new ExecutionContext(
        httpContext.RequestServices,
        httpContext.RequestAborted);
    
    await graph.ExecuteAsync(context);
    return Results.Ok();
});
```

---

## Next Steps

You now understand the fundamentals of DataFlow! Here's where to go next:

### Learn More Features
- **[Working with Blocks](./working-with-blocks.md)** - Block types, custom blocks, patterns
- **[Topology Patterns](./control-flow-topologies.md)** - Broadcast, competing consumers, routing
- **[Source Blocks](./source-blocks.md)** - Database sources, file sources, API sources

### Advanced Topics
- **[Using Epochs](./using-epochs.md)** - Transaction boundaries, checkpointing
- **[EF Core Integration](./ef-core-epochs.md)** - Database patterns with epochs
- **[Dependency Injection](./dependency-injection-registration.md)** - Advanced DI patterns

### Reference
- **[Testing Guide](./testing-guide.md)** - Testing your DataFlows
- **[Business Logic Decoupling](./business-logic-decoupling.md)** - Separation patterns

---

## Troubleshooting

### Graph not found

```csharp
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("app:main");
// Returns null
```

**Solution**: Check the key format `"{namespace}:{graphname}"` matches your registration:

```csharp
services.AddDataFlows("app", df =>      // ← namespace
{
    df.AddGraph("main", g => { ... });  // ← graph name
});
```

### Block not found

```csharp
g.UseBlock("my-block")
// Throws: "Block 'my-block' not found"
```

**Solution**: Ensure block is registered before use:

```csharp
df.AddBlock("my-block", sp => new MyBlock());  // ← Register first
df.AddGraph("g", g => g.UseBlock("my-block")); // ← Then use
```

### Type mismatch

```csharp
g.Connect("producer", "transformer")
// Throws: "Type mismatch: producer outputs int, transformer expects string"
```

**Solution**: Verify generic type parameters match:

```csharp
// Producer outputs int
public class ProducerBlock : BlockBase<object, int> { ... }

// Transformer must accept int
public class TransformerBlock : BlockBase<int, string> { ... }
```

---

## Summary

You've learned:

✅ How to register blocks and graphs with DI  
✅ The `UseBlock` pattern for graph definition  
✅ Keyed service resolution (`"{namespace}:{graphname}"`)  
✅ Block reuse across multiple graphs  
✅ Namespace organization for complex apps  
✅ Execution contexts and scoping  
✅ Safe concurrent execution

**Key Pattern** (commit this to memory):

```csharp
// 1. Register
services.AddDataFlows("namespace", df =>
{
    df.AddBlock("block-name", sp => new MyBlock());
    df.AddGraph("graph-name", g =>
    {
        g.UseBlock("block-name")
         .Connect(...);
    });
});

// 2. Resolve
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("namespace:graph-name");

// 3. Execute
var context = new ExecutionContext(serviceProvider, CancellationToken.None);
await graph.ExecuteAsync(context);
```

Now go build something! 🚀
