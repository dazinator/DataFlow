# Structured DataFlow Routing

## Overview

The **Routing Feature** for the Structured DataFlow Builder enables dynamic branching of data flows based on item content. Items can be routed to different processing pipelines, where each route is a complete sub-dataflow with its own blocks and configuration.

**Key Features:**
- **Static Routing**: Pre-register routes for known categories
- **Dynamic Routing**: Create routes on-demand using a template
- **Safety Limits**: Optional maximum number of dynamic routes
- **DI Scoping**: Each route gets its own DI scope
- **Complex Routes**: Routes can contain multiple blocks (transforms, batches, processors)
- **Thread-Safe**: Concurrent route creation with proper locking

## Quick Start

### Static Routing

```csharp
using Uniun.DataFlow.Builder.Graph;

var builder = new StructuredDataFlowBuilder(serviceProvider, "OrderProcessing");

// Add source
builder.AddProducer("orders", sp => new OrderProducer());

// Add router with static routes
builder.AddRouter<Order>("router", order => order.Priority)
    .RegisterRoute("high", context =>
    {
        // Build sub-dataflow for high-priority orders
        var routeBuilder = context.RouteBuilder;
        routeBuilder.AddProcessor("urgent-processor", sp => new UrgentOrderProcessor())
            .AsEntry(); // Mark as entry point for routed items
        
        return routeBuilder.Build();
    })
    .RegisterRoute("normal", context =>
    {
        // Build sub-dataflow for normal orders
        var routeBuilder = context.RouteBuilder;
        routeBuilder.AddBatch("batcher", maxBatchSize: 10)
            .AsEntry()
            .AddProcessor("batch-processor", sp => new BatchOrderProcessor());
        
        return routeBuilder.Build();
    })
    .ReceiveFrom("orders");

var flow = builder.Build();
await flow.ExecuteAsync(context);
```

### Dynamic Routing

```csharp
var builder = new StructuredDataFlowBuilder(serviceProvider, "CustomerOrders");

builder.AddProducer("orders", sp => new OrderProducer());

// Add router with dynamic routing
builder.AddRouter<Order>("router", order => order.CustomerId)
    .RegisterRoute("template", context =>
    {
        // This route is used as a template for all customer IDs
        var routeBuilder = context.RouteBuilder;
        var customerId = context.RouteName; // e.g., "CUST001"
        
        routeBuilder.AddProcessor("customer-processor", sp => 
            new CustomerOrderProcessor(customerId))
            .AsEntry();
        
        return routeBuilder.Build();
    })
    .WithDynamicRouting("template", maxDynamicRoutes: 1000) // Limit to 1000 customers
    .ReceiveFrom("orders");

var flow = builder.Build();
await flow.ExecuteAsync(context);
```

## Route Context

When building a route, you receive a `RouteContext` with useful information:

```csharp
.RegisterRoute("my-route", context =>
{
    // context.RouteName - The name of this specific route instance
    // context.RouteDefinitionName - The registered route name (for dynamic routes, this is the template name)
    // context.TriggeringItem - The first item that triggered this route creation (for initialization)
    // context.ServiceProvider - Scoped service provider for this route
    // context.RouteBuilder - Builder for constructing the route sub-dataflow
    // context.IsDesignTime - True when building for design-time (e.g., diagram visualization), false at runtime
    
    var item = (MyType)context.TriggeringItem;
    Console.WriteLine($"Creating route '{context.RouteName}' triggered by {item.Id}");
    
    var routeBuilder = context.RouteBuilder;
    // Build your route...
});
```

### Design-Time vs Runtime

When generating Mermaid diagrams or other design-time operations, routes are built with `IsDesignTime = true` and `TriggeringItem = null`. Route factories should handle this gracefully:

```csharp
.RegisterRoute("my-route", context =>
{
    var routeBuilder = context.RouteBuilder;
    
    // Always add the basic structure
    routeBuilder.AddProcessor("processor", sp => new MyProcessor())
        .AsEntry();
    
    // Only perform item-dependent operations at runtime
    if (!context.IsDesignTime && context.TriggeringItem != null)
    {
        var item = (MyType)context.TriggeringItem;
        // Use item for runtime-specific configuration
        var logger = context.ServiceProvider.GetService<ILogger>();
        logger?.LogInformation("Route created for item {Id}", item.Id);
    }
    
    return routeBuilder.Build();
});
```

