# Import Dependency and Concurrent Routing - Implementation Summary

## Overview

This document summarizes the solution implemented to address the issue: *"Investigate Design Patterns for Import Dependency and Concurrent Routing in DataFlow"*.

## Problem Statement

The core challenge was processing financial data imports where:

1. **Sequential Dependencies** exist: Currencies must be imported before GL accounts and bank accounts (which need currency IDs)
2. **Concurrent Processing** is needed: After currency import, entity codes, bank accounts, and GL accounts should be processed concurrently
3. **Routing Limitation**: The existing DataFlow routing allows an item to be sent to only ONE route, but we need to send data to MULTIPLE routes for concurrent processing

## Solution: BroadcastBlock

We implemented a **BroadcastBlock** that enables the broadcast/fan-out pattern where items are sent to multiple downstream processors concurrently.

### Key Features

- **Fan-out to Multiple Targets**: Each input item is broadcast to all registered targets
- **Concurrent Processing**: All targets process items independently and concurrently
- **Per-Target Transformations**: Optionally extract different data for each target
- **Backpressure Management**: Uses "slowest wins" strategy - all targets must accept before proceeding
- **Type-Safe**: Full generic support with compile-time type checking
- **Composable**: Integrates seamlessly with existing DataFlow blocks

## Architecture

### Block Structure

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
    Processor 1       Processor 2      Processor 3
```

### Key Components

1. **BroadcastBlock<T>**: Main block that manages broadcasting
2. **BroadcastTarget<TIn, TOut>**: Represents each output target with optional transformation
3. **BroadcastBlockOptions<T>**: Configuration for targets
4. **StructuredBroadcastBlockBuilder<T>**: Fluent API builder
5. **BroadcastTargetAdapter<T>**: Internal adapter to connect targets to downstream blocks

## Usage Examples

### Example 1: Simple Concurrent Processing

```csharp
var builder = new StructuredDataFlowBuilder(sp, "ConcurrentProcessing");

builder.AddProducer("invoices", sp => new InvoiceProducer());

builder.AddBroadcast<Invoice>("fanout", 
    defaultCloneFunc: inv => inv with { })  // Clone by default for thread safety
    .WithTarget("validator", clone: null)     // Override: no cloning needed
    .ReceiveFrom("invoices");

// Standard connections
builder.AddProcessor<Invoice>("validator", sp => new InvoiceValidator()).ReceiveFrom("fanout");
builder.AddProcessor<Invoice>("archiver", sp => new InvoiceArchiver()).ReceiveFrom("fanout");
builder.AddProcessor<Invoice>("notifier", sp => new InvoiceNotifier()).ReceiveFrom("fanout");
```

**Result**: Each invoice is sent to all three processors concurrently.

### Example 2: Sequential Dependency + Concurrent Processing

This example directly addresses the original issue:

```csharp
var builder = new StructuredDataFlowBuilder(sp, "ImportDependencyFlow");

// Step 1: Read invoices
builder.AddProducer("invoices", sp => new InvoiceProducer());

// Step 2: Import currencies first (sequential dependency)
builder.AddTransform<ImportInvoice, EnrichedInvoice>(
    "currency-importer", 
    sp => new CurrencyImporter(),
    null)
.ReceiveFrom("invoices");

// Step 3: Broadcast enriched invoices to concurrent importers
builder.AddBroadcast<EnrichedInvoice>("concurrent-import",
    defaultCloneFunc: inv => inv with { })  // Clone for thread safety
    .ReceiveFrom("currency-importer");

// Step 4: Connect concurrent importers using standard API
builder.AddProcessor<EnrichedInvoice>("entity-importer", sp => new EntityImporter()).ReceiveFrom("concurrent-import");
builder.AddProcessor<EnrichedInvoice>("gl-importer", sp => new GLAccountImporter()).ReceiveFrom("concurrent-import");
builder.AddProcessor<EnrichedInvoice>("bank-importer", sp => new BankAccountImporter()).ReceiveFrom("concurrent-import");
```

**Flow:**
```
Invoices 
  → Currency Importer (sequential dependency)
    → Broadcast → ┬→ Entity Importer    (concurrent)
                  ├→ GL Account Importer (concurrent)
                  └→ Bank Account Importer (concurrent)
