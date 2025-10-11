# BroadcastBlock Usage Guide

## Overview

The **BroadcastBlock** is a specialized block that fans out input items to multiple named targets concurrently. It enables the **broadcast/fan-out pattern** where the same data (or transformations of it) needs to be processed by multiple independent consumers simultaneously.

## When to Use BroadcastBlock

Use BroadcastBlock when you need to:

1. **Concurrent Processing**: Send the same item to multiple processors that run independently
2. **Data Extraction**: Extract different fields from a complex object for specialized processing
3. **Multi-Stage Pipelines**: Fan out after a dependency stage to process multiple aspects concurrently
4. **Parallel Validation/Archival/Notification**: Process an item through multiple independent workflows

## Basic Usage

### Simple Broadcast (Same Type)

The simplest use case: broadcast items to multiple processors of the same type.

```csharp
var builder = new StructuredDataFlowBuilder(sp, "SimpleBroadcast");

// Add source
builder.AddProducer("invoices", sp => new InvoiceProducer());

// Add broadcast block (no pre-configuration needed for simple cases)
builder.AddBroadcast<Invoice>("fanout")
    .ReceiveFrom("invoices");

// Downstream blocks connect using standard API
builder.AddProcessor("validator", sp => new InvoiceValidator())
    .ReceiveFrom("fanout");
builder.AddProcessor("archiver", sp => new InvoiceArchiver())
    .ReceiveFrom("fanout");
builder.AddProcessor("notifier", sp => new InvoiceNotifier())
    .ReceiveFrom("fanout");

var flow = builder.Build();
await flow.ExecuteAsync(context);
```

**What happens:**
- Each invoice is sent to **all three** processors concurrently
- Processors run independently - one slow processor doesn't block others (beyond backpressure)
- All processors receive all items
- Channels are created on-demand when processors connect

### Broadcast with Clone Functions (Thread Safety)

Control whether items are cloned or passed by reference to each target.

```csharp
var builder = new StructuredDataFlowBuilder(sp, "ThreadSafeBroadcast");

builder.AddProducer("invoices", sp => new InvoiceProducer());

// Broadcast with default cloning and per-target overrides
builder.AddBroadcast<Invoice>("fanout",
    defaultCloneFunc: inv => inv with { })  // Default: clone for all (records)
    .WithTarget("validator", clone: null)   // Override: validator doesn't modify, no cloning needed
    .WithTarget("archiver", clone: inv => inv.DeepClone())  // Override: deep clone for archival
    .ReceiveFrom("invoices");

// Connect processors using standard API
builder.AddProcessor("validator", sp => new InvoiceValidator())
    .ReceiveFrom("fanout");
builder.AddProcessor("archiver", sp => new InvoiceArchiver())
    .ReceiveFrom("fanout");
builder.AddProcessor("modifier", sp => new InvoiceModifier())  // Uses default clone
    .ReceiveFrom("fanout");
```

**What happens:**
- **validator** receives the original reference (no cloning overhead)
- **archiver** receives a deep clone (ensures archival integrity)
- **modifier** receives a shallow clone (uses default clone function)
- All three processes run concurrently without interfering with each other's data

## Advanced Patterns

### Sequential Dependency + Concurrent Processing

Handle dependencies (e.g., import currencies first) then fan out for concurrent processing.

```csharp
var builder = new StructuredDataFlowBuilder(sp, "DependencyFlow");

// Step 1: Source
builder.AddProducer("invoices", sp => new InvoiceProducer());

// Step 2: Handle dependency (currencies must exist first)
builder.AddTransform<Invoice, EnrichedInvoice>(
    "currency-enricher",
    sp => new CurrencyEnricher())
.ReceiveFrom("invoices");

// Step 3: Fan out for concurrent processing
builder.AddBroadcast<EnrichedInvoice>("concurrent-import")
    .ReceiveFrom("currency-enricher");

// Step 4: Concurrent importers connect using standard API
builder.AddProcessor("entity-importer", sp => new EntityImporter())
    .ReceiveFrom("concurrent-import");
builder.AddProcessor("gl-importer", sp => new GLAccountImporter())
    .ReceiveFrom("concurrent-import");
builder.AddProcessor("bank-importer", sp => new BankAccountImporter())
    .ReceiveFrom("concurrent-import");
```