**Best Practices:**
- Always check `IsDesignTime` before using `TriggeringItem`
- Keep diagram structure identical to runtime (only skip data-dependent operations)
- Use null-safe operators when accessing `TriggeringItem`

## Complex Routes

Routes can contain any blocks supported by the main builder:

```csharp
builder.AddRouter<Invoice>("router", invoice => invoice.Type)
    .RegisterRoute("standard", context =>
    {
        var routeBuilder = context.RouteBuilder;
        
        // Build a complex processing pipeline
        routeBuilder.AddTransform("validator", sp => new InvoiceValidator())
            .AsEntry() // Entry point
            .AddTransform("enricher", sp => new DataEnricher())
            .AddBatch("batcher", maxBatchSize: 50)
            .AddProcessor("saver", sp => new DatabaseSaver());
        
        return routeBuilder.Build();
    })
    .RegisterRoute("urgent", context =>
    {
        var routeBuilder = context.RouteBuilder;
        
        // Simpler pipeline for urgent invoices
        routeBuilder.AddProcessor("priority-saver", sp => new PrioritySaver())
            .AsEntry();
        
        return routeBuilder.Build();
    })
    .ReceiveFrom("invoices");
```

## Entry Block Requirement

Every route **must** have an entry block - this is where routed items enter the sub-dataflow. **By convention, the first target block you add to a route automatically becomes the entry block**, so you typically don't need to call `.AsEntry()` unless you want to set a different block as the entry point.

```csharp
// ✅ Correct - First target block is automatically the entry block
.RegisterRoute("my-route", context =>
{
    var routeBuilder = context.RouteBuilder;
    routeBuilder.AddProcessor("processor", sp => new MyProcessor());
    // No need for .AsEntry() - this is automatically the entry block
    
    return routeBuilder.Build();
})

// ✅ Also correct - Explicitly set a different entry block
.RegisterRoute("my-route", context =>
{
    var routeBuilder = context.RouteBuilder;
    routeBuilder.AddTransform("transform", sp => new MyTransformer())
        .AddProcessor("processor", sp => new MyProcessor())
        .AsEntry(); // Explicitly set processor as entry (overrides default)
    
    return routeBuilder.Build();
})

// ✅ Also correct - Mark the first block explicitly (redundant but clear)
.RegisterRoute("my-route", context =>
{
    var routeBuilder = context.RouteBuilder;
    routeBuilder.AddProcessor("processor", sp => new MyProcessor())
        .AsEntry(); // Explicitly mark as entry (same as automatic behavior)
    
    return routeBuilder.Build();
})
```

## Route Selector

The route selector determines which route an item takes:

```csharp
// Simple property-based routing
.AddRouter<Order>("router", order => order.Status)

// Computed routing
.AddRouter<Order>("router", order =>
{
    if (order.Amount > 10000) return "high-value";
    if (order.Customer.IsPremium) return "premium";
    return "standard";
})

// Multi-criteria routing
.AddRouter<Order>("router", order => 
    $"{order.Region}-{order.Type}") // e.g., "US-retail" or "EU-wholesale"
```

## Dynamic Routing Details

### Creating Template Routes

Template routes are used as blueprints for dynamically created routes:

```csharp
.RegisterRoute("template", context =>
{
    // context.RouteName will be the actual route name, not "template"
    // context.RouteDefinitionName will be "template"
    // context.TriggeringItem is the first item for this specific route
    
    var routeBuilder = context.RouteBuilder;
    routeBuilder.AddProcessor("processor", sp => 
        new DynamicProcessor(context.RouteName))
        .AsEntry();
    
    return routeBuilder.Build();
})
.WithDynamicRouting("template")
```

### Dynamic Route Lifecycle

1. Item arrives with route name "ABC"
2. Router checks if route "ABC" exists
3. If not found and dynamic routing enabled:
   - Uses "template" route definition
   - Creates new route with name "ABC"
   - Stores in route cache
   - Sends item to new route
4. Future items with "ABC" use cached route

### Safety Limits

Prevent unbounded growth with `maxDynamicRoutes`:

```csharp
// Limit to 100 dynamic routes
.WithDynamicRouting("template", maxDynamicRoutes: 100)

// Unlimited routes (use cautiously!)
.WithDynamicRouting("template") // or maxDynamicRoutes: null
```

When the limit is exceeded, an `InvalidOperationException` is thrown.

## Concurrency and Backpressure

Routing blocks support concurrent processing of items:

```csharp
.AddRouter<Order>("router", order => order.Type,
    options => options.MaxConcurrency = 5) // Process 5 items in parallel
```

**How Concurrency Works:**
- The router block can process multiple items concurrently (up to `MaxConcurrency`)
- Each concurrent "actor" pulls an item, determines its route, and writes it to the route's channel
- Routes themselves execute independently and don't block each other

**Backpressure Considerations:**

The routing block uses bounded channels (capacity = `MaxConcurrency`) for each route to manage backpressure effectively. This means:

1. **Memory Safety**: Routes use small buffers (capacity = MaxConcurrency, minimum 1) to prevent unbounded memory growth
2. **Backpressure Propagation**: If a route's processing is slow and its buffer fills up, routing actors will wait when trying to route items to that route
3. **Cross-Route Effects**: Backpressure in one route can affect routing of items to other routes

**Example Scenario:**
```
MaxConcurrency = 3
Route "A" is experiencing backpressure (slow processing, buffer full)
Route "B" is processing normally

Time T1:
  Actor 1: Gets item for "A" -> waits (backpressure)
  Actor 2: Gets item for "B" -> succeeds, now free
  Actor 3: Gets item for "A" -> waits (backpressure)

Time T2:
  Actor 2: Gets next item for "A" -> waits (backpressure)
  
All 3 actors are now tied up waiting to route to "A". 
No actors available to route items to "B", even though "B" has no backpressure.
This decreases overall throughput.
```

**Data Homogeneity Impact:**
- **Heterogeneous Data** (items distributed across many routes): Backpressure in one route has less impact on overall throughput, especially when MaxConcurrency > 1
- **Homogeneous Data** (many items to same route): Backpressure in that route can significantly reduce throughput
- The effectiveness of concurrency depends on how evenly items are distributed across routes

**Mitigation Strategies:**
1. **Tune MaxConcurrency**: Higher values can help when routes have varying processing speeds
2. **Optimize Route Processing**: Ensure route pipelines are efficient to reduce backpressure
3. **Monitor Route Performance**: Watch for routes causing backpressure
4. **Consider Buffer Blocks** (future): Large capacity buffer blocks as entry points could alleviate backpressure effects

**Best Practices:**
- Start with `MaxConcurrency = 1` for predictable behavior
- Increase concurrency if you have heterogeneous data and want better throughput
- Monitor for backpressure issues before tuning concurrency settings
- Be aware that concurrency benefits depend on your specific routing patterns

## DI Scoping

Each route gets its own DI scope when it's created:

```csharp
services.AddScoped<CustomerContext>();

.RegisterRoute("customer-route", context =>
{
    // The context.ServiceProvider is scoped to this specific route
    // This scope is created when the route is first instantiated
    var customerContext = context.ServiceProvider.GetRequiredService<CustomerContext>();
    customerContext.CustomerId = context.RouteName;
    
    var routeBuilder = context.RouteBuilder;
    routeBuilder.AddProcessor("processor", sp => 
    {
        // The 'sp' here is the same route-scoped ServiceProvider
        // All blocks within this route share the same DI scope
        var ctx = sp.GetRequiredService<CustomerContext>();
        return new OrderProcessor(ctx);
    })
    .AsEntry();
    
    return routeBuilder.Build();
});
```

**Important Notes:**
- Each route has one DI scope that is created when the route is instantiated
- All blocks within a route share that route's DI scope
- Routes are long-lived - they persist for the lifetime of the routing block
- If you have scoped dependencies (like `DbContext`), be careful with blocks that execute concurrently within the route, as they will share the same scoped instance
- The route's scope is disposed when the routing block completes or is disposed

## Error Handling

### Unknown Route (Static Mode)

```csharp
// Only "even" route registered
.AddRouter<int>("router", num => num % 2 == 0 ? "even" : "odd")
.RegisterRoute("even", ...)

// Will throw InvalidOperationException when odd number encountered
```

### Missing Template