```

**This pattern ensures:**
1. ✅ Currencies are imported first (sequential)
2. ✅ All invoices are enriched with currency IDs
3. ✅ Entity, GL, and bank account imports run concurrently
4. ✅ Natural backpressure prevents overwhelming downstream systems

### Example 3: Per-Target Transformations

```csharp
builder.AddBroadcast<Invoice>("extractor", options =>
{
    options.AddTarget<string>("currency", inv => inv.Currency);
    options.AddTarget<decimal>("amount", inv => inv.Amount);
    options.AddTarget<int>("lineCount", inv => inv.LineItems.Count);
})
.ReceiveFrom("invoices");
```

**Result**: Different data types extracted for specialized processing.

## Design Comparison

We evaluated 4 different approaches:

| Approach | Pros | Cons | Verdict |
|----------|------|------|---------|
| **BroadcastBlock** | Composable, reusable, type-safe, clean API | Needs new block type | ✅ **Recommended** |
| **Orchestrator Block** | Simple, full control | Tightly coupled, not reusable | ❌ Not scalable |
| **Staged Pipeline** | Uses existing routing | Can't broadcast to multiple routes | ❌ Doesn't solve problem |
| **Multi-Output Source** | Theoretical | Major architecture changes | ❌ Not practical |

**Conclusion**: BroadcastBlock is the best solution.

## Implementation Details

### Files Created

**Core Implementation:**
- `src/DataFlow/Blocks/Broadcast/BroadcastBlock.cs`
- `src/DataFlow/Blocks/Broadcast/BroadcastBlockOptions.cs`
- `src/DataFlow/Blocks/Broadcast/BroadcastTarget.cs`
- `src/DataFlow/Blocks/Broadcast/BroadcastTargetAdapter.cs`
- `src/DataFlow/Builder/Graph/StructuredBroadcastBlockBuilder.cs`

**Builder Integration:**
- Modified: `src/DataFlow/Builder/Graph/StructuredDataFlowBuilder.cs` (broadcast target connection logic)
- Modified: `src/DataFlow/Builder/Graph/StructuredDataFlowBuilderExtensions.cs` (AddBroadcast extension)

**Tests:**
- `src/Tests/DataFlow/BroadcastBlockTests.cs` (3 unit tests, all passing)
- `src/Tests/DataFlow/Examples/ImportDependencyExampleTests.cs` (3 real-world examples, all passing)

**Documentation:**
- `docs/import-dependency-patterns.md` (design analysis and comparison)
- `docs/broadcast-block-usage.md` (practical usage guide)
- `docs/IMPLEMENTATION_SUMMARY.md` (this file)

### Test Coverage

**Unit Tests (BroadcastBlockTests.cs):**
1. ✅ `BroadcastBlock_Should_Fanout_Items_To_Multiple_Targets` - Verifies basic broadcast
2. ✅ `BroadcastBlock_Should_Support_Transformations_Per_Target` - Tests type transformations
3. ✅ `BroadcastBlock_Should_Process_Targets_Concurrently` - Validates concurrent processing

**Example Tests (ImportDependencyExampleTests.cs):**
1. ✅ `Example1_Simple_Concurrent_Processing` - Concurrent validation/archival/notification
2. ✅ `Example2_Sequential_Then_Concurrent_With_Dependency` - Full import dependency scenario
3. ✅ `Example3_Complex_Import_With_Reconciliation` - Multi-stage reconciliation flow

All 6 tests pass successfully.

## Key Benefits

### 1. Solves the Original Problem

The implementation directly addresses the issue requirements:
- ✅ Sequential dependencies (currencies first)
- ✅ Concurrent downstream processing (entities, GL accounts, bank accounts)
- ✅ Data routing to multiple destinations

### 2. Clean Architecture

- Composable with existing blocks
- Follows DataFlow patterns
- Type-safe API
- Natural backpressure management

### 3. Reusable

The BroadcastBlock can be used for many scenarios beyond financial imports:
- Multi-stage validation
- Parallel archival and processing
- Fan-out for analytics and audit trails
- Real-time and batch processing split

### 4. Well-Documented

- Comprehensive design analysis
- Practical usage guide
- Working examples
- Best practices and patterns

## Migration Path

Existing flows continue to work unchanged. BroadcastBlock is opt-in:

1. No breaking changes to existing APIs
2. Composable with existing blocks (Router, Transform, etc.)
3. Gradual adoption - use where needed
4. Compatible with structured builder (as requested)

## Performance Characteristics

### Backpressure Strategy

- **"Slowest Wins"**: All targets must accept item before proceeding
- Ensures no target gets overwhelmed
- Slowest target determines throughput

### Memory Usage

- Each target has bounded channel (capacity configurable)
- Memory = `num_targets × capacity × item_size`
- Default capacity: 100 items per target

### Concurrency

- Targets process concurrently
- Broadcast writes to all targets using `Task.WhenAll`
- Each target can have its own `MaxConcurrency`

## Future Enhancements

Potential improvements (not in scope for this implementation):

1. **Selective Broadcasting**: Predicate-based target selection
2. **Dynamic Targets**: Add/remove targets at runtime
3. **Priority Targets**: Some targets get items before others
4. **Multicast Groups**: Logical grouping of related targets
5. **Metrics**: Per-target throughput and backpressure metrics

## Conclusion

The BroadcastBlock implementation successfully addresses the import dependency and concurrent routing requirements. It provides a clean, composable, and type-safe solution that:

- Handles sequential dependencies naturally
- Enables concurrent processing with proper backpressure
- Integrates seamlessly with the structured builder
- Follows DataFlow architectural patterns
- Is well-tested and documented

The solution is production-ready and provides a solid foundation for complex data import scenarios involving dependencies and concurrent processing.

## References

- **Issue**: Investigate Design Patterns for Import Dependency and Concurrent Routing in DataFlow
- **Design Document**: `docs/import-dependency-patterns.md`
- **Usage Guide**: `docs/broadcast-block-usage.md`
- **Tests**: `src/Tests/DataFlow/BroadcastBlockTests.cs`
- **Examples**: `src/Tests/DataFlow/Examples/ImportDependencyExampleTests.cs`
