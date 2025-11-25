# Source Blocks Guide

**Audience**: Intermediate DataFlow users  
**Prerequisites**: [Getting Started](./getting-started.md), [Working with Blocks](./working-with-blocks.md)  
**Time**: 15 minutes

---

## Table of Contents

1. [What are Source Blocks?](#what-are-source-blocks)
2. [Creating Simple Sources](#creating-simple-sources)
3. [Database Sources](#database-sources)
4. [File Sources](#file-sources)
5. [API Sources](#api-sources)
6. [Epoch Sources](#epoch-sources)
7. [Best Practices](#best-practices)

---

## What are Source Blocks?

**Source blocks** (also called **producers**) are the entry point of your data pipeline. They generate data without consuming input.

```
┌─────────────┐
│ Source      │──▶ Data stream
└─────────────┘
```

**Key Characteristics**:
- No input stream (they produce data)
- Yield `IAsyncEnumerable<T>`
- Can be synchronous or asynchronous
- Control when the pipeline starts and stops

---

## Creating Simple Sources

### In-Memory Data Source

The simplest source yields data from memory:

```csharp
using DataFlow.POC.Tests.TestHelpers;

// Synchronous source
var numbers = BlockHelpers.CreateProducer<int>("numbers", ctx =>
{
    for (int i = 1; i <= 10; i++)
    {
        yield return i;
    }
});

// List-based source
var items = new[] { "apple", "banana", "cherry" };
var fruits = BlockHelpers.CreateProducer<string>("fruits", ctx =>
{
    foreach (var item in items)
        yield return item;
});
```

### Async Data Source

For asynchronous data sources, use `async` enumeration:

```csharp
using System.Runtime.CompilerServices;

var asyncSource = BlockHelpers.CreateProducer<string>("async-source", async ctx =>
{
    await foreach (var item in GetDataAsync(ctx.CancellationToken))
    {
        yield return item;
    }
});

async IAsyncEnumerable<string> GetDataAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    for (int i = 0; i < 10; i++)
    {
        await Task.Delay(100, cancellationToken); // Simulate async work
        yield return $"Item-{i}";
    }
}
```

### Infinite Source

For continuous streams (like message queues):

```csharp
var continuousSource = BlockHelpers.CreateProducer<Message>("queue", async ctx =>
{
    while (!ctx.CancellationToken.IsCancellationRequested)
    {
        var message = await queue.DequeueAsync(ctx.CancellationToken);
        if (message != null)
            yield return message;
        else
            await Task.Delay(100, ctx.CancellationToken); // Wait for next message
    }
});
```

---

## Database Sources

### Entity Framework Core Source

Read data from a database using Entity Framework Core:

```csharp
using Microsoft.EntityFrameworkCore;

services.AddDataFlows("global", df =>
{
    df.AddBlock("order-source", sp =>
    {
        return BlockHelpers.CreateProducer<Order>("order-source", async ctx =>
        {
            // Resolve DbContext from service provider
            var dbContext = ctx.ServiceProvider.GetRequiredService<OrderDbContext>();
            
            // Query database
            var orders = await dbContext.Orders
                .Where(o => o.Status == "Pending")
                .AsNoTracking() // Important: no tracking for read-only
                .ToListAsync(ctx.CancellationToken);
            
            // Yield each order
            foreach (var order in orders)
            {
                yield return order;
            }
        });
    });
});
```

### Batched Database Source

For large datasets, read in batches to avoid loading everything into memory:

```csharp
df.AddBlock("large-dataset-source", sp =>
{
    return BlockHelpers.CreateProducer<Customer>("large-dataset-source", async ctx =>
    {
        var dbContext = ctx.ServiceProvider.GetRequiredService<CustomerDbContext>();
        var pageSize = 1000;
        var page = 0;
        
        while (true)
        {
            var customers = await dbContext.Customers
                .OrderBy(c => c.Id)
                .Skip(page * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync(ctx.CancellationToken);
            
            if (!customers.Any())
                break; // No more data
            
            foreach (var customer in customers)
                yield return customer;
            
            page++;
        }
    });
});
```

---

## File Sources

### Reading Text Files

```csharp
var fileSource = BlockHelpers.CreateProducer<string>("file-reader", async ctx =>
{
    using var reader = new StreamReader("data.txt");
    string? line;
    
    while ((line = await reader.ReadLineAsync()) != null)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();
        yield return line;
    }
});
```

### Reading CSV Files

```csharp
using CsvHelper;
using CsvHelper.Configuration;

var csvSource = BlockHelpers.CreateProducer<CustomerRecord>("csv-reader", async ctx =>
{
    using var reader = new StreamReader("customers.csv");
    using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
    
    await foreach (var record in csv.GetRecordsAsync<CustomerRecord>(ctx.CancellationToken))
    {
        yield return record;
    }
});
```

### Reading JSON Files

```csharp
using System.Text.Json;

var jsonSource = BlockHelpers.CreateProducer<Order>("json-reader", async ctx =>
{
    using var stream = File.OpenRead("orders.json");
    
    var orders = await JsonSerializer.DeserializeAsync<List<Order>>(
        stream,
        cancellationToken: ctx.CancellationToken);
    
    if (orders != null)
    {
        foreach (var order in orders)
            yield return order;
    }
});
```

---

## API Sources

### REST API Source

```csharp
using System.Net.Http.Json;

var apiSource = BlockHelpers.CreateProducer<Product>("api-source", async ctx =>
{
    using var httpClient = new HttpClient();
    var page = 1;
    
    while (true)
    {
        var response = await httpClient.GetAsync(
            $"https://api.example.com/products?page={page}",
            ctx.CancellationToken);
        
        response.EnsureSuccessStatusCode();
        
        var products = await response.Content.ReadFromJsonAsync<List<Product>>(
            cancellationToken: ctx.CancellationToken);
        
        if (products == null || !products.Any())
            break; // No more pages
        
        foreach (var product in products)
            yield return product;
        
        page++;
    }
});
```

### Message Queue Source

```csharp
// Example with Azure Service Bus
using Azure.Messaging.ServiceBus;

var queueSource = BlockHelpers.CreateProducer<OrderMessage>("queue-source", async ctx =>
{
    var client = ctx.ServiceProvider.GetRequiredService<ServiceBusClient>();
    var receiver = client.CreateReceiver("orders-queue");
    
    try
    {
        while (!ctx.CancellationToken.IsCancellationRequested)
        {
            var message = await receiver.ReceiveMessageAsync(
                maxWaitTime: TimeSpan.FromSeconds(5),
                cancellationToken: ctx.CancellationToken);
            
            if (message != null)
            {
                var orderMessage = JsonSerializer.Deserialize<OrderMessage>(
                    message.Body.ToString());
                
                if (orderMessage != null)
                    yield return orderMessage;
                
                // Complete the message
                await receiver.CompleteMessageAsync(message, ctx.CancellationToken);
            }
        }
    }
    finally
    {
        await receiver.DisposeAsync();
    }
});
```

---

## Epoch Sources

When working with [epochs](./using-epochs.md), your source can create epoch boundaries.

### Single Epoch Stream

The simplest case - all data in one epoch:

```csharp
var singleEpochSource = BlockHelpers.CreateProducer<int>("single-epoch", ctx =>
{
    // All items belong to the same logical epoch
    for (int i = 1; i <= 100; i++)
        yield return i;
});
```

**Note**: When you don't explicitly configure epochs, DataFlow treats this as a single-epoch stream automatically.

### Multi-Epoch Source with ConfigureEpochs

For explicit epoch boundaries, use the `ConfigureEpochs` API:

```csharp
services.AddDataFlows("global", df =>
{
    df.AddGraph("epoch-flow", g =>
    {
        var source = BlockHelpers.CreateProducer<Order>("orders", GetOrders);
        
        g.AddBlock(source)
         .ConfigureEpochs(config =>
         {
             // Create epochs by count (every 100 items)
             config.SetPolicy(EpochPolicy.ByCount(100));
             
             // Add processor for each epoch
             config.AddProcessor("process-batch");
         },
         sp => new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
    });
});
```

For more details, see the [Using Epochs](./using-epochs.md) guide.

---

## Best Practices

### 1. Always Respect Cancellation Token

```csharp
// ✅ Good: Check cancellation
var source = BlockHelpers.CreateProducer<int>("source", async ctx =>
{
    for (int i = 0; i < 1000000; i++)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();
        yield return i;
    }
});

// ❌ Bad: Ignore cancellation
var badSource = BlockHelpers.CreateProducer<int>("source", ctx =>
{
    for (int i = 0; i < 1000000; i++)
        yield return i; // Can't stop!
});
```

### 2. Complete the Stream

Make sure your source completes:

```csharp
// ✅ Good: Completes after processing all data
var source = BlockHelpers.CreateProducer<string>("file", async ctx =>
{
    using var reader = new StreamReader("data.txt");
    string? line;
    while ((line = await reader.ReadLineAsync()) != null)
        yield return line;
    // Stream completes when file ends
});

// ❌ Bad: Never completes
var badSource = BlockHelpers.CreateProducer<int>("infinite", ctx =>
{
    while (true) // Infinite loop!
        yield return 1;
});
```

### 3. Use AsNoTracking for Read-Only Queries

```csharp
// ✅ Good: No tracking for read-only
var orders = await dbContext.Orders
    .AsNoTracking()
    .ToListAsync();

// ❌ Bad: Tracking enabled unnecessarily
var orders = await dbContext.Orders
    .ToListAsync(); // EF Core tracks changes
```

### 4. Batch Large Datasets

```csharp
// ✅ Good: Read in batches
var pageSize = 1000;
for (int page = 0; ; page++)
{
    var batch = await dbContext.Items
        .Skip(page * pageSize)
        .Take(pageSize)
        .ToListAsync();
    
    if (!batch.Any()) break;
    
    foreach (var item in batch)
        yield return item;
}

// ❌ Bad: Load everything into memory
var allItems = await dbContext.Items.ToListAsync();
foreach (var item in allItems)
    yield return item;
```

### 5. Handle Errors Gracefully

```csharp
var source = BlockHelpers.CreateProducer<Data>("source", async ctx =>
{
    try
    {
        await foreach (var item in GetDataAsync(ctx.CancellationToken))
        {
            yield return item;
        }
    }
    catch (Exception ex)
    {
        // Log the error
        logger.LogError(ex, "Error reading source data");
        throw; // Re-throw to stop pipeline
    }
});
```

---

## Common Patterns

### Pattern: Polling Source

Continuously poll an external source:

```csharp
var pollingSource = BlockHelpers.CreateProducer<Event>("events", async ctx =>
{
    var lastEventId = 0;
    
    while (!ctx.CancellationToken.IsCancellationRequested)
    {
        var events = await GetNewEventsAsync(lastEventId, ctx.CancellationToken);
        
        foreach (var evt in events)
        {
            yield return evt;
            lastEventId = Math.Max(lastEventId, evt.Id);
        }
        
        if (!events.Any())
            await Task.Delay(TimeSpan.FromSeconds(5), ctx.CancellationToken);
    }
});
```

### Pattern: Composite Source

Combine multiple sources:

```csharp
var compositeSource = BlockHelpers.CreateProducer<Order>("all-orders", async ctx =>
{
    // Source 1: Database
    var dbOrders = await GetOrdersFromDatabase(ctx);
    foreach (var order in dbOrders)
        yield return order;
    
    // Source 2: API
    await foreach (var order in GetOrdersFromAPI(ctx))
        yield return order;
    
    // Source 3: File
    await foreach (var order in GetOrdersFromFile(ctx))
        yield return order;
});
```

### Pattern: Filtered Source

Apply filtering at the source level:

```csharp
var filteredSource = BlockHelpers.CreateProducer<Order>("high-value-orders", async ctx =>
{
    var dbContext = ctx.ServiceProvider.GetRequiredService<OrderDbContext>();
    
    await foreach (var order in dbContext.Orders
        .Where(o => o.Total > 1000) // Filter in database
        .AsNoTracking()
        .AsAsyncEnumerable()
        .WithCancellation(ctx.CancellationToken))
    {
        yield return order;
    }
});
```

---

## Next Steps

Now that you understand source blocks, explore:

1. **[Epoch Actor Block](./epoch-actor-block.md)** - Scope rotation for sources with epochs
2. **[Using Epochs](./using-epochs.md)** - Creating epoch boundaries
3. **[Checkpointing](./checkpointing.md)** - Resume from where you left off

---

## Summary

You've learned:

- ✅ What source blocks are and their role in pipelines
- ✅ How to create sources from various data sources (DB, files, APIs)
- ✅ Best practices for source blocks
- ✅ Common patterns for real-world scenarios

**Next**: Learn about [Epoch Actor Blocks](./epoch-actor-block.md) for advanced scope management.

---

**Related Guides**:
- [Getting Started](./getting-started.md)
- [Working with Blocks](./working-with-blocks.md)
- [Using Epochs](./using-epochs.md)
