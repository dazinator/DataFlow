# Import Dependency and Concurrent Routing Patterns

## Overview

This document explores design patterns for handling import dependencies and concurrent routing in DataFlow, specifically addressing scenarios where:

1. **Sequential Dependencies**: Some data must be processed before other data (e.g., currencies before accounts)
2. **Concurrent Processing**: After dependencies are met, multiple downstream tasks should run in parallel
3. **Data Distribution**: A single input source needs to send data (or derived data) to multiple downstream blocks

## Problem Statement

### Use Case: Financial Data Import

**Scenario**: Importing invoices that reference core data (currencies, entity codes, GL accounts, bank accounts).

**Requirements**:
- Currencies must be imported first (other entities depend on currency IDs)
- Once currencies are imported, invoices need their currency reference updated
- After currency import, concurrently process: entity codes, bank accounts, GL accounts
- All these tasks work with the same or derived data from the source

### Current DataFlow Limitations

1. **Router Limitation**: The `StructuredRoutingBlock` routes each item to exactly ONE route based on a selector function
2. **Single Consumer**: Each block output is typically consumed by one downstream block
3. **No Built-in Broadcast**: No native way to fan-out data to multiple concurrent consumers

## Proposed Solutions

### Solution 1: Broadcast Block (Recommended)

**Concept**: A new block type that takes input and broadcasts/fans it out to multiple downstream consumers concurrently.

**Key Features**:
- Accepts a single input stream
- Outputs to multiple named targets concurrently
- Supports optional transformation per target (e.g., extract different fields for different consumers)
- Provides backpressure management across all targets
- Integrates cleanly with the structured builder API

**API Design**:

```csharp
builder.AddProducer("invoices", sp => new InvoiceProducer())
    .AddBroadcast<Invoice>("fanout", options =>
    {
        // Define multiple targets with optional transformations
        options.AddTarget("currency-extractor", invoice => invoice.Currency);
        options.AddTarget("entity-extractor", invoice => invoice.Entity);
        options.AddTarget("gl-extractor", invoice => invoice.GLAccount);
        options.AddTarget("bank-extractor", invoice => invoice.BankAccount);
    })
    .ReceiveFrom("invoices");

// Each target can be consumed independently
builder.AddProcessor("currency-importer", sp => new CurrencyImporter())
    .ReceiveFrom("fanout", targetName: "currency-extractor");
    
builder.AddProcessor("entity-importer", sp => new EntityImporter())
    .ReceiveFrom("fanout", targetName: "entity-extractor");
```

**Advantages**:
- Clean separation of concerns
- Natural backpressure across targets
- Composable with existing blocks
- Type-safe when using transformations

**Challenges**:
- Need to coordinate completion of all targets
- Backpressure from one slow target affects all others
- Memory overhead for buffering to multiple targets

### Solution 2: Dependency-Aware Sequential + Concurrent Pattern

**Concept**: Use orchestration within a custom block to handle sequential dependencies, then fan out to concurrent processing.

**Implementation Approach**:

```csharp
// Custom orchestrator block that handles the dependency logic
public class ImportOrchestrator : IStreamTransformer<Invoice, ImportResult>
{
    public async IAsyncEnumerable<ImportResult> TransformAsync(
        IDataFlowContext context,
        IAsyncEnumerable<Invoice> input,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var invoice in input.WithCancellation(ct))
        {
            // Step 1: Import currency first (sequential dependency)
            var currencyId = await ImportCurrencyAsync(invoice.Currency, ct);
            
            // Step 2: Update invoice with currency ID
            invoice.CurrencyId = currencyId;
            
            // Step 3: Launch concurrent imports for other entities
            var tasks = new[]
            {
                ImportEntityAsync(invoice.Entity, ct),
                ImportGLAccountAsync(invoice.GLAccount, currencyId, ct),
                ImportBankAccountAsync(invoice.BankAccount, currencyId, ct)
            };
            
            await Task.WhenAll(tasks);
            
            yield return new ImportResult { Invoice = invoice, Success = true };
        }
    }
}

// Usage in flow
builder.AddProducer("invoices", sp => new InvoiceProducer())
    .AddTransform("orchestrator", sp => new ImportOrchestrator())
    .ReceiveFrom("invoices")
    .AddProcessor("result-handler", sp => new ResultProcessor())
    .ReceiveFrom("orchestrator");
```

**Advantages**:
- Full control over dependency logic
- Simple to implement
- No new block types needed

**Disadvantages**:
- Couples dependency logic with data processing
- Less composable
- Harder to reuse patterns across flows
- No separation of concerns

### Solution 3: Staged Pipeline with Routing

**Concept**: Use multiple stages with routing to control the flow of dependencies.

**Implementation**:

```csharp
// Stage 1: Import currencies
builder.AddProducer("invoices", sp => new InvoiceProducer())
    .AddTransform("currency-importer", sp => new CurrencyImporter())
    .ReceiveFrom("invoices");

// Stage 2: Route by entity type to concurrent processors
builder.AddRouter<EnrichedInvoice>("router", inv => inv.EntityType)
    .RegisterRoute("entity", context =>
    {
        var rb = context.RouteBuilder;
        rb.AddProcessor("entity-importer", sp => new EntityImporter()).AsEntry();
        return rb.Build();
    })
    .RegisterRoute("gl", context =>
    {
        var rb = context.RouteBuilder;
        rb.AddProcessor("gl-importer", sp => new GLAccountImporter()).AsEntry();
        return rb.Build();
    })
    .RegisterRoute("bank", context =>
    {
        var rb = context.RouteBuilder;
        rb.AddProcessor("bank-importer", sp => new BankAccountImporter()).AsEntry();
        return rb.Build();
    })
    .ReceiveFrom("currency-importer");
```

**Advantages**:
- Uses existing routing infrastructure
- Good for heterogeneous data

**Disadvantages**:
- Doesn't solve the "broadcast one item to multiple routes" problem
- Each invoice can only go to one route
- Not suitable for concurrent processing of the same item

### Solution 4: Multi-Output Source Block with Type Discrimination

**Concept**: A source block that implements multiple `ISourceBlock<T>` interfaces for different output types.

**Theoretical API**:

```csharp
public class MultiOutputInvoiceSource : 
    ISourceBlock<Currency>, 
    ISourceBlock<Entity>, 
    ISourceBlock<GLAccount>
{
    // Each GetAsyncEnumerable returns a different projected stream
}
```

**Challenges**:
- Current DataFlow architecture expects `ISourceBlock<T>` with a single type parameter
- Connecting blocks would need significant changes
- Type safety becomes complex
- Not compatible with current builder patterns

**Verdict**: Not recommended without major architectural changes.

## Recommendation: Implement BroadcastBlock

After analyzing the options, **Solution 1 (BroadcastBlock)** is the most practical approach because:

1. **Composable**: Fits naturally into the existing block architecture
2. **Reusable**: Can be used in many scenarios, not just import dependencies
3. **Type-Safe**: Leverages generics for compile-time safety
4. **Flexible**: Supports both simple broadcast and transformation per target
5. **Backpressure-Aware**: Integrates with the pull-based architecture

## BroadcastBlock Design

### Architecture

```
                    ┌──────────────────┐
                    │  BroadcastBlock  │
                    │   ITargetBlock   │
                    └────────┬─────────┘
                             │
                    ┌────────▼─────────┐
                    │  Source Stream   │
                    └────────┬─────────┘
                             │
           ┌─────────────────┼─────────────────┐
           │                 │                 │
    ┌──────▼──────┐   ┌──────▼──────┐   ┌─────▼──────┐
    │   Target 1  │   │   Target 2  │   │  Target 3  │
    │   Channel   │   │   Channel   │   │  Channel   │
    └──────┬──────┘   └──────┬──────┘   └─────┬──────┘
           │                 │                 │
      Consumers          Consumers         Consumers
```

### Key Implementation Details

1. **Multiple Output Channels**: Each target gets its own channel with backpressure
2. **Concurrent Write**: Items are written to all targets concurrently using `Task.WhenAll`
3. **Shared Cancellation**: All targets share the same cancellation token
4. **Target Factories**: Each target can have an optional transformation function
5. **Completion Propagation**: When source completes, all target channels complete

### Usage Patterns

#### Pattern 1: Simple Broadcast (Same Type)

```csharp
// Broadcast invoices to multiple processors
builder.AddBroadcast<Invoice>("invoice-fanout")
    .WithTarget("validator")
    .WithTarget("archiver")
    .WithTarget("notifier")
    .ReceiveFrom("invoices");

builder.AddProcessor("validator", sp => new InvoiceValidator())
    .ReceiveFrom("invoice-fanout", target: "validator");
```

#### Pattern 2: Transformation per Target (Different Types)

```csharp
// Extract different data for different importers
builder.AddBroadcast<Invoice, object>("data-extractor")
    .WithTarget<Currency>("currency", inv => inv.Currency)
    .WithTarget<Entity>("entity", inv => inv.Entity)
    .WithTarget<GLAccount>("gl", inv => inv.GLAccount)
    .ReceiveFrom("invoices");

builder.AddProcessor<Currency>("currency-importer", sp => new CurrencyImporter())
    .ReceiveFrom("data-extractor", target: "currency");
```

#### Pattern 3: Dependency with Concurrent Processing