**Flow:**
```
Invoices → Currency Enricher → ┬→ Entity Importer
                                ├→ GL Account Importer
                                └→ Bank Account Importer
```

**Note:** If you need to extract different properties from EnrichedInvoice for each importer, add transform blocks before the importers.

### Chaining Broadcasts

You can chain broadcasts for multi-stage fan-out.

```csharp
builder.AddProducer("orders", sp => new OrderProducer());

// First broadcast: fan out to multiple processors
builder.AddBroadcast<Order>("order-fanout")
    .ReceiveFrom("orders");

// Connect processors - each can continue the pipeline
builder.AddProcessor("standard-processor", sp => new StandardOrderProcessor())
    .ReceiveFrom("order-fanout");

builder.AddTransform<Order, ProcessedOrder>("urgent-processor", sp => new UrgentProcessor())
    .ReceiveFrom("order-fanout");

// Second broadcast from the urgent processor
builder.AddBroadcast<ProcessedOrder>("urgent-fanout")
    .ReceiveFrom("urgent-processor");

builder.AddProcessor("notification", sp => new NotificationProcessor())
    .ReceiveFrom("urgent-fanout");
builder.AddProcessor("archival", sp => new ArchivalProcessor())
    .ReceiveFrom("urgent-fanout");
```

## Configuration Options

### Default Clone Function

Provide a default cloning strategy for all targets:

```csharp
// For records (shallow copy)
builder.AddBroadcast<Invoice>("fanout", 
    defaultCloneFunc: inv => inv with { });

// For ICloneable types
builder.AddBroadcast<MyData>("fanout",
    defaultCloneFunc: data => (MyData)data.Clone());

// For immutable types (no cloning needed)
builder.AddBroadcast<int>("fanout");  // defaultCloneFunc defaults to null
```

### Per-Target Clone Configuration

Override the default clone function for specific targets:

```csharp
builder.AddBroadcast<Invoice>("fanout",
    defaultCloneFunc: inv => inv with { })  // Default: shallow clone
    .WithTarget("validator", clone: null)   // Override: no cloning for validator
    .WithTarget("archiver", clone: inv => inv.DeepClone())  // Override: deep clone
    .ReceiveFrom("source");
```

**How it works:**
1. When a target connects, BroadcastBlock matches it by block name
2. If `.WithTarget(blockName, ...)` was called, uses that clone function
3. Otherwise, uses the `defaultCloneFunc`
4. If both are null, passes the same reference (suitable for immutable data)

### Block Options

Standard block options apply:

```csharp
builder.AddBroadcast<Invoice>("fanout",
    defaultCloneFunc: inv => inv with { },
    options: new BlockOptions
    {
        // Note: Capacity is ignored for BroadcastBlock target channels
        // Each target channel has a fixed capacity of 1 for backpressure management
        // MaxConcurrency is typically kept at 1 (default)
        MaxConcurrency = 1
    });
```

**Note:** The BroadcastBlock uses bounded channels with capacity = 1 for each subscriber to maintain backpressure without buffering. If you need buffering, add a BufferBlock downstream.

## Backpressure Behavior

The BroadcastBlock uses a **"slowest wins"** backpressure strategy:

- Each target has its own bounded channel (capacity = 1)
- When writing an item, the block waits for **all** targets to accept it
- If one target is slow (channel full), all targets wait
- This ensures all targets stay synchronized without unbounded memory growth

**Example:**
```
Target A: Fast processor (channel rarely full)
Target B: Slow processor (channel often full)
Target C: Medium processor

The broadcast block will slow down to match Target B's pace.
All targets stay approximately in sync (within 1 item).
```

**Mitigation:**
- Optimize slow target processing
- Consider if the slow target really needs all items
- Add a BufferBlock downstream of slow targets if you need more decoupling:
  ```csharp
  builder.AddBroadcast<Invoice>("fanout").ReceiveFrom("source");
  
  // Add buffer before slow processor
  builder.AddBuffer<Invoice>("slow-buffer", capacity: 100);  // Future: BufferBlock not yet implemented
  builder.AddProcessor("slow", sp => new SlowProcessor()).ReceiveFrom("slow-buffer");
  ```

