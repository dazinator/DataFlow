# Selective Content-Based Routing Guide

**Last Updated**: 2025-11-25  
**Difficulty**: Intermediate  
**Prerequisites**: [Getting Started](getting-started.md), [Working with Blocks](working-with-blocks.md)

---

## Overview

**Selective Routing** allows you to route items to specific target blocks based on item content inspection. Each item goes **only to its matching route**, providing efficient content-based routing with zero overhead.

### What You'll Learn

- How selective routing works with route selectors
- Performance characteristics and zero-overhead design
- When to use selective routing vs other patterns
- Real-world examples of content-based routing
- Advanced multi-way routing scenarios

---

## Table of Contents

1. [Core Concept](#core-concept)
2. [Your First Selective Routing Flow](#your-first-selective-routing-flow)
3. [How It Works](#how-it-works)
4. [Performance Characteristics](#performance-characteristics)
5. [Route Selector Functions](#route-selector-functions)
6. [Handling Unknown Routes](#handling-unknown-routes)
7. [When to Use Selective Routing](#when-to-use-selective-routing)
8. [Real-World Examples](#real-world-examples)
9. [Advanced Patterns](#advanced-patterns)
10. [Routes Not Known at Build Time](#routes-not-known-at-build-time)
11. [Troubleshooting](#troubleshooting)
12. [Next Steps](#next-steps)

---

## Core Concept

### The Problem: All-or-Nothing Routing

Without selective routing, you either:
1. **Broadcast to all** (every consumer gets every item, lots of waste)
2. **Compete randomly** (can't control which worker gets which item)

```
❌ Broadcast: High/Normal orders both go to both processors
Source → High-Priority Processor (filters out normal orders - waste!)
      ↓
      → Normal Processor (filters out high orders - waste!)

❌ Competing: Random distribution, can't guarantee priority handling
Source → [Shared Channel] → Random processor (might be wrong one!)
```

### The Solution: Content-Based Routing

Route each item **only** to the target that should handle it:

```
✅ Selective: Each order goes to correct processor
Source ─┬→ High-Priority Processor   (only high-priority orders)
        └→ Normal Processor           (only normal orders)
```

### Key Characteristics

- ✅ Routes based on **item content**
- ✅ **Zero overhead** (no wrapper records, O(1) lookup)
- ✅ Each item sent **only to matching route**
- ✅ Scales efficiently with route count
- ✅ **No filtering waste** (unlike broadcast-and-filter)
- ⚠️ Routes must be **defined at build time** (not dynamic in POC)

---

## Your First Selective Routing Flow

Let's route orders based on priority:

```csharp
// Step 1: Register blocks for each route
builder.Services.AddDataFlows("orders", df =>
{
    // Register source
    df.AddSourceBlock<Order, OrderSource>("order-source");
    
    // Register route-specific processors
    df.AddActorBlock<Order, Order, ExpressProcessor>("express-processor");
    df.AddActorBlock<Order, Order, StandardProcessor>("standard-processor");
    
    // Build graph with selective routing
    df.AddGraph("priority-routing", g =>
    {
        g.UseBlock("order-source")
         .RouteBy(order => order.Priority)  // Route selector function
         .To("express-processor", priority => priority == "high")
         .To("standard-processor", priority => priority == "normal");
    });
});

// Step 2: Implement the order model
public class Order
{
    public string Id { get; set; }
    public string Priority { get; set; }  // "high" or "normal"
    public decimal Amount { get; set; }
}

// Step 3: Implement route-specific processors
public class ExpressProcessor : IActor<Order, Order>
{
    public async Task<Order> ProcessAsync(Order order, CancellationToken ct)
    {
        // Express handling - same-day shipping, priority queue
        await ShipExpressAsync(order, ct);
        return order;
    }
}

public class StandardProcessor : IActor<Order, Order>
{
    public async Task<Order> ProcessAsync(Order order, CancellationToken ct)
    {
        // Standard handling - batched shipping
        await ShipStandardAsync(order, ct);
        return order;
    }
}
```

**What happens**: When an order arrives, the routing function inspects `order.Priority`. If it's "high", the order goes to `express-processor`. If it's "normal", it goes to `standard-processor`. Each order is routed exactly once.

---

## How It Works

### Route Selector and Mapping

Selective routing uses two key components:

1. **Route Selector Function**: Inspects item and returns a route key
2. **Route Mapping**: Maps route keys to target blocks

```csharp
// Conceptual implementation
public class SelectiveRoutingEdgeStrategy<TItem> : EdgeStrategy
{
    private readonly Dictionary<string, IBlock> _routeKeyToBlock;
    private readonly Func<TItem, string> _routeSelector;
    
    public SelectiveRoutingEdgeStrategy(
        Dictionary<string, IBlock> routeKeyToBlock,
        Func<TItem, string> routeSelector)
    {
        _routeKeyToBlock = routeKeyToBlock;
        _routeSelector = routeSelector;
    }
    
    public override async Task RouteTypedItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        // Cast to actual item type
        var typedItem = (TItem)(object)item!;
        
        // Get route key from item (e.g., "high" or "normal")
        var routeKey = _routeSelector(typedItem);
        
        // Look up target block - O(1) dictionary lookup
        if (!_routeKeyToBlock.TryGetValue(routeKey, out var targetBlock))
        {
            throw new InvalidOperationException($"Unknown route key: {routeKey}");
        }
        
        // Write ONLY to matching channel (not broadcast!)
        var writer = typedWriters[targetBlock];
        await writer.WriteAsync(item, cancellationToken);
    }
}
```

### Channel Creation

Each route gets its own dedicated channel:

```csharp
public override (Writers, Readers) CreateTypedChannels(...)
{
    var writers = new Dictionary<IBlock, object>();
    var readers = new Dictionary<IBlock, object>();
    
    // Create one channel per route (not shared like competing!)
    foreach (var targetBlock in targetBlocks)
    {
        var (writer, reader) = TypedChannelFactory.CreateTypedChannel(
            dataType,
            BufferMode.Bounded,
            capacity: 100,
            singleReader: true,
            singleWriter: true);
        
        writers[targetBlock] = writer;
        readers[targetBlock] = reader;
    }
    
    return (writers, readers);
}
```

**Key insight**: Unlike competing consumers (shared channel) or broadcast (concurrent writes), selective routing writes to exactly one channel per item based on content.

---

## Performance Characteristics

### Zero Overhead Design

Selective routing is designed for high performance:

```
Per-Item Cost:
1. Route selector function call: ~1-5 ns (property access)
2. Dictionary lookup: O(1) ~10-20 ns
3. Channel write: ~100-200 ns
Total: ~111-225 ns per item
```

**No Allocations**:
- Works directly with your item type (no wrapper records)
- No intermediate objects created
- No array allocations for route lists

### Comparison with Broadcast-and-Filter

**Selective Routing** (modern approach):
```csharp
Source ─┬→ High Processor    (only high items - 1 write)
        └→ Normal Processor  (only normal items - 1 write)

Cost per item: 1 channel write
```

**Broadcast-and-Filter** (obsolete pattern):
```csharp
Source ─┬→ High Processor    (filter: keep high, discard normal - 1 write + filter)
        └→ Normal Processor  (filter: keep normal, discard high - 1 write + filter)

Cost per item: N concurrent writes + N filter checks (wasted work!)
```

| Metric | Selective Routing | Broadcast-and-Filter |
|--------|------------------|---------------------|
| Writes per item | 1 | N (N = route count) |
| Filter operations | 0 | N |
| Allocations | 0 | 0 |
| CPU per item | Minimal (O(1) lookup) | N× more (concurrent writes + filters) |

**Result**: Selective routing is ~N× more efficient for routing scenarios.

### Scaling with Routes

| Route Count | Throughput Impact | Memory Impact |
|-------------|------------------|---------------|
| 2 routes | Baseline | 2 channels |
| 5 routes | Same | 5 channels |
| 10 routes | Same | 10 channels |
| 50 routes | Same | 50 channels |

**Key insight**: Performance doesn't degrade with route count (O(1) lookup).

---

## Route Selector Functions

### Simple Property-Based Routing

```csharp
// Route by a single property
.RouteBy(order => order.Priority)
.To("high-processor", p => p == "high")
.To("normal-processor", p => p == "normal")
```

### Complex Conditional Routing

```csharp
// Route based on multiple conditions
.RouteBy(order => 
{
    if (order.Amount > 10000 && order.Priority == "high")
        return "vip";
    else if (order.Priority == "high")
        return "express";
    else
        return "standard";
})
.To("vip-processor", key => key == "vip")
.To("express-processor", key => key == "express")
.To("standard-processor", key => key == "standard")
```

### Region-Based Routing

```csharp
// Route by geographic region
.RouteBy(customer => customer.Region)
.To("us-processor", region => region == "US")
.To("eu-processor", region => region == "EU")
.To("apac-processor", region => region == "APAC")
```

### Type-Based Routing

```csharp
// Route by object type or category
.RouteBy(product => product.Category)
.To("electronics-processor", cat => cat == "Electronics")
.To("clothing-processor", cat => cat == "Clothing")
.To("food-processor", cat => cat == "Food")
```

### Computed Key Routing

```csharp
// Route by computed/derived value
.RouteBy(customer => 
{
    var lifetimeValue = customer.TotalPurchases * customer.YearsActive;
    return lifetimeValue > 50000 ? "platinum" :
           lifetimeValue > 10000 ? "gold" :
           "standard";
})
.To("platinum-service", tier => tier == "platinum")
.To("gold-service", tier => tier == "gold")
.To("standard-service", tier => tier == "standard")
```

---

## Handling Unknown Routes

### The Problem

What happens if your route selector returns a key that's not in the route mapping?

```csharp
var routeMapping = new Dictionary<string, IBlock>
{
    ["high"] = highProcessor,
    ["normal"] = normalProcessor
    // What if an order has Priority = "urgent"?
};

var routingStrategy = new SelectiveRoutingEdgeStrategy<Order>(
    routeKeyToBlock: routeMapping,
    routeSelector: order => order.Priority);  // Might return "urgent"!

// Result: InvalidOperationException: "Unknown route key: urgent"
```

### Solution 1: Default Route

Provide a default route for unmatched cases:

```csharp
var routeMapping = new Dictionary<string, IBlock>
{
    ["high"] = highProcessor,
    ["normal"] = normalProcessor,
    ["unknown"] = defaultProcessor  // Catch-all route
};

var routingStrategy = new SelectiveRoutingEdgeStrategy<Order>(
    routeKeyToBlock: routeMapping,
    routeSelector: order => order.Priority ?? "unknown");  // Safe!
```

### Solution 2: Normalize in Selector

Ensure selector always returns a valid key:

```csharp
var routingStrategy = new SelectiveRoutingEdgeStrategy<Order>(
    routeKeyToBlock: routeMapping,
    routeSelector: order =>
    {
        var priority = order.Priority?.ToLowerInvariant();
        
        return priority switch
        {
            "high" => "high",
            "normal" => "normal",
            _ => "normal"  // Default to normal for unknown values
        };
    });
```

### Solution 3: Validate Upstream

Ensure data is clean before routing:

```csharp
df.AddGraph("validated-routing", g =>
{
    g.UseBlock("order-source")
     .ProcessWith("validator")  // Ensures Priority is always "high" or "normal"
     .RouteBy(order => order.Priority)
     .To("high-processor", p => p == "high")
     .To("normal-processor", p => p == "normal");
});

public class ValidatorActor : IActor<Order, Order>
{
    public async Task<Order> ProcessAsync(Order order, CancellationToken ct)
    {
        // Normalize priority
        order.Priority = order.Priority switch
        {
            "urgent" or "high" => "high",
            _ => "normal"
        };
        
        return order;
    }
}
```

---

## When to Use Selective Routing

### ✅ Good Use Cases

1. **Priority-Based Routing**
   ```csharp
   .RouteBy(order => order.Priority)
   .To("express", p => p == "high")
   .To("standard", p => p == "normal")
   ```

2. **Customer Segmentation**
   ```csharp
   .RouteBy(customer => customer.Tier)
   .To("vip-processor", tier => tier == "VIP")
   .To("regular-processor", tier => tier == "Regular")
   .To("trial-processor", tier => tier == "Trial")
   ```

3. **Geographic Routing**
   ```csharp
   .RouteBy(request => request.Region)
   .To("us-handler", r => r == "US")
   .To("eu-handler", r => r == "EU")
   .To("apac-handler", r => r == "APAC")
   ```

4. **Content Type Routing**
   ```csharp
   .RouteBy(message => message.Type)
   .To("email-sender", t => t == "Email")
   .To("sms-sender", t => t == "SMS")
   .To("push-sender", t => t == "Push")
   ```

5. **Error vs Success Routing**
   ```csharp
   .RouteBy(result => result.IsSuccess ? "success" : "error")
   .To("success-handler", status => status == "success")
   .To("error-handler", status => status == "error")
   ```

### ❌ Bad Use Cases

1. **Load Balancing (No Content Inspection)**
   ```csharp
   // ❌ Wrong: Selective routing not needed
   .RouteBy(order => /* random selection? */)
   
   // ✅ Better: Use competing consumers
   .CompeteWith(new[] { "worker-1", "worker-2", "worker-3" })
   ```

2. **All Consumers Need All Items**
   ```csharp
   // ❌ Wrong: Can't send one item to multiple routes with selective routing
   .RouteBy(data => /* ??? */)
   
   // ✅ Better: Use broadcast
   .BroadcastTo(new[] { "processor", "logger", "metrics" })
   ```

3. **Dynamic Routes (Created at Runtime)**
   ```csharp
   // ❌ Not supported in POC: Routes must be defined at build time
   .RouteBy(item => item.DynamicRouteKey)  // Won't work if routes added later
   
   // ✅ In POC: All routes must be known when building graph
   ```

---

## Real-World Examples

### Example 1: E-Commerce Order Routing

Route orders to different fulfillment centers based on customer location:

```csharp
builder.Services.AddDataFlows("fulfillment", df =>
{
    df.AddSourceBlock<Order, OrderSource>("order-source");
    df.AddActorBlock<Order, Order, WestCoastFulfillment>("west-coast");
    df.AddActorBlock<Order, Order, EastCoastFulfillment>("east-coast");
    df.AddActorBlock<Order, Order, MidwestFulfillment>("midwest");
    
    df.AddGraph("route-to-fulfillment", g =>
    {
        g.UseBlock("order-source")
         .RouteBy(order => DetermineRegion(order.ShippingAddress.State))
         .To("west-coast", region => region == "west")
         .To("east-coast", region => region == "east")
         .To("midwest", region => region == "midwest");
    });
});

private static string DetermineRegion(string state)
{
    return state switch
    {
        "CA" or "OR" or "WA" => "west",
        "NY" or "MA" or "FL" => "east",
        _ => "midwest"
    };
}
```

### Example 2: Support Ticket Routing

Route support tickets to specialized teams:

```csharp
builder.Services.AddDataFlows("support", df =>
{
    df.AddSourceBlock<Ticket, TicketSource>("ticket-source");
    df.AddActorBlock<Ticket, Ticket, TechnicalSupport>("technical-team");
    df.AddActorBlock<Ticket, Ticket, BillingSupport>("billing-team");
    df.AddActorBlock<Ticket, Ticket, GeneralSupport>("general-team");
    
    df.AddGraph("route-tickets", g =>
    {
        g.UseBlock("ticket-source")
         .RouteBy(ticket => ticket.Category)
         .To("technical-team", cat => cat == "Technical")
         .To("billing-team", cat => cat == "Billing")
         .To("general-team", cat => cat == "General");
    });
});
```

### Example 3: Data Processing Pipeline

Route data to different processors based on size and complexity:

```csharp
builder.Services.AddDataFlows("data-processing", df =>
{
    df.AddSourceBlock<DataFile, FileSource>("file-source");
    df.AddActorBlock<DataFile, ProcessedData, SmallFileProcessor>("small-processor");
    df.AddActorBlock<DataFile, ProcessedData, LargeFileProcessor>("large-processor");
    df.AddActorBlock<DataFile, ProcessedData, ComplexProcessor>("complex-processor");
    
    df.AddGraph("process-files", g =>
    {
        g.UseBlock("file-source")
         .RouteBy(file => ClassifyFile(file))
         .To("small-processor", type => type == "small")
         .To("large-processor", type => type == "large")
         .To("complex-processor", type => type == "complex");
    });
});

private static string ClassifyFile(DataFile file)
{
    if (file.SizeBytes > 100_000_000)
        return "large";
    if (file.RequiresSpecialProcessing)
        return "complex";
    return "small";
}
```

---

## Advanced Patterns

### Pattern 1: Multi-Stage Routing

Route, process, then route again based on results:

```csharp
builder.Services.AddDataFlows("multi-stage", df =>
{
    df.AddSourceBlock<Order, OrderSource>("orders");
    df.AddActorBlock<Order, ValidationResult, ValidatorActor>("validator");
    df.AddActorBlock<ValidationResult, Order, SuccessProcessor>("success-processor");
    df.AddActorBlock<ValidationResult, Order, RetryProcessor>("retry-processor");
    df.AddActorBlock<ValidationResult, Order, FailureProcessor>("failure-processor");
    
    df.AddGraph("validate-and-route", g =>
    {
        // Stage 1: All orders go to validator
        g.UseBlock("orders")
         .ProcessWith("validator")
         // Stage 2: Route based on validation result
         .RouteBy(result => result.Status)
         .To("success-processor", status => status == "Success")
         .To("retry-processor", status => status == "RetryableError")
         .To("failure-processor", status => status == "PermanentFailure");
    });
});
```

### Pattern 2: Routing with Fallback

Route to specialized processors with a fallback:

```csharp
builder.Services.AddDataFlows("specialized", df =>
{
    df.AddSourceBlock<Product, ProductSource>("products");
    df.AddActorBlock<Product, Product, ElectronicsProcessor>("electronics");
    df.AddActorBlock<Product, Product, ClothingProcessor>("clothing");
    df.AddActorBlock<Product, Product, FoodProcessor>("food");
    df.AddActorBlock<Product, Product, GeneralProcessor>("general");
    
    df.AddGraph("categorize-products", g =>
    {
        g.UseBlock("products")
         .RouteBy(product => product.Category ?? "general")
         .To("electronics", cat => cat == "Electronics")
         .To("clothing", cat => cat == "Clothing")
         .To("food", cat => cat == "Food")
         .To("general", cat => cat == "general");  // Fallback
    });
});
```

### Pattern 3: Combining Selective Routing + Competing

Route by content, then compete within each route:

```csharp
builder.Services.AddDataFlows("hybrid", df =>
{
    df.AddSourceBlock<Order, OrderSource>("orders");
    df.AddBufferBlock<Order>("high-buffer", 100);
    df.AddBufferBlock<Order>("normal-buffer", 100);
    
    // High priority: 4 workers
    df.AddActorBlock<Order, Order, ExpressProcessor>("high-1");
    df.AddActorBlock<Order, Order, ExpressProcessor>("high-2");
    df.AddActorBlock<Order, Order, ExpressProcessor>("high-3");
    df.AddActorBlock<Order, Order, ExpressProcessor>("high-4");
    
    // Normal priority: 2 workers
    df.AddActorBlock<Order, Order, StandardProcessor>("normal-1");
    df.AddActorBlock<Order, Order, StandardProcessor>("normal-2");
    
    df.AddGraph("priority-with-load-balancing", g =>
    {
        // Step 1: Route to buffers by priority
        g.UseBlock("orders")
         .RouteBy(order => order.Priority)
         .To("high-buffer", p => p == "high")
         .To("normal-buffer", p => p == "normal");
        
        // Step 2: Compete within each priority tier
        g.UseBlock("high-buffer")
         .CompeteWith(new[] { "high-1", "high-2", "high-3", "high-4" });
        
        g.UseBlock("normal-buffer")
         .CompeteWith(new[] { "normal-1", "normal-2" });
    });
});
```

---

## Routes Not Known at Build Time

The POC library's graph topology is **static** — all blocks and connections must be declared at
DI registration time, before the application starts processing data. This is an intentional
architectural constraint that provides predictable, observable, and safe graphs.

However, some use cases appear to require routes that are only known at runtime — for example,
routing to different ERP systems where new tenants can be added while the application is running.
This section explains how to handle these scenarios.

### Re-Framing the Problem: Type vs. Tenant

The most important step is to distinguish between two different kinds of "new route":

| Kind | Example | What it means |
|------|---------|---------------|
| **New integration type** | Adding Oracle support | Requires a new handler implementation — code change + restart |
| **New tenant instance** | Customer adds a new SAP environment | Only configuration changes — handled via `IOptionsSnapshot<T>` |

In most cases, what appears to be a dynamic routing problem is actually a **new tenant instance
of an existing integration type**. The routing logic is the same; only the configuration values
differ. This does not require a new graph route.

### Solution: Generic Handler with DI Polymorphism (Recommended)

For the common case where routing destinations are different instances of the same integration
type, use a **single actor block** that resolves the correct handler from DI by system name:

```csharp
/// <summary>
/// Single actor block that handles ALL ERP system types.
/// New tenants don't require new blocks or routes — only configuration changes.
/// </summary>
public class ErpPostingActor : IActor<SystemItem, PostingResult>
{
    private readonly IReadOnlyDictionary<string, INamedErpHandler> _handlers;
    private readonly INamedErpHandler _unroutedHandler;

    public ErpPostingActor(
        IEnumerable<INamedErpHandler> handlers,   // All registered ERP types
        UnroutedErpHandler unroutedHandler)
    {
        _handlers = handlers.ToDictionary(h => h.SystemName);
        _unroutedHandler = unroutedHandler;
    }

    public async IAsyncEnumerable<PostingResult> ProcessAsync(
        SystemItem item,
        IActorExecutionContext ctx,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Resolve handler by ERP type name from the item
        var handler = item.SystemName != null && _handlers.TryGetValue(item.SystemName, out var h)
            ? h : _unroutedHandler;

        await foreach (var result in handler.PostAsync(item, ct))
            yield return result;
    }
}

// Register one handler per ERP INTEGRATION TYPE (not per tenant)
services.AddScoped<INamedErpHandler, SapBtpErpHandler>();
// services.AddScoped<INamedErpHandler, OracleErpHandler>(); // Added when Oracle is supported
services.AddScoped<UnroutedErpHandler>();
services.AddScoped<ErpPostingActor>();

// Graph: single linear pipeline, no routing fan-out needed
df.AddActorBlock<SystemItem, PostingResult, ErpPostingActor>("erp-poster", maxConcurrency: 3);
```

Per-tenant configuration (e.g., different SAP subscription keys for different customers) is
provided via `IOptionsSnapshot<T>` or a named configuration service — no new graph routes needed.

### When a Static Route per Type Is Needed

If different ERP types need different concurrency settings or independent observability, add a
**thin static routing layer per ERP type** (not per tenant):

```csharp
// Route by ERP TYPE (2–3 routes, known at build time)
g.UseBlock("routing-transformer")
 .RouteBy(item => item.System?.Type ?? "unrouted")
 .To("sap-poster",      type => type == "SAP")
 .To("oracle-poster",   type => type == "Oracle")
 .To("unrouted-poster", _ => true);  // Fallback
```

This creates a small, static set of routes by integration type. Per-tenant variation is still
handled by configuration within each type's handler.

### When Routes Truly Must Be Created at Runtime

If new ERP integration types (not just new tenants) can be added at runtime without a deployment,
implement a **dispatcher block with internal channels**:

```csharp
public class DynamicErpDispatcher : IStreamActor<SystemItem, PostingResult>
{
    // Creates per-system-type channels dynamically on first encounter
    private readonly ConcurrentDictionary<string, SystemPipeline> _pipelines = new();
    private readonly IErpHandlerFactory _factory;

    public async IAsyncEnumerable<PostingResult> ProcessAsync(
        IAsyncEnumerable<SystemItem> input,
        IActorExecutionContext ctx,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var output = Channel.CreateBounded<PostingResult>(500);
        // ... route each item to a per-type channel, created on demand
        // ... merge all results through output channel
        // (full implementation in research: /research/add-persistent-router-guidance/)
    }
}
```

**Trade-offs**: Internal pipelines are not visible to graph visualization or metrics. Manual
observability (internal logging, metrics) must be added.

### Decision Guide

```
Set of route destinations small (< 10) and known at build time?
  └─ YES → Use SelectiveRoutingEdgeStrategy (this guide, above sections)

Different integration TYPES with different concurrency needs?
  └─ YES → Static routing by type + Generic handler within each type

New tenants added at runtime (same integration type, new config)?
  └─ YES → Generic handler + IOptionsSnapshot<T> for per-tenant config (no restart needed)

New integration TYPES added at runtime without deployment?
  └─ YES → Dispatcher block with internal channels (complex — use only if necessary)
```

**See**: [`migrating-from-persistent-router.md`](migrating-from-persistent-router.md) for a
complete migration guide covering all approaches with worked examples.

---

## Troubleshooting


### Problem: Unknown Route Key Exception

**Symptom**: `InvalidOperationException: "Unknown route key: xyz"`

**Cause**: Route selector returned a key not in the route mapping.

**Solutions**:
1. Add a default route
2. Normalize in selector to ensure valid keys
3. Validate data upstream

See [Handling Unknown Routes](#handling-unknown-routes) for details.

### Problem: Items Going to Wrong Route

**Symptom**: High-priority orders going to normal processor.

**Cause**: Route selector logic or predicate issue.

**Debug**:
```csharp
.RouteBy(order =>
{
    var key = order.Priority;
    _logger.LogInformation("Routing order {Id} with priority {Priority} to key {Key}",
        order.Id, order.Priority, key);
    return key;
})
```

### Problem: Poor Performance

**Symptom**: Routing slower than expected.

**Cause**: Expensive route selector function.

**Solution**: Keep selector simple and fast:
```csharp
// ❌ Slow: Database call in selector
.RouteBy(order => await _db.GetPriority(order.Id))  // DON'T DO THIS!

// ✅ Fast: Simple property access
.RouteBy(order => order.Priority)
```

---

## Next Steps

### Learn More Patterns

- **[Broadcast Topology](topology-broadcast.md)** - Fan-out to all consumers
- **[Competing Consumers](topology-competing-consumers.md)** - Load balancing
- **[Migrating from AddPersistentRouter](migrating-from-persistent-router.md)** - Dynamic routing migration guide

### Advanced Features

- **[Working with Blocks](working-with-blocks.md)** - Deep dive on block types
- **[Epoch Actor Block](epoch-actor-block.md)** - Scope rotation and epochs
- **[Using Epochs](using-epochs.md)** - Transaction boundaries

### Related Topics

- **[Testing Guide](testing-guide.md)** - Test selective routing flows
- **[Dependency Injection](dependency-injection-registration.md)** - DI patterns

---

## Summary

### Key Takeaways

- **Selective routing** = Route based on item content
- **Zero overhead** (no wrapper records, O(1) lookup)
- Each item sent **only to matching route** (no broadcast waste)
- Routes defined at **build time** in POC
- **Unknown routes throw exceptions** - handle with defaults or validation
- Scales efficiently (performance doesn't degrade with route count)

### When to Use

| Use Selective Routing When... | Don't Use Selective Routing When... |
|-------------------------------|-------------------------------------|
| ✅ Routing by priority, region, type | ❌ Load balancing (use competing) |
| ✅ Different handlers per category | ❌ All consumers need all items (use broadcast) |
| ✅ Content-based decisions | ❌ Random distribution (use competing) |
| ✅ High-volume scenarios | ❌ Unlimited dynamic routes at runtime — use dispatcher block pattern instead |

### Performance Benefits

| Aspect | Selective Routing | Broadcast-and-Filter |
|--------|------------------|---------------------|
| Writes per item | 1 | N |
| Filter operations | 0 | N |
| CPU overhead | Minimal (O(1)) | N× more |
| Efficiency gain | Baseline | ~N× slower |

---

**Document Version**: 1.1  
**Status**: ✅ Current  
**Last Updated**: 2026-03-17