```csharp
// Step 1: Process sequential dependency
builder.AddProducer("invoices", sp => new InvoiceProducer())
    .AddTransform("currency-enricher", sp => new CurrencyEnricher())
    .ReceiveFrom("invoices");

// Step 2: Broadcast enriched data to concurrent processors
builder.AddBroadcast<EnrichedInvoice>("concurrent-fanout")
    .WithTarget("entity")
    .WithTarget("gl")
    .WithTarget("bank")
    .ReceiveFrom("currency-enricher");

// Step 3: Connect concurrent importers
builder.AddProcessor("entity-importer", sp => new EntityImporter())
    .ReceiveFrom("concurrent-fanout", target: "entity");
    
builder.AddProcessor("gl-importer", sp => new GLAccountImporter())
    .ReceiveFrom("concurrent-fanout", target: "gl");
    
builder.AddProcessor("bank-importer", sp => new BankAccountImporter())
    .ReceiveFrom("concurrent-fanout", target: "bank");
```

## Implementation Considerations

### Backpressure Strategy

The BroadcastBlock must handle backpressure from multiple targets:

**Option A: Wait for All (Slower Consumer Wins)**
- Write to all targets concurrently with `Task.WhenAll`
- Slowest target determines throughput
- Fair but can be bottlenecked by one slow consumer

**Option B: Independent Channels (Faster)**
- Each target has large buffer
- Write completes when all buffers accept
- Risk of memory growth if consumers have different speeds

**Recommendation**: Start with Option A (Wait for All) for correctness and predictability.

### Completion and Error Handling

1. **Source Completion**: When source completes, complete all target channels
2. **Target Errors**: If a target channel throws on write, propagate error and fail the block
3. **Cancellation**: Respect cancellation token across all operations

### DI Scoping

- BroadcastBlock itself runs in the flow's scope
- Each downstream consumer can have its own scope (as usual for blocks)
- Target transformations run in the broadcast block's scope

## Testing Strategy

1. **Unit Tests**:
   - Broadcast to multiple targets
   - Backpressure from slow consumer
   - Completion propagation
   - Error handling
   - Transformation per target

2. **Integration Tests**:
   - Full import dependency scenario
   - Concurrent processing verification
   - Performance under load

3. **Performance Tests**:
   - Compare with sequential processing
   - Measure overhead of broadcasting
   - Memory usage with multiple targets

## Migration Path

Existing flows can gradually adopt BroadcastBlock:

1. **No Breaking Changes**: Existing blocks and APIs remain unchanged
2. **Opt-In**: Use BroadcastBlock only where needed
3. **Composable**: Mix with existing routing, transforms, etc.

## Future Enhancements

1. **Selective Broadcasting**: Predicate-based target selection
2. **Dynamic Targets**: Add/remove targets at runtime
3. **Priority Targets**: Some targets get items before others
4. **Multicast Groups**: Logical grouping of related targets

## Conclusion

The **BroadcastBlock** pattern provides a clean, composable solution for concurrent routing and dependency handling in DataFlow. It respects the existing architecture, provides natural backpressure management, and enables powerful patterns for complex data import scenarios.

## Practical Examples

Comprehensive examples demonstrating these patterns can be found in:
- `/src/Tests/DataFlow/BroadcastBlockTests.cs` - Unit tests for BroadcastBlock functionality
- `/src/Tests/DataFlow/Examples/ImportDependencyExampleTests.cs` - Real-world import scenarios including:
  - Simple concurrent processing (validation, archival, notification)
  - Sequential dependency followed by concurrent processing (currency → entities/accounts)
  - Complex multi-stage import with reconciliation

### Quick Example: Financial Import with Dependency

```csharp
// Step 1: Read invoices
builder.AddProducer("invoices", sp => new InvoiceProducer());

// Step 2: Import currencies first (sequential dependency)
builder.AddTransform<ImportInvoice, EnrichedInvoice>(
    "currency-importer", 
    sp => new CurrencyImporter())
    .ReceiveFrom("invoices");

// Step 3: Broadcast to concurrent importers
var broadcast = builder.AddBroadcast<EnrichedInvoice>("concurrent-import", options =>
{
    options.AddTarget<string>("entity", inv => inv.Entity);
    options.AddTarget<string>("gl", inv => inv.GLAccount);
    options.AddTarget<string>("bank", inv => inv.BankAccount);
})
.ReceiveFrom("currency-importer");

// Step 4: Connect concurrent importers
broadcast
    .AddProcessorForTarget<string>("entity-importer", "entity", 
        sp => new EntityImporter())
    .AddProcessorForTarget<string>("gl-importer", "gl", 
        sp => new GLAccountImporter())
    .AddProcessorForTarget<string>("bank-importer", "bank", 
        sp => new BankAccountImporter());
```

This pattern ensures:
1. ✅ Currencies are imported first
2. ✅ All invoices are enriched with currency IDs
3. ✅ Entity, GL, and bank account imports run concurrently
4. ✅ Natural backpressure prevents overwhelming downstream systems
5. ✅ Clean separation of concerns with composable blocks