## Performance Considerations

### Concurrency

- BroadcastBlock itself uses `MaxConcurrency = 1` by default (sequential broadcasting)
- Target processors can have their own concurrency settings
- Concurrent writing to all targets happens via `Task.WhenAll`

### Memory Usage

- Each target has a bounded channel with capacity = 1
- Memory scales with: `number_of_targets × item_size`
- Very low memory footprint compared to buffered approaches
- No risk of unbounded memory growth

### Throughput

- Limited by the slowest target (backpressure)
- Concurrent processing of targets can improve overall throughput
- Monitor channel metrics to identify bottlenecks

## Error Handling

### Channel Write Operations

Channel writes don't fail under normal circumstances:
- Backpressure causes waiting (async operation), not exceptions
- `OperationCanceledException` propagates naturally when the flow is cancelled
- No special error handling needed for channel operations

### Completion

- When the source completes, all target channels are completed
- Downstream blocks continue processing buffered items
- The flow completes when all targets finish

## Comparison with Router

| Feature | BroadcastBlock | RouterBlock |
|---------|----------------|-------------|
| **Destination** | All connected targets | One target (selected by key) |
| **Use Case** | Fan-out, parallel processing | Conditional routing, partitioning |
| **Item Count** | N items → N × targets items | N items → N items (distributed) |
| **Configuration** | Optional per-target clone functions | Route selector function |
| **Backpressure** | All targets affect source (slowest wins) | Selected target affects source |
| **Connection** | Targets connect via `.ReceiveFrom()` | Routes are sub-dataflows |

**Use BroadcastBlock when:** You want every target to process every item

**Use RouterBlock when:** You want to send each item to one target based on a condition

## Best Practices

1. **Use WithTarget for per-target configuration**: Configure clone functions only for targets that need them

2. **Default to immutable or cloning**: Either use immutable data types or provide a default clone function to avoid concurrent access issues

3. **Monitor backpressure**: Use metrics to identify slow targets that bottleneck the flow

4. **Test concurrency**: Ensure target processors handle concurrent load properly

5. **Keep clone functions simple**: Complex logic should be in dedicated transform blocks, not in clone functions

6. **Document flow**: Complex broadcast patterns benefit from flow diagrams

7. **Connect with standard API**: Always use `.ReceiveFrom("broadcast-name")` - no special connection methods needed

## Common Pitfalls

### ❌ Broadcasting Mutable Objects Without Cloning

**Problem:** Multiple targets receive the same object reference and can mutate it concurrently.

**Bad:**
```csharp
builder.AddBroadcast<MutableInvoice>("fanout")  // No clone function!
    .ReceiveFrom("source");

// Both processors receive same reference and may mutate it
builder.AddProcessor("modifier1", ...).ReceiveFrom("fanout");
builder.AddProcessor("modifier2", ...).ReceiveFrom("fanout");
```

**Better:**
```csharp
builder.AddBroadcast<MutableInvoice>("fanout",
    defaultCloneFunc: inv => inv.Clone())  // Clone for safety
    .ReceiveFrom("source");
```

### ❌ Ignoring Backpressure

Monitor for slow targets that bottleneck the entire flow. All targets must keep pace.

### ❌ Complex Clone Functions

Keep clone functions simple. Complex logic belongs in transform blocks.

**Bad:**
```csharp
defaultCloneFunc: item => {
    // 50 lines of complex transformation logic
    return result;
}
```

**Better:**
```csharp
builder.AddTransform("complex-transform", sp => new ComplexTransformer())
    .ReceiveFrom("source");
builder.AddBroadcast("fanout", defaultCloneFunc: item => item.Clone())
    .ReceiveFrom("complex-transform");
```

## Examples

See `/src/Tests/DataFlow/BroadcastBlockTests.cs` for complete working examples including:
- Simple fan-out to multiple processors
- Clone function verification with mutable objects
- Per-target clone configuration with different transformations
- Proper completion handling