```csharp
// Will throw during builder configuration
.WithDynamicRouting("nonexistent-template") // Error: template not registered
```

### Exceeded Dynamic Limit

```csharp
// Will throw InvalidOperationException during execution
.WithDynamicRouting("template", maxDynamicRoutes: 5)
// After 5 routes created, 6th unique route name throws
```

### Route Build Errors

```csharp
.RegisterRoute("my-route", context =>
{
    var routeBuilder = context.RouteBuilder;
    
    // If this throws, the route is not created
    // The scope is disposed
    // The exception is propagated
    throw new Exception("Route initialization failed");
})
```

## Complete Example

```csharp
public class OrderProcessingFlow : IDataFlowConfiguration
{
    public void Configure(DataFlowBuilder builder)
    {
        var structuredBuilder = new StructuredDataFlowBuilder(
            builder.ServiceProvider, 
            "OrderProcessing");
        
        // Source: Read orders from queue
        structuredBuilder.AddProducer("orders", sp => 
            sp.GetRequiredService<OrderQueueReader>());
        
        // Router: Split by region (dynamic) and priority (static)
        structuredBuilder.AddRouter<Order>("region-router", order => order.Region)
            .RegisterRoute("template", context =>
            {
                var region = context.RouteName;
                var routeBuilder = context.RouteBuilder;
                
                // Each region has its own priority router
                routeBuilder.AddRouter<Order>("priority-router", order => order.Priority)
                    .AsEntry()
                    .RegisterRoute("high", priorityContext =>
                    {
                        var rb = priorityContext.RouteBuilder;
                        rb.AddProcessor("urgent-processor", sp =>
                            new UrgentProcessor(region))
                            .AsEntry();
                        return rb.Build();
                    })
                    .RegisterRoute("normal", priorityContext =>
                    {
                        var rb = priorityContext.RouteBuilder;
                        rb.AddBatch("batcher", maxBatchSize: 100)
                            .AsEntry()
                            .AddProcessor("batch-processor", sp =>
                                new BatchProcessor(region));
                        return rb.Build();
                    })
                    .ReceiveFrom("region-router"); // Note: can't chain in route
                
                return routeBuilder.Build();
            })
            .WithDynamicRouting("template", maxDynamicRoutes: 50) // Max 50 regions
            .ReceiveFrom("orders");
        
        // Build and register
        var flow = structuredBuilder.Build();
        builder.RegisterDataFlow(flow);
    }
}
```

## API Reference

### AddRouter

```csharp
IStructuredDataFlowBuilder.AddRouter<T>(
    string name,
    Func<T, string> routeSelector,
    Action<StructuredRoutingBlockOptions<T>>? configureOptions = null)
```

### RegisterRoute

```csharp
StructuredRoutingBlockBuilder<T>.RegisterRoute(
    string routeName,
    Func<RouteContext, IDataFlow> routeFactory)
```

### WithDynamicRouting

```csharp
StructuredRoutingBlockBuilder<T>.WithDynamicRouting(
    string templateRouteName,
    int? maxDynamicRoutes = null)
```

### RouteBuilder Extensions

Route builders support the same API as the main structured builder:

- `AddProcessor<T>(name, factory, options?)`
- `AddTransform<TIn, TOut>(name, factory, options?)`
- `AddBatch<T>(name, maxBatchSize, windowPeriod?, options?)`
- `.AsEntry()` - Mark block as route entry point
- `.ReceiveFrom(sourceBlockName)` - Connect blocks

## Best Practices

1. **Use Static Routing** when you know all possible routes upfront
2. **Set Dynamic Limits** to prevent unbounded memory growth
3. **Consider Cardinality** - Dynamic routing is great for 100s-1000s of routes, not millions
4. **Scope Resources** - Use DI scoping to manage per-route state
5. **Entry Validation** - Always mark an entry block with `.AsEntry()`
6. **Error Handling** - Handle exceptions in route factories gracefully
7. **Test Thoroughly** - Test both route creation and route processing logic

## Performance Considerations

- **Route Creation**: Has locking overhead on first encounter
- **Route Lookup**: Very fast (concurrent dictionary)
- **Memory**: Each route has overhead (dataflow + blocks + scope)
- **Concurrency**: Routes execute in parallel automatically
- **Dynamic Limit**: Prevents memory issues with high-cardinality routing keys
