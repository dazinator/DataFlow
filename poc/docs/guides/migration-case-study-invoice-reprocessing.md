# Migration Case Study: Invoice Reprocessing DataFlow

**Audience**: Application developers migrating from legacy DataFlow (`Uniun.DataFlow`) to POC DataFlow  
**Prerequisites**: [Getting Started](getting-started.md), [Business Logic Decoupling](business-logic-decoupling.md), [Testing Guide](testing-guide.md)  
**Difficulty**: Advanced  
**Last Updated**: 2025-12-10

---

## Table of Contents

1. [Overview](#overview)
2. [General Migration Approach](#general-migration-approach)
3. [Understanding the Legacy Dataflow](#understanding-the-legacy-dataflow)
4. [Identifying Topology Patterns](#identifying-topology-patterns)
5. [Business Logic Decoupling](#business-logic-decoupling)
6. [Complete POC Migration](#complete-poc-migration)
7. [Testing the Migrated Dataflow](#testing-the-migrated-dataflow)
8. [Migration Checklist](#migration-checklist)

---

## Overview

This guide provides a **complete, hands-on case study** for migrating a real-world invoice reprocessing dataflow from the legacy `Uniun.DataFlow` library to the new POC DataFlow library. By the end of this guide, you'll have a fully functional, tested POC dataflow that you can deploy in a PR.

### What You'll Learn

- How to run both libraries side-by-side during migration
- How to identify and map legacy patterns to POC equivalents
- How to apply business logic decoupling for better testability
- How to migrate complex features like batching, rate limiting, and concurrency
- How to write comprehensive tests for the migrated dataflow

### Case Study: Invoice Reprocessing

The invoice reprocessing dataflow is a production-grade ETL pipeline that:

1. Reads invoices from a database
2. Batches them for efficient processing
3. Rate-limits batch consumption to control memory
4. Enriches invoices with counterparty information
5. Applies business rules for invoice enrichment
6. Bulk updates enriched invoices to the database
7. Identifies cashflow reallocation changes
8. Batches and processes cashflow reallocations

**Complexity**: 14 blocks, ~200 lines of configuration code, multiple external dependencies

---

## General Migration Approach

### Side-by-Side Package Strategy

The migration leverages **dual-package support** to enable phased, incremental migration:

```
Legacy Package:    Uniun.DataFlow  (current production library)
POC Package:       DataFlow        (new library - different namespace)
```

**Key Benefit**: Both packages can coexist in the same application, allowing you to:
- Migrate one dataflow at a time
- Test new flows alongside old ones
- Reduce risk by keeping production flows running
- Rollback easily if issues arise

### Migration Workflow

```
Phase 1: Analysis
├─ Understand legacy dataflow structure
├─ Identify topology patterns
└─ Map dependencies

Phase 2: Preparation
├─ Install POC package
├─ Extract business logic to services
└─ Create service interfaces

Phase 3: Migration
├─ Recreate blocks using POC library
├─ Configure graph topology
└─ Wire up DI registration

Phase 4: Testing
├─ Unit test actors with mocks
├─ Integration test complete dataflow
└─ Performance validation

Phase 5: Deployment
├─ Deploy alongside legacy flow
├─ Monitor and validate
└─ Remove legacy flow when confident
```

### Installing the POC Package

```bash
# Add POC package (example - adjust version as needed)
dotnet add package DataFlow --version 1.0.0-preview
```

In your `.csproj`:

```xml
<ItemGroup>
  <!-- Legacy library - keep during migration -->
  <PackageReference Include="Uniun.DataFlow" Version="2.x.x" />
  
  <!-- POC library - new dataflows use this -->
  <PackageReference Include="DataFlow" Version="1.0.0-preview" />
</ItemGroup>
```

**Namespace Separation**:
- Legacy: `using Uniun.DataFlow.*`
- POC: `using DataFlow.POC.*`

This prevents conflicts and allows both to coexist.

---

## Understanding the Legacy Dataflow

### Legacy Dataflow Overview

The invoice reprocessing dataflow processes invoices through multiple enrichment and reallocation stages.

### Legacy Code Structure

```csharp
public partial class InvoiceReprocessingDataFlowConfiguration : IDataFlowConfiguration
{
    public const string Name = "Invoice Reprocessing";
    
    public void Configure(DataFlowBuilder builder)
    {
        var logger = builder.ServiceProvider.GetRequiredService<ILogger<InvoiceReprocessingDataFlowConfiguration>>();

        builder
         // STAGE 1: Invoice Production
         .AddProducer<InvoiceEnrichmentContext, InvoiceProducer>(
             BlockNames.Producer, 
             (options) =>
             {
                 options.Capacity = 2;
                 options.MaxConcurrency = 1;
             })

          // STAGE 2: Batching
          .AddBatch<InvoiceEnrichmentContext>(
              BlockNames.BatchEnrichmentItems,
              (options) =>
              {
                  options.WindowPeriod = TimeSpan.FromSeconds(10);
                  options.Capacity = 2;
                  options.MaxBatchSize = 10000;
              })
              .ReceiveFrom(BlockNames.Producer)

              // STAGE 3: Rate Limiting
        .AddRateLimit<InvoiceEnrichmentContext[]>(
            BlockNames.BatchRateLimiter,
            () => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1,
                Window = TimeSpan.FromSeconds(6),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = int.MaxValue,
                AutoReplenishment = true
            }), options => options.Capacity = 1)
            .ReceiveFrom(BlockNames.BatchEnrichmentItems)

         // STAGE 4: Enrichment Pipeline
         .AddTransform<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[], InvoiceCounterpartyInfoLoaderTransformer>(
             BlockNames.LoadCounterpartyInfo)
            .ReceiveFrom(BlockNames.BatchRateLimiter)

         .AddTransform<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[], InvoiceEnrichmentTransformer>(
             BlockNames.EnrichInvoices)
             .ReceiveFrom(BlockNames.LoadCounterpartyInfo)

         .AddTransform<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[], InvoiceEnrichmentBulkUpdateTransformer>(
             BlockNames.UpdateEnrichedInvoices)
             .ReceiveFrom(BlockNames.EnrichInvoices)

         .AddTransform<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[], InvoiceReprocessingDataflowNotifier>(
             BlockNames.InvoiceReprocessingDataflowNotifier)
            .ReceiveFrom(BlockNames.UpdateEnrichedInvoices)

         // STAGE 5: Cashflow Reallocation
         .AddTransform<InvoiceEnrichmentContext[], CashflowReallocationChange, InvoiceCashflowReallocationChangesProducer>(
             BlockNames.CashflowChangesIdentifier)
             .ReceiveFrom(BlockNames.InvoiceReprocessingDataflowNotifier)

         .AddBatch<CashflowReallocationChange>(
             BlockNames.BatchChangeItems,
             maxBatchSize: 10000,
             windowPeriod: TimeSpan.FromSeconds(2),
             o => o.Capacity = 1)
             .ReceiveFrom(BlockNames.CashflowChangesIdentifier)

         .AddTransform<CashflowReallocationChange[], CashflowReallocationGroup[], ReallocationBatchTransformer>(
             BlockNames.ReallocateInvoicesToCashflows)
             .ReceiveFrom(BlockNames.BatchChangeItems)

         .AddTransform<CashflowReallocationGroup[], CashflowReallocationGroup[], ReallocationBatchProcessor>(
             BlockNames.ReallocateCashflowsProcessor)
             .ReceiveFrom(BlockNames.ReallocateInvoicesToCashflows);
    }
}
```

### Key Observations

1. **14 blocks** in the pipeline
2. **Multiple transformation stages** with batch processing
3. **Rate limiting** for memory control
4. **Service dependencies** in each transformer/processor
5. **Sequential processing** (no concurrency at topology level)

### Block Types in Legacy Dataflow

| Legacy Block Type | Purpose | Example |
|-------------------|---------|---------|
| `AddProducer` | Generate stream items | `InvoiceProducer` |
| `AddBatch` | Group items into batches | `BatchEnrichmentItems` |
| `AddRateLimit` | Control throughput | `BatchRateLimiter` |
| `AddTransform` | Transform items | `InvoiceEnrichmentTransformer` |
| `AddProcess` | Terminal consumer | (Not used in this flow) |

---

## Identifying Topology Patterns

To migrate effectively, we need to identify the **topology patterns** used in the legacy dataflow and map them to POC equivalents.

### Pattern 1: Producer → Consumer (Sequential Pipeline)

**Legacy Pattern:**
```csharp
.AddProducer<T, ProducerType>(name)
.AddTransform<TIn, TOut, TransformerType>(name)
    .ReceiveFrom(producerName)
```

**POC Equivalent:**
```csharp
// Register source block with DI
df.AddBlock("producer", sp =>
{
    var factory = sp.GetRequiredService<IServiceScopeFactory>();
    return new PlainSourceAdapter<T, ProducerType>(
        new BlockContext("namespace:producer"),
        factory,
        sourceName: "my-source");
});

// Register transformer actor
df.AddActorBlock<TIn, TOut, TransformerActor>("transformer");

df.AddGraph("graph", g =>
{
    g.UseBlock("producer");
    g.UseBlock("transformer");
    g.Connect("producer", "transformer");
});
```

**Mapping:**
- `AddProducer` → Register with `AddBlock` using `PlainSourceAdapter`
- `AddTransform` → `AddActorBlock` (EpochActorBlock)
- `.ReceiveFrom()` → Use `g.Connect(source, target)` for graph connections

---

### Pattern 2: Batching

**Legacy Pattern:**
```csharp
.AddBatch<T>(name, options =>
{
    options.WindowPeriod = TimeSpan.FromSeconds(10);
    options.MaxBatchSize = 10000;
    options.Capacity = 2;
})
.ReceiveFrom(sourceName)
```

**POC Equivalent:**
```csharp
// Register batch block with DI
df.AddBlock("batcher", sp =>
{
    return new EpochBatchBlock<T>(
        new BlockContext("namespace:batcher"),
        maxBatchSize: 10000,
        windowPeriod: TimeSpan.FromSeconds(10));
});

df.AddGraph("graph", g =>
{
    g.UseBlock("source");
    g.UseBlock("batcher");
    g.Connect("source", "batcher");
});
```

**Mapping:**
- `AddBatch<T>` → Register with `AddBlock` using `EpochBatchBlock`
- Window period and max batch size configured in constructor
- Capacity is now edge-level configuration via `Connect()`

---

### Pattern 3: Rate Limiting

**Legacy Pattern:**
```csharp
.AddRateLimit<T>(name, 
    () => new FixedWindowRateLimiter(...),
    options => options.Capacity = 1)
.ReceiveFrom(sourceName)
```

**POC Equivalent:**

Rate limiting in POC is typically achieved through:
1. **Buffer blocks** with bounded capacity
2. **Edge configuration** with appropriate buffer modes
3. **Custom actors** that implement throttling logic

```csharp
// Option 1: Use buffer block for memory control
df.AddBlock("rate-limited-buffer", sp =>
{
    return new EpochBufferBlock<T>(
        new BlockContext("namespace:rate-limited-buffer"),
        new BufferConfiguration(capacity: 2));
});

df.AddGraph("graph", g =>
{
    g.UseBlock("source");
    g.UseBlock("rate-limited-buffer");
    g.Connect("source", "rate-limited-buffer");
});

// Option 2: Custom throttling actor
public class ThrottlingActor<T> : IStreamActor<T, T>
{
    private readonly SemaphoreSlim _semaphore;
    private readonly TimeSpan _releaseInterval;
    
    public ThrottlingActor(int maxConcurrent, TimeSpan releaseInterval)
    {
        _semaphore = new SemaphoreSlim(maxConcurrent);
        _releaseInterval = releaseInterval;
    }
    
    public async IAsyncEnumerable<T> RunAsync(
        IAsyncEnumerable<T> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            await _semaphore.WaitAsync(context.CancellationToken);
            yield return item;
            
            // Release after interval
            _ = Task.Run(async () =>
            {
                await Task.Delay(_releaseInterval);
                _semaphore.Release();
            });
        }
    }
}
```

**Migration Decision**: For this dataflow, the rate limiter controls memory by limiting how many batches are in flight. We'll use a **buffer block** with small capacity to achieve the same effect.

---

### Pattern 4: Concurrency (Legacy MaxConcurrency)

**Legacy Pattern:**
```csharp
.AddTransform<TIn, TOut, TransformerType>(name, options =>
{
    options.MaxConcurrency = 4;  // Internal concurrency
})
```

**POC Equivalent - Producer-Consumer Pattern:**

In the legacy model, blocks could do internal concurrency. In POC, concurrency is achieved at the **topology level** using the **Producer-Consumer** pattern with buffer blocks.

```csharp
// Legacy: 1 block with MaxConcurrency = 4
// POC: 1 buffer + 4 competing actor blocks

df.AddBlock("work-buffer", sp =>
{
    return new EpochBufferBlock<TIn>(
        new BlockContext("namespace:work-buffer"),
        new BufferConfiguration(capacity: 100));
});

df.AddActorBlock<TIn, TOut, TransformerActor>("worker-1");
df.AddActorBlock<TIn, TOut, TransformerActor>("worker-2");
df.AddActorBlock<TIn, TOut, TransformerActor>("worker-3");
df.AddActorBlock<TIn, TOut, TransformerActor>("worker-4");

df.AddGraph("graph", g =>
{
    g.UseBlock("source");
    g.UseBlock("work-buffer");
    g.UseBlock("worker-1");
    g.UseBlock("worker-2");
    g.UseBlock("worker-3");
    g.UseBlock("worker-4");
    
    g.Connect("source", "work-buffer");
    g.ConnectCompeting(
        g.FindBlockByName("work-buffer"), 
        new[] { 
            g.FindBlockByName("worker-1"),
            g.FindBlockByName("worker-2"),
            g.FindBlockByName("worker-3"),
            g.FindBlockByName("worker-4")
        },
        bufferCapacity: 100);
});
```

**See**: [Competing Consumers Topology](topology-competing-consumers.md) for complete details.

**Important**: The invoice reprocessing flow uses **MaxConcurrency = 1** (sequential processing), so we don't need this pattern. However, if you're migrating a flow with concurrency, use the competing consumers pattern.

---

### Pattern 5: Transform that Expands/Contracts Items

**Legacy Pattern:**
```csharp
// Transforms InvoiceEnrichmentContext[] → CashflowReallocationChange
// (i.e., one batch → stream of changes)
.AddTransform<InvoiceEnrichmentContext[], CashflowReallocationChange, Producer>(name)
```

**POC Equivalent:**

In POC, this is still an `EpochActorBlock`, but the actor implements `IStreamActor<TIn, TOut>`:

```csharp
public class ChangesProducerActor : IStreamActor<InvoiceEnrichmentContext[], CashflowReallocationChange>
{
    private readonly ICashflowChangeIdentifier _changeIdentifier;
    
    public ChangesProducerActor(ICashflowChangeIdentifier changeIdentifier)
    {
        _changeIdentifier = changeIdentifier;
    }
    
    public async IAsyncEnumerable<CashflowReallocationChange> RunAsync(
        IAsyncEnumerable<InvoiceEnrichmentContext[]> input,
        IActorExecutionContext context)
    {
        await foreach (var batch in input.WithCancellation(context.CancellationToken))
        {
            // Expand: one batch → multiple changes
            var changes = _changeIdentifier.IdentifyChanges(batch);
            foreach (var change in changes)
            {
                yield return change;
            }
        }
    }
}
```

---

### Summary: Pattern Mapping Table

| Legacy Pattern | POC Equivalent | Notes |
|----------------|---------------|-------|
| `AddProducer<T, P>` | `AddBlock` with `PlainSourceAdapter` | Wrap plain source actors with PlainSourceAdapter |
| `AddBatch<T>` | `AddBlock` with `EpochBatchBlock` | Pass maxBatchSize and windowPeriod to constructor |
| `AddRateLimit<T>` | `AddBlock` with `EpochBufferBlock` + `BufferConfiguration` | Use bounded buffer for memory control |
| `AddTransform<TIn, TOut, T>` | `AddActorBlock<TIn, TOut, T>` | EpochActorBlock - unified block type |
| `AddProcess<T, P>` | `AddActorBlock<T, object, P>` | Processor returns empty/dummy output |
| `MaxConcurrency = N` | Buffer + N competing actors | Topology-level concurrency pattern |
| `.ReceiveFrom(name)` | `g.Connect(source, target)` | Use Connect() method for graph connections |

---

## Business Logic Decoupling

Before migrating the blocks, we should apply the **Business Logic Decoupling Pattern** to separate business logic from DataFlow orchestration. This dramatically improves testability.

**See**: [Business Logic Decoupling Pattern](business-logic-decoupling.md) for complete guidance.

### Services to Extract

Looking at the legacy dataflow, we have these transformers/processors:

1. `InvoiceProducer` - Database query
2. `InvoiceCounterpartyInfoLoaderTransformer` - Load counterparty data
3. `InvoiceEnrichmentTransformer` - Business rule enrichment
4. `InvoiceEnrichmentBulkUpdateTransformer` - Database bulk update
5. `InvoiceReprocessingDataflowNotifier` - Notification
6. `InvoiceCashflowReallocationChangesProducer` - Change identification
7. `ReallocationBatchTransformer` - Reallocation logic
8. `ReallocationBatchProcessor` - Final processing

For each, we'll create:
- **Service Interface** - Defines the business logic contract
- **Service Implementation** - Pure business logic (easy to test)
- **Actor** - Thin orchestration layer (uses service)

### Example: Invoice Enrichment Service

**Step 1: Define Service Interface**

```csharp
public interface IInvoiceEnrichmentService
{
    /// <summary>
    /// Enriches a batch of invoices with business rules
    /// </summary>
    InvoiceEnrichmentContext[] Enrich(InvoiceEnrichmentContext[] batch);
}
```

**Step 2: Implement Service (Pure Business Logic)**

```csharp
public class InvoiceEnrichmentService : IInvoiceEnrichmentService
{
    private readonly ILogger<InvoiceEnrichmentService> _logger;
    
    public InvoiceEnrichmentService(ILogger<InvoiceEnrichmentService> logger)
    {
        _logger = logger;
    }
    
    public InvoiceEnrichmentContext[] Enrich(InvoiceEnrichmentContext[] batch)
    {
        // Pure business logic - no async, no streaming
        foreach (var context in batch)
        {
            // Apply enrichment rules
            context.EnrichedField1 = CalculateField1(context);
            context.EnrichedField2 = CalculateField2(context);
            // ... more business logic
        }
        
        _logger.LogInformation("Enriched {Count} invoices", batch.Length);
        return batch;
    }
    
    private string CalculateField1(InvoiceEnrichmentContext context)
    {
        // Business rule logic
        return context.SourceData + "_enriched";
    }
    
    private decimal CalculateField2(InvoiceEnrichmentContext context)
    {
        // Business rule logic
        return context.Amount * 1.1m;
    }
}
```

**Step 3: Create Actor (Thin Orchestration)**

```csharp
public class InvoiceEnrichmentActor : IStreamActor<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[]>
{
    private readonly IInvoiceEnrichmentService _enrichmentService;
    
    public InvoiceEnrichmentActor(IInvoiceEnrichmentService enrichmentService)
    {
        _enrichmentService = enrichmentService;
    }
    
    public async IAsyncEnumerable<InvoiceEnrichmentContext[]> RunAsync(
        IAsyncEnumerable<InvoiceEnrichmentContext[]> input,
        IActorExecutionContext context)
    {
        await foreach (var batch in input.WithCancellation(context.CancellationToken))
        {
            // Delegate to service - actor is just orchestration
            yield return _enrichmentService.Enrich(batch);
        }
    }
}
```

**Benefits**:
- ✅ `InvoiceEnrichmentService` can be unit tested in isolation (fast, simple)
- ✅ `InvoiceEnrichmentActor` can be tested with mock service
- ✅ Business logic clearly separated from streaming concerns
- ✅ Service can be reused in other contexts

### All Services for This Dataflow

For the invoice reprocessing flow, we need these services:

```csharp
// 1. Invoice repository (already exists - database access)
public interface IInvoiceRepository
{
    IAsyncEnumerable<InvoiceEnrichmentContext> GetInvoicesForReprocessingAsync(
        CancellationToken cancellationToken);
    Task BulkUpdateAsync(InvoiceEnrichmentContext[] batch, CancellationToken cancellationToken);
}

// 2. Counterparty info loader
public interface ICounterpartyInfoLoader
{
    Task<InvoiceEnrichmentContext[]> LoadCounterpartyInfoAsync(
        InvoiceEnrichmentContext[] batch, 
        CancellationToken cancellationToken);
}

// 3. Invoice enrichment (business rules)
public interface IInvoiceEnrichmentService
{
    InvoiceEnrichmentContext[] Enrich(InvoiceEnrichmentContext[] batch);
}

// 4. Notification service
public interface INotificationService
{
    Task NotifyAsync(InvoiceEnrichmentContext[] batch, CancellationToken cancellationToken);
}

// 5. Cashflow change identifier
public interface ICashflowChangeIdentifier
{
    IEnumerable<CashflowReallocationChange> IdentifyChanges(InvoiceEnrichmentContext[] batch);
}

// 6. Reallocation transformer
public interface IReallocationTransformer
{
    CashflowReallocationGroup[] TransformToGroups(CashflowReallocationChange[] changes);
}

// 7. Reallocation processor
public interface IReallocationProcessor
{
    Task<CashflowReallocationGroup[]> ProcessAsync(
        CashflowReallocationGroup[] groups, 
        CancellationToken cancellationToken);
}
```

**Registration in DI:**

```csharp
// Register services
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<ICounterpartyInfoLoader, CounterpartyInfoLoader>();
builder.Services.AddScoped<IInvoiceEnrichmentService, InvoiceEnrichmentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICashflowChangeIdentifier, CashflowChangeIdentifier>();
builder.Services.AddScoped<IReallocationTransformer, ReallocationTransformer>();
builder.Services.AddScoped<IReallocationProcessor, ReallocationProcessor>();
```

---

## Complete POC Migration

Now we'll migrate the entire invoice reprocessing dataflow to the POC library, step by step.

### Step 1: Define Actors

Create actors for each block in the pipeline. Each actor delegates to a service.

**Invoice Source Actor:**

```csharp
using DataFlow.POC.Core;

public class InvoiceSource : IPlainSourceActor<InvoiceEnrichmentContext>
{
    private readonly IInvoiceRepository _repository;
    
    public InvoiceSource(IInvoiceRepository repository)
    {
        _repository = repository;
    }
    
    public async IAsyncEnumerable<InvoiceEnrichmentContext> ProduceAsync(
        IActorExecutionContext context)
    {
        await foreach (var invoice in _repository.GetInvoicesForReprocessingAsync(context.CancellationToken))
        {
            yield return invoice;
        }
    }
}
```

**Counterparty Info Loader Actor:**

```csharp
public class CounterpartyInfoLoaderActor 
    : IStreamActor<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[]>
{
    private readonly ICounterpartyInfoLoader _loader;
    
    public CounterpartyInfoLoaderActor(ICounterpartyInfoLoader loader)
    {
        _loader = loader;
    }
    
    public async IAsyncEnumerable<InvoiceEnrichmentContext[]> RunAsync(
        IAsyncEnumerable<InvoiceEnrichmentContext[]> input,
        IActorExecutionContext context)
    {
        await foreach (var batch in input.WithCancellation(context.CancellationToken))
        {
            yield return await _loader.LoadCounterpartyInfoAsync(batch, context.CancellationToken);
        }
    }
}
```

**Invoice Enrichment Actor:**

```csharp
public class InvoiceEnrichmentActor 
    : IStreamActor<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[]>
{
    private readonly IInvoiceEnrichmentService _enrichmentService;
    
    public InvoiceEnrichmentActor(IInvoiceEnrichmentService enrichmentService)
    {
        _enrichmentService = enrichmentService;
    }
    
    public async IAsyncEnumerable<InvoiceEnrichmentContext[]> RunAsync(
        IAsyncEnumerable<InvoiceEnrichmentContext[]> input,
        IActorExecutionContext context)
    {
        await foreach (var batch in input.WithCancellation(context.CancellationToken))
        {
            yield return _enrichmentService.Enrich(batch);
        }
    }
}
```

**Bulk Update Actor:**

```csharp
public class InvoiceBulkUpdateActor 
    : IStreamActor<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[]>
{
    private readonly IInvoiceRepository _repository;
    
    public InvoiceBulkUpdateActor(IInvoiceRepository repository)
    {
        _repository = repository;
    }
    
    public async IAsyncEnumerable<InvoiceEnrichmentContext[]> RunAsync(
        IAsyncEnumerable<InvoiceEnrichmentContext[]> input,
        IActorExecutionContext context)
    {
        await foreach (var batch in input.WithCancellation(context.CancellationToken))
        {
            await _repository.BulkUpdateAsync(batch, context.CancellationToken);
            yield return batch;  // Pass through for next stage
        }
    }
}
```

**Notification Actor:**

```csharp
public class NotificationActor 
    : IStreamActor<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[]>
{
    private readonly INotificationService _notificationService;
    
    public NotificationActor(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }
    
    public async IAsyncEnumerable<InvoiceEnrichmentContext[]> RunAsync(
        IAsyncEnumerable<InvoiceEnrichmentContext[]> input,
        IActorExecutionContext context)
    {
        await foreach (var batch in input.WithCancellation(context.CancellationToken))
        {
            await _notificationService.NotifyAsync(batch, context.CancellationToken);
            yield return batch;
        }
    }
}
```

**Cashflow Changes Producer Actor:**

```csharp
public class CashflowChangesProducerActor 
    : IStreamActor<InvoiceEnrichmentContext[], CashflowReallocationChange>
{
    private readonly ICashflowChangeIdentifier _changeIdentifier;
    
    public CashflowChangesProducerActor(ICashflowChangeIdentifier changeIdentifier)
    {
        _changeIdentifier = changeIdentifier;
    }
    
    public async IAsyncEnumerable<CashflowReallocationChange> RunAsync(
        IAsyncEnumerable<InvoiceEnrichmentContext[]> input,
        IActorExecutionContext context)
    {
        await foreach (var batch in input.WithCancellation(context.CancellationToken))
        {
            // Expand: one batch → multiple changes
            var changes = _changeIdentifier.IdentifyChanges(batch);
            foreach (var change in changes)
            {
                yield return change;
            }
        }
    }
}
```

**Reallocation Transformer Actor:**

```csharp
public class ReallocationTransformerActor 
    : IStreamActor<CashflowReallocationChange[], CashflowReallocationGroup[]>
{
    private readonly IReallocationTransformer _transformer;
    
    public ReallocationTransformerActor(IReallocationTransformer transformer)
    {
        _transformer = transformer;
    }
    
    public async IAsyncEnumerable<CashflowReallocationGroup[]> RunAsync(
        IAsyncEnumerable<CashflowReallocationChange[]> input,
        IActorExecutionContext context)
    {
        await foreach (var batch in input.WithCancellation(context.CancellationToken))
        {
            yield return _transformer.TransformToGroups(batch);
        }
    }
}
```

**Reallocation Processor Actor:**

```csharp
public class ReallocationProcessorActor 
    : IStreamActor<CashflowReallocationGroup[], CashflowReallocationGroup[]>
{
    private readonly IReallocationProcessor _processor;
    
    public ReallocationProcessorActor(IReallocationProcessor processor)
    {
        _processor = processor;
    }
    
    public async IAsyncEnumerable<CashflowReallocationGroup[]> RunAsync(
        IAsyncEnumerable<CashflowReallocationGroup[]> input,
        IActorExecutionContext context)
    {
        await foreach (var batch in input.WithCancellation(context.CancellationToken))
        {
            yield return await _processor.ProcessAsync(batch, context.CancellationToken);
        }
    }
}
```

### Step 2: Register Services and Actors

```csharp
// In your Program.cs or Startup.cs
using DataFlow.POC.DependencyInjection;
using DataFlow.POC.Builder;
using DataFlow.POC.Blocks;
using DataFlow.POC.Core;

// Register business logic services
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<ICounterpartyInfoLoader, CounterpartyInfoLoader>();
builder.Services.AddScoped<IInvoiceEnrichmentService, InvoiceEnrichmentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICashflowChangeIdentifier, CashflowChangeIdentifier>();
builder.Services.AddScoped<IReallocationTransformer, ReallocationTransformer>();
builder.Services.AddScoped<IReallocationProcessor, ReallocationProcessor>();

// Register actors
builder.Services.AddScoped<InvoiceSource>();
builder.Services.AddScoped<CounterpartyInfoLoaderActor>();
builder.Services.AddScoped<InvoiceEnrichmentActor>();
builder.Services.AddScoped<InvoiceBulkUpdateActor>();
builder.Services.AddScoped<NotificationActor>();
builder.Services.AddScoped<CashflowChangesProducerActor>();
builder.Services.AddScoped<ReallocationTransformerActor>();
builder.Services.AddScoped<ReallocationProcessorActor>();
```

### Step 3: Define the DataFlow

```csharp
builder.Services.AddDataFlows("invoice-reprocessing", df =>
{
    // === STAGE 1: Invoice Source ===
    df.AddBlock("invoice-source", sp =>
    {
        var factory = sp.GetRequiredService<IServiceScopeFactory>();
        return new PlainSourceAdapter<InvoiceEnrichmentContext, InvoiceSource>(
            new BlockContext("invoice-reprocessing:invoice-source"),
            factory,
            sourceName: "database");
    });
    
    // === STAGE 2: Batching ===
    df.AddBlock("batch-invoices", sp =>
    {
        return new EpochBatchBlock<InvoiceEnrichmentContext>(
            new BlockContext("invoice-reprocessing:batch-invoices"),
            maxBatchSize: 10000,
            windowPeriod: TimeSpan.FromSeconds(10));
    });
    
    // === STAGE 3: Rate Limiting (using buffer with small capacity) ===
    df.AddBlock("rate-limit-buffer", sp =>
    {
        return new EpochBufferBlock<InvoiceEnrichmentContext[]>(
            new BlockContext("invoice-reprocessing:rate-limit-buffer"),
            new BufferConfiguration(capacity: 2));
    });
    
    // === STAGE 4: Enrichment Pipeline ===
    df.AddActorBlock<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[], CounterpartyInfoLoaderActor>(
        "load-counterparty");
    df.AddActorBlock<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[], InvoiceEnrichmentActor>(
        "enrich-invoices");
    df.AddActorBlock<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[], InvoiceBulkUpdateActor>(
        "update-invoices");
    df.AddActorBlock<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[], NotificationActor>(
        "notify");
    
    // === STAGE 5: Cashflow Reallocation ===
    df.AddActorBlock<InvoiceEnrichmentContext[], CashflowReallocationChange, CashflowChangesProducerActor>(
        "identify-changes");
    df.AddBlock("batch-changes", sp =>
    {
        return new EpochBatchBlock<CashflowReallocationChange>(
            new BlockContext("invoice-reprocessing:batch-changes"),
            maxBatchSize: 10000,
            windowPeriod: TimeSpan.FromSeconds(2));
    });
    df.AddActorBlock<CashflowReallocationChange[], CashflowReallocationGroup[], ReallocationTransformerActor>(
        "reallocate-to-cashflows");
    df.AddActorBlock<CashflowReallocationGroup[], CashflowReallocationGroup[], ReallocationProcessorActor>(
        "process-reallocations");
    
    // === GRAPH DEFINITION ===
    df.AddGraph("main", g =>
    {
        // Add blocks to graph
        g.UseBlock("invoice-source");
        g.UseBlock("batch-invoices");
        g.UseBlock("rate-limit-buffer");
        g.UseBlock("load-counterparty");
        g.UseBlock("enrich-invoices");
        g.UseBlock("update-invoices");
        g.UseBlock("notify");
        g.UseBlock("identify-changes");
        g.UseBlock("batch-changes");
        g.UseBlock("reallocate-to-cashflows");
        g.UseBlock("process-reallocations");
        
        // Connect blocks in sequence
        g.Connect("invoice-source", "batch-invoices");
        g.Connect("batch-invoices", "rate-limit-buffer");
        g.Connect("rate-limit-buffer", "load-counterparty");
        g.Connect("load-counterparty", "enrich-invoices");
        g.Connect("enrich-invoices", "update-invoices");
        g.Connect("update-invoices", "notify");
        g.Connect("notify", "identify-changes");
        g.Connect("identify-changes", "batch-changes");
        g.Connect("batch-changes", "reallocate-to-cashflows");
        g.Connect("reallocate-to-cashflows", "process-reallocations");
    });
});
```

### Step 4: Execute the DataFlow

```csharp
var app = builder.Build();

// Resolve and execute the graph
var graph = app.Services.GetKeyedService<DataFlowGraph>("invoice-reprocessing:main");
var context = new ExecutionContext(app.Services, CancellationToken.None);
await graph!.ExecuteAsync(context);

Console.WriteLine("Invoice reprocessing completed!");
```

---

## Testing the Migrated Dataflow

Testing is critical for ensuring the migrated dataflow works correctly. We'll create comprehensive tests following the patterns from [Testing Guide](testing-guide.md).

**See**: [Testing Guide](testing-guide.md) for complete testing patterns and utilities.

### Test Strategy

1. **Unit Test Services** - Test business logic in isolation (fast, simple)
2. **Unit Test Actors** - Test actors with mock services
3. **Integration Test Pipeline** - Test complete dataflow end-to-end

### Test Setup

**Install Testing Packages:**

```xml
<ItemGroup>
  <PackageReference Include="xunit" Version="2.6.0" />
  <PackageReference Include="Shouldly" Version="4.2.1" />
  <PackageReference Include="NSubstitute" Version="5.1.0" />
</ItemGroup>
```

### Unit Tests for Services

Test business logic without DataFlow complexity:

```csharp
using Xunit;
using Shouldly;

public class InvoiceEnrichmentServiceTests
{
    [Fact]
    public void Enrich_Should_Apply_Business_Rules()
    {
        // Arrange
        var logger = Substitute.For<ILogger<InvoiceEnrichmentService>>();
        var service = new InvoiceEnrichmentService(logger);
        
        var batch = new[]
        {
            new InvoiceEnrichmentContext { SourceData = "INV001", Amount = 100m },
            new InvoiceEnrichmentContext { SourceData = "INV002", Amount = 200m }
        };
        
        // Act
        var result = service.Enrich(batch);
        
        // Assert
        result.Length.ShouldBe(2);
        result[0].EnrichedField1.ShouldBe("INV001_enriched");
        result[0].EnrichedField2.ShouldBe(110m);  // 100 * 1.1
        result[1].EnrichedField1.ShouldBe("INV002_enriched");
        result[1].EnrichedField2.ShouldBe(220m);  // 200 * 1.1
    }
    
    [Fact]
    public void Enrich_Should_Handle_Empty_Batch()
    {
        // Arrange
        var logger = Substitute.For<ILogger<InvoiceEnrichmentService>>();
        var service = new InvoiceEnrichmentService(logger);
        var batch = Array.Empty<InvoiceEnrichmentContext>();
        
        // Act
        var result = service.Enrich(batch);
        
        // Assert
        result.ShouldBeEmpty();
    }
}
```

### Unit Tests for Actors

Test actors with mock services:

```csharp
using DataFlow.POC.Tests.Helpers;  // Test utilities
using NSubstitute;

public class InvoiceEnrichmentActorTests
{
    [Fact]
    public async Task Actor_Should_Delegate_To_Service()
    {
        // Arrange - Mock the service
        var mockService = Substitute.For<IInvoiceEnrichmentService>();
        mockService.Enrich(Arg.Any<InvoiceEnrichmentContext[]>())
            .Returns(x => x.Arg<InvoiceEnrichmentContext[]>());  // Pass through
        
        var actor = new InvoiceEnrichmentActor(mockService);
        
        var batch1 = new[] { new InvoiceEnrichmentContext { SourceData = "INV001" } };
        var batch2 = new[] { new InvoiceEnrichmentContext { SourceData = "INV002" } };
        var input = TestStreams.FromArray(batch1, batch2);
        var context = TestContext.CreateActor();
        
        // Act
        var results = await TestStreams.CollectAsync(actor.RunAsync(input, context));
        
        // Assert
        results.Count.ShouldBe(2);
        mockService.Received(2).Enrich(Arg.Any<InvoiceEnrichmentContext[]>());
    }
    
    [Fact]
    public async Task Actor_Should_Respect_Cancellation()
    {
        // Arrange
        var mockService = Substitute.For<IInvoiceEnrichmentService>();
        var actor = new InvoiceEnrichmentActor(mockService);
        
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(10));
        
        var input = TestStreams.Integers(10000)
            .Select(i => new[] { new InvoiceEnrichmentContext { SourceData = $"INV{i}" } });
        var context = TestContext.CreateActor(cts.Token);
        
        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await TestStreams.CollectAsync(actor.RunAsync(input, context), cts.Token);
        });
    }
}
```

### Integration Tests for Complete Pipeline

Test the entire dataflow end-to-end:

```csharp
public class InvoiceReprocessingDataFlowIntegrationTests
{
    [Fact]
    public async Task Complete_Pipeline_Should_Process_All_Invoices()
    {
        // Arrange - Create test services with in-memory data
        var testInvoices = new[]
        {
            new InvoiceEnrichmentContext { Id = 1, SourceData = "INV001", Amount = 100m },
            new InvoiceEnrichmentContext { Id = 2, SourceData = "INV002", Amount = 200m },
            new InvoiceEnrichmentContext { Id = 3, SourceData = "INV003", Amount = 300m }
        };
        
        var mockRepository = CreateMockRepository(testInvoices);
        var mockLoader = Substitute.For<ICounterpartyInfoLoader>();
        mockLoader.LoadCounterpartyInfoAsync(Arg.Any<InvoiceEnrichmentContext[]>(), Arg.Any<CancellationToken>())
            .Returns(x => Task.FromResult(x.Arg<InvoiceEnrichmentContext[]>()));
        
        var enrichmentService = new InvoiceEnrichmentService(
            Substitute.For<ILogger<InvoiceEnrichmentService>>());
        
        var mockNotification = Substitute.For<INotificationService>();
        var mockChangeIdentifier = Substitute.For<ICashflowChangeIdentifier>();
        mockChangeIdentifier.IdentifyChanges(Arg.Any<InvoiceEnrichmentContext[]>())
            .Returns(Array.Empty<CashflowReallocationChange>());  // Simplified
        
        // Build service provider with test dependencies
        var services = new ServiceCollection();
        services.AddScoped<IInvoiceRepository>(_ => mockRepository);
        services.AddScoped<ICounterpartyInfoLoader>(_ => mockLoader);
        services.AddScoped<IInvoiceEnrichmentService>(_ => enrichmentService);
        services.AddScoped<INotificationService>(_ => mockNotification);
        services.AddScoped<ICashflowChangeIdentifier>(_ => mockChangeIdentifier);
        
        // Register actors
        services.AddScoped<InvoiceSource>();
        services.AddScoped<CounterpartyInfoLoaderActor>();
        services.AddScoped<InvoiceEnrichmentActor>();
        services.AddScoped<InvoiceBulkUpdateActor>();
        services.AddScoped<NotificationActor>();
        services.AddScoped<CashflowChangesProducerActor>();
        
        // Register DataFlow (simplified version for test)
        services.AddDataFlows("test-invoice-reprocessing", df =>
        {
            df.AddBlock("invoice-source", sp =>
            {
                var factory = sp.GetRequiredService<IServiceScopeFactory>();
                return new PlainSourceAdapter<InvoiceEnrichmentContext, InvoiceSource>(
                    new BlockContext("test-invoice-reprocessing:invoice-source"),
                    factory,
                    sourceName: "database");
            });
            
            df.AddBlock("batch-invoices", sp =>
            {
                return new EpochBatchBlock<InvoiceEnrichmentContext>(
                    new BlockContext("test-invoice-reprocessing:batch-invoices"),
                    maxBatchSize: 2,  // Small batch for testing
                    windowPeriod: TimeSpan.FromSeconds(1));
            });
            
            df.AddActorBlock<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[], CounterpartyInfoLoaderActor>(
                "load-counterparty");
            df.AddActorBlock<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[], InvoiceEnrichmentActor>(
                "enrich-invoices");
            df.AddActorBlock<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[], InvoiceBulkUpdateActor>(
                "update-invoices");
            
            df.AddGraph("main", g =>
            {
                g.UseBlock("invoice-source");
                g.UseBlock("batch-invoices");
                g.UseBlock("load-counterparty");
                g.UseBlock("enrich-invoices");
                g.UseBlock("update-invoices");
                
                g.Connect("invoice-source", "batch-invoices");
                g.Connect("batch-invoices", "load-counterparty");
                g.Connect("load-counterparty", "enrich-invoices");
                g.Connect("enrich-invoices", "update-invoices");
            });
        });
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act
        var graph = serviceProvider.GetKeyedService<DataFlowGraph>("test-invoice-reprocessing:main");
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        await graph!.ExecuteAsync(context);
        
        // Assert
        await mockRepository.Received().BulkUpdateAsync(
            Arg.Is<InvoiceEnrichmentContext[]>(batch => batch.Length > 0),
            Arg.Any<CancellationToken>());
        
        await mockNotification.DidNotReceive().NotifyAsync(
            Arg.Any<InvoiceEnrichmentContext[]>(),
            Arg.Any<CancellationToken>());  // Not in simplified test graph
    }
    
    private IInvoiceRepository CreateMockRepository(InvoiceEnrichmentContext[] invoices)
    {
        var repo = Substitute.For<IInvoiceRepository>();
        
        repo.GetInvoicesForReprocessingAsync(Arg.Any<CancellationToken>())
            .Returns(invoices.ToAsyncEnumerable());
        
        repo.BulkUpdateAsync(Arg.Any<InvoiceEnrichmentContext[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        
        return repo;
    }
}
```

### Test Helper Utilities

The POC library provides test helpers to reduce boilerplate:

```csharp
using DataFlow.POC.Tests.Helpers;

// Create test streams
var stream = TestStreams.FromArray(item1, item2, item3);
var stream = TestStreams.Integers(10);  // 1, 2, 3, ..., 10
var stream = TestStreams.Empty<InvoiceEnrichmentContext>();

// Collect results
var results = await TestStreams.CollectAsync(stream);

// Create execution contexts
var context = TestContext.CreateActor();
var context = TestContext.CreateActor(cancellationToken);
var context = TestContext.CreateExecution(serviceProvider);

// Build test service provider
var scopeFactory = TestServiceBuilder.Create()
    .WithActor<MyActor>()
    .WithScoped(mockService)
    .BuildScopeFactory();
```

**See**: [Testing Guide - Test Helper Utilities](testing-guide.md#test-helper-utilities) for complete details.

---

## Migration Checklist

Use this checklist during your migration PR to ensure nothing is missed:

### Pre-Migration

- [ ] Understand the legacy dataflow structure
- [ ] Identify all block types used
- [ ] Map topology patterns (sequential, batching, rate limiting, etc.)
- [ ] List all service dependencies
- [ ] Review POC guides: [Getting Started](getting-started.md), [Business Logic Decoupling](business-logic-decoupling.md), [Testing Guide](testing-guide.md)

### Business Logic Extraction

- [ ] Define service interfaces for all business logic
- [ ] Implement services (pure business logic, no DataFlow)
- [ ] Unit test services in isolation
- [ ] Register services in DI container

### Actor Implementation

- [ ] Create actors for each block
- [ ] Actors delegate to services (thin orchestration)
- [ ] Actors handle cancellation properly
- [ ] Unit test actors with mock services

### DataFlow Configuration

- [ ] Install POC package (`DataFlow`)
- [ ] Register actors in DI
- [ ] Define blocks using `AddBlock`, `AddActorBlock`, `AddBatchBlock`, etc.
- [ ] Build graph topology with `.ProcessWith()` and connections
- [ ] Configure batch blocks (MaxBatchSize, WindowPeriod)
- [ ] Configure buffer blocks for rate limiting (if needed)
- [ ] Test graph resolves correctly from service provider

### Testing

- [ ] Unit tests for all services (fast, simple)
- [ ] Unit tests for all actors (with mocks)
- [ ] Integration test for complete pipeline
- [ ] Test cancellation scenarios
- [ ] Test empty input handling
- [ ] Test error propagation (if applicable)

### Deployment

- [ ] Both packages coexist (`Uniun.DataFlow` + `DataFlow`)
- [ ] Legacy dataflow still works (unchanged)
- [ ] POC dataflow deployed and tested
- [ ] Performance validated (throughput, memory)
- [ ] Monitoring and logging in place
- [ ] Documentation updated

### Post-Migration

- [ ] Monitor POC dataflow in production
- [ ] Compare metrics with legacy flow
- [ ] Remove legacy dataflow when confident
- [ ] Remove `Uniun.DataFlow` package reference
- [ ] Celebrate! 🎉

---

## Summary

This guide provided a complete, hands-on case study for migrating the invoice reprocessing dataflow from the legacy `Uniun.DataFlow` library to the new POC DataFlow library.

### Key Takeaways

1. **Side-by-Side Migration**: Both packages can coexist during migration
2. **Pattern Mapping**: Legacy blocks map directly to POC equivalents
3. **Business Logic Decoupling**: Separate services from actors for testability
4. **Topology-Level Concurrency**: Use buffer blocks and competing consumers instead of `MaxConcurrency`
5. **Comprehensive Testing**: Unit test services, unit test actors, integration test pipeline

### Next Steps

- **Start migrating** your first dataflow using this guide as a template
- **Review POC guides** for specific patterns:
  - [Getting Started](getting-started.md)
  - [Business Logic Decoupling](business-logic-decoupling.md)
  - [Testing Guide](testing-guide.md)
  - [Competing Consumers Topology](topology-competing-consumers.md)
  - [Selective Routing Topology](topology-selective-routing.md)
- **Ask questions** if you encounter challenges

---

**Document Version**: 1.0  
**Status**: ✅ Ready for Use  
**Last Updated**: 2025-12-10
