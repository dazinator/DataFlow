# Working with Blocks

**Audience**: Developers familiar with DataFlow basics  
**Prerequisites**: Complete [Getting Started Guide](./getting-started.md)  
**Time**: 15-20 minutes

---

## Table of Contents

1. [Block Types Overview](#block-types-overview)
2. [Producer Blocks](#producer-blocks)
3. [Actor Blocks](#actor-blocks)
4. [Transform vs Process](#transform-vs-process)
5. [Block Registration Patterns](#block-registration-patterns)
6. [Block Helpers](#block-helpers)
7. [Best Practices](#best-practices)

---

## Block Types Overview

DataFlow provides several block types, each serving a specific purpose in your pipeline:

| Block Type | Purpose | Input | Output | Use Case |
|------------|---------|-------|--------|----------|
| **Producer** | Generate data | None | Stream | Data sources (DB, file, API) |
| **Actor (Transform)** | Transform data | Stream | Stream | Map, filter, enrich data |
| **Actor (Processor)** | Consume data | Stream | None | Write to DB, send emails, log |
| **Batch** | Accumulate items | Stream | Batches | Bulk operations |

**Key Concept**: All blocks (except Producer) use the **Actor pattern** for DI-aware, scoped execution.

---

## Producer Blocks

Producers are the source of data in your pipeline. They generate items without consuming input.

### Creating a Simple Producer

```csharp
using DataFlow.POC.Tests.TestHelpers;

// Synchronous data
var numbers = BlockHelpers.CreateProducer<int>("numbers", ctx =>
{
    for (int i = 1; i <= 10; i++)
        yield return i;
});

// Asynchronous data
var linesFromFile = BlockHelpers.CreateProducer<string>("file-reader", async ctx =>
{
    using var reader = new StreamReader("data.txt");
    string? line;
    while ((line = await reader.ReadLineAsync()) != null)
    {
        yield return line;
    }
});
```

### Producer from Database

```csharp
var ordersProducer = BlockHelpers.CreateProducer<Order>("orders", async ctx =>
{
    // Note: Get DbContext from the execution context
    var dbContext = ctx.ServiceProvider.GetRequiredService<OrderDbContext>();
    
    var orders = await dbContext.Orders
        .Where(o => o.Status == "Pending")
        .ToListAsync(ctx.CancellationToken);
    
    foreach (var order in orders)
        yield return order;
});
```

### Producer Best Practices

✅ **DO**:
- Always respect `ctx.CancellationToken`
- Complete the stream (don't yield infinitely unless intentional)
- Handle exceptions gracefully

❌ **DON'T**:
- Create infinite streams without a way to stop
- Ignore cancellation tokens
- Throw exceptions without handling

---

## Actor Blocks

Actors are the workhorses of DataFlow. They process streams of data with automatic DI scope management.

### Basic Actor: Transform

Transforms convert input to output:

```csharp
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

// Create the block
var transformer = BlockHelpers.CreateActor<string, string, UppercaseActor>(
    "uppercase",
    new UppercaseActor());
```

### Actor with Dependencies

Actors can have constructor dependencies that are resolved from DI:

```csharp
public class OrderValidatorActor : IStreamActor<Order, Order>
{
    private readonly ILogger<OrderValidatorActor> _logger;
    
    public OrderValidatorActor(ILogger<OrderValidatorActor> logger)
    {
        _logger = logger;
    }
    
    public async IAsyncEnumerable<Order> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            _logger.LogInformation("Validating order {OrderId}", order.Id);
            
            if (order.Total > 0)
            {
                yield return order;
            }
            else
            {
                _logger.LogWarning("Invalid order {OrderId}: Total must be > 0", order.Id);
            }
        }
    }
}

// Register with DI
services.AddScoped<OrderValidatorActor>();

// Create block using IServiceScopeFactory
services.AddDataFlows("global", df =>
{
    df.AddBlock("validator", sp =>
    {
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return BlockHelpers.CreateActor<Order, Order, OrderValidatorActor>(
            "validator", 
            scopeFactory);
    });
});
```

### Generic Transform Actor

For simple transformations, use the built-in `TransformActor<TIn, TOut>`:

```csharp
using DataFlow.POC.Tests.TestHelpers;

// Simple function-based transform
var multiplier = new TransformActor<int, int>(x => x * 2);

var block = BlockHelpers.CreateActor<int, int, TransformActor<int, int>>(
    "multiplier",
    multiplier);
```

---

## Transform vs Process

Understanding when to transform vs process is key to building effective pipelines.

### Transform Blocks

**Purpose**: Convert input to different output (maintains stream)

**Characteristics**:
- Input and output are different types (or same type, transformed)
- Always yields output for further processing
- Can be chained with other blocks

```csharp
// Transform: Order → OrderDto
public class OrderToDtoActor : IStreamActor<Order, OrderDto>
{
    public async IAsyncEnumerable<OrderDto> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            yield return new OrderDto
            {
                Id = order.Id,
                Total = order.Total,
                CustomerName = order.CustomerName
            };
        }
    }
}
```

### Processor Blocks

**Purpose**: Terminal consumer (end of pipeline)

**Characteristics**:
- Consumes input but doesn't yield output (or yields `object` as placeholder)
- Typically performs side effects (write to DB, send email, log)
- Usually the last block in a pipeline

```csharp
// Process: Write to database
public class OrderSaverActor : IStreamActor<Order, object>
{
    private readonly OrderDbContext _dbContext;
    
    public OrderSaverActor(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            // Processor doesn't yield output
        }
    }
}
```

### When to Use Each

| Use Transform When | Use Processor When |
|--------------------|-------------------|
| You need to pass data downstream | This is the end of the pipeline |
| You're mapping/enriching data | You're writing to storage |
| You're filtering items | You're sending notifications |
| The output will be used by another block | You're logging/auditing |

---

## Block Registration Patterns

There are several ways to register and create blocks. Choose the pattern that fits your use case.

### Pattern 1: Direct Instantiation (Simple Cases)

For simple blocks without dependencies:

```csharp
var transformer = BlockHelpers.CreateActor<string, string, UppercaseActor>(
    "uppercase",
    new UppercaseActor());
```

**When to use**: Simple actors with no dependencies, prototyping

### Pattern 2: DI Registration with UseBlock (Recommended)

Register blocks with names, then reference them when building graphs:

```csharp
services.AddDataFlows("global", df =>
{
    // Register actors as scoped services
    df.AddActorBlock<Order, Order, OrderValidatorActor>("validator");
    df.AddActorBlock<Order, OrderDto, OrderToDtoActor>("mapper");
    
    // Build graph using names
    df.AddGraph("process-orders", g =>
    {
        g.UseBlock("validator")
         .UseBlock("mapper")
         .Connect("validator", "mapper");
    });
});
```

**When to use**: Production code, complex graphs, reusable blocks

**Benefits**:
- Cleaner graph building code
- Blocks registered once, used many times
- Easy to test individual blocks
- Clear separation between block creation and graph topology

### Pattern 3: Factory Function (Complex Dependencies)

For blocks that need special initialization:

```csharp
services.AddDataFlows("global", df =>
{
    df.AddBlock("complex-block", sp =>
    {
        var logger = sp.GetRequiredService<ILogger<MyActor>>();
        var config = sp.GetRequiredService<IConfiguration>();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        
        var actor = new MyActor(logger, config.GetValue<string>("SomeSetting"));
        
        return BlockHelpers.CreateActor<int, string, MyActor>(
            "complex-block",
            scopeFactory);
    });
});
```

**When to use**: Blocks with complex initialization, configuration-dependent actors

### Pattern 4: Namespace Isolation (Modular Monolith)

For multi-tenant or modular applications:

```csharp
// Tenant A
services.AddDataFlows("tenant-a", df =>
{
    df.AddActorBlock<Order, Order, OrderProcessorActor>("processor");
    df.AddGraph("main", g => { /* ... */ });
});

// Tenant B
services.AddDataFlows("tenant-b", df =>
{
    df.AddActorBlock<Order, Order, OrderProcessorActor>("processor");
    df.AddGraph("main", g => { /* ... */ });
});

// Resolve by namespace
var graphA = serviceProvider.GetKeyedService<DataFlowGraph>("tenant-a:main");
var graphB = serviceProvider.GetKeyedService<DataFlowGraph>("tenant-b:main");
```

**When to use**: Multi-tenant applications, modular monoliths, isolated contexts

---

## Block Helpers

The `BlockHelpers` class provides convenient factory methods for creating blocks.

### CreateProducer

```csharp
// Synchronous
var producer = BlockHelpers.CreateProducer<int>("source", ctx =>
{
    yield return 1;
    yield return 2;
    yield return 3;
});

// Asynchronous
var asyncProducer = BlockHelpers.CreateProducer<string>("async-source", async ctx =>
{
    await Task.Delay(100);
    yield return "item1";
    await Task.Delay(100);
    yield return "item2";
});
```

### CreateActor

```csharp
// With instance
var actor = BlockHelpers.CreateActor<int, string, MyActor>(
    "my-actor",
    new MyActor());

// With IServiceScopeFactory (for DI)
var diActor = BlockHelpers.CreateActor<int, string, MyActor>(
    "di-actor",
    scopeFactory);
```

### Why Use BlockHelpers?

- ✅ Consistent block creation
- ✅ Handles common patterns
- ✅ Type-safe
- ✅ Reduces boilerplate

---

## Best Practices

### 1. Use Scoped Lifetime for Blocks

Blocks should always be scoped (not singleton or transient):

```csharp
// ✅ Good: Default is scoped
services.AddDataFlows("global", df =>
{
    df.AddActorBlock<Order, Order, OrderProcessorActor>("processor");
});

// ❌ Bad: Don't register blocks as singleton
// (This can cause state conflicts between executions)
```

**Why?** Blocks have stateful execution semantics. Scoped lifetime ensures isolation between graph executions.

### 2. Name Blocks Meaningfully

Use descriptive names that reflect the block's purpose:

```csharp
// ✅ Good: Clear purpose
df.AddActorBlock<Order, Order, ValidateOrderActor>("order-validator");
df.AddActorBlock<Order, OrderDto, MapToDto>("order-mapper");

// ❌ Bad: Unclear names
df.AddActorBlock<Order, Order, ValidateOrderActor>("block1");
df.AddActorBlock<Order, OrderDto, MapToDto>("b2");
```

### 3. Keep Actors Focused

Each actor should have a single responsibility:

```csharp
// ✅ Good: Focused actors
public class ValidateOrderActor { /* only validates */ }
public class EnrichOrderActor { /* only enriches */ }
public class SaveOrderActor { /* only saves */ }

// ❌ Bad: Does too much
public class OrderProcessorActor
{
    // Validates, enriches, AND saves - too much!
}
```

### 4. Always Respect Cancellation Tokens

```csharp
public async IAsyncEnumerable<T> RunAsync(
    IAsyncEnumerable<T> input,
    IActorExecutionContext context)
{
    // ✅ Good: Respect cancellation
    await foreach (var item in input.WithCancellation(context.CancellationToken))
    {
        // Process item
        yield return item;
    }
}
```

### 5. Use Generic Actors for Simple Transformations

Don't create custom actors for simple transforms:

```csharp
// ❌ Bad: Unnecessary custom actor
public class MultiplyByTwoActor : IStreamActor<int, int>
{
    public async IAsyncEnumerable<int> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return item * 2;
        }
    }
}

// ✅ Good: Use generic TransformActor
var multiplier = new TransformActor<int, int>(x => x * 2);
```

### 6. Prefer Constructor Injection

Let DI resolve your dependencies:

```csharp
// ✅ Good: Dependencies injected
public class OrderValidatorActor : IStreamActor<Order, Order>
{
    private readonly ILogger<OrderValidatorActor> _logger;
    private readonly IOrderValidator _validator;
    
    public OrderValidatorActor(
        ILogger<OrderValidatorActor> logger,
        IOrderValidator validator)
    {
        _logger = logger;
        _validator = validator;
    }
    
    // ...
}

// ❌ Bad: Service location
public class OrderValidatorActor : IStreamActor<Order, Order>
{
    public async IAsyncEnumerable<Order> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        // Don't do this - use constructor injection instead
        var logger = context.ServiceProvider.GetRequiredService<ILogger>();
        // ...
    }
}
```

---

## Common Patterns

### Pattern: Filter Actor

```csharp
public class FilterActor<T> : IStreamActor<T, T>
{
    private readonly Func<T, bool> _predicate;
    
    public FilterActor(Func<T, bool> predicate)
    {
        _predicate = predicate;
    }
    
    public async IAsyncEnumerable<T> RunAsync(
        IAsyncEnumerable<T> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            if (_predicate(item))
                yield return item;
        }
    }
}

// Usage
var filter = new FilterActor<Order>(order => order.Total > 100);
```

### Pattern: Batch Processor

```csharp
public class BatchSaverActor : IStreamActor<Order[], object>
{
    private readonly OrderDbContext _dbContext;
    
    public BatchSaverActor(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<Order[]> input,
        IActorExecutionContext context)
    {
        await foreach (var batch in input.WithCancellation(context.CancellationToken))
        {
            _dbContext.Orders.AddRange(batch);
            await _dbContext.SaveChangesAsync(context.CancellationToken);
        }
    }
}
```

### Pattern: Enrichment Actor

```csharp
public class OrderEnrichmentActor : IStreamActor<Order, EnrichedOrder>
{
    private readonly ICustomerService _customerService;
    
    public OrderEnrichmentActor(ICustomerService customerService)
    {
        _customerService = customerService;
    }
    
    public async IAsyncEnumerable<EnrichedOrder> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            var customer = await _customerService.GetCustomerAsync(
                order.CustomerId, 
                context.CancellationToken);
            
            yield return new EnrichedOrder
            {
                Order = order,
                CustomerName = customer.Name,
                CustomerTier = customer.Tier
            };
        }
    }
}
```

---

## Next Steps

Now that you understand blocks, explore:

1. **[Topology Guides](./topology-broadcast.md)** - Learn about broadcast, competing consumers, and routing
2. **[Testing Guide](./testing-guide.md)** - Test your blocks effectively
3. **[Business Logic Decoupling](./business-logic-decoupling.md)** - Separate logic from actors

Or dive into advanced features:

4. **[Using Epochs](./using-epochs.md)** - Transaction boundaries
5. **[Epoch Actor Block](./epoch-actor-block.md)** - Scope rotation
6. **[Source Blocks](./source-blocks.md)** - Creating custom sources

---

## Summary

You've learned:

- ✅ Different block types and when to use them
- ✅ How to create producers and actors
- ✅ The difference between transform and processor blocks
- ✅ Various block registration patterns
- ✅ Best practices for building blocks

**Next**: Explore [Broadcast Topology](./topology-broadcast.md) to learn about fan-out patterns.
