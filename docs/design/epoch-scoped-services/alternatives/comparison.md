# Alternative Approaches Comparison

**Status**: Draft  
**Last Updated**: 2025-11-13

---

## Overview

This document provides an in-depth comparison of different approaches to providing epoch-scoped services in the DataFlow library.

---

## Summary Table

| Approach | Explicitness | Testability | Concurrency | DI Integration | Fan-In Support | Complexity | Verdict |
|----------|--------------|-------------|-------------|----------------|----------------|------------|---------|
| **Manual Tracking** | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ | ❌ | ⭐ | 😰😰😰 | ❌ Too much duplication |
| **AsyncLocal Context** | ⭐ | ⭐⭐ | ⭐ | ⭐⭐ | ⭐⭐ | 😰😰 | ❌ Hidden dependencies |
| **Epoch Object with DI** | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | 😰 | ✅ **Recommended** |
| **Block-Level Scopes** | ⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ❌ | 😰 | ❌ Wrong granularity |
| **Hybrid Approach** | ⭐⭐ | ⭐⭐ | ⭐⭐ | ⭐⭐ | ⭐⭐ | 😰😰😰 | ❌ Too complex |
| **Stream-Coupled Scopes** | ⭐⭐ | ⭐⭐ | ⭐⭐ | ⭐⭐⭐ | ❌ | 😰😰 | ❌ Fails fan-in |

Legend:
- ⭐⭐⭐ = Excellent
- ⭐⭐ = Good
- ⭐ = Poor
- 😰 = Low complexity
- 😰😰 = Medium complexity
- 😰😰😰 = High complexity

---

## Approach 1: Manual Tracking (Current State)

### Description

Each block manually tracks epoch boundaries and manages resources.

### Code Example

```csharp
public sealed class WriteContextBlock
{
    private readonly DbContextOptions<DemoDbContext> _dbOptions;
    
    public async IAsyncEnumerable<IEpochStream<DataRecord>> ProcessAsync(
        IAsyncEnumerable<IEpochStream<DataRecord>> input,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var epochStream in input.WithCancellation(ct))
        {
            yield return CreateEpochStream(
                epochStream.Epoch,
                ProcessEpochItems(epochStream.Epoch, epochStream.Items, ct));
        }
    }

    private async IAsyncEnumerable<DataRecord> ProcessEpochItems(
        EpochVector epoch,
        IAsyncEnumerable<DataRecord> items,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // MANUAL: Create DbContext for this epoch
        await using var dbContext = new DemoDbContext(_dbOptions);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        var processedItems = new List<DataRecord>();

        await foreach (var item in items.WithCancellation(ct))
        {
            var trackedRecord = await dbContext.DataRecords.FindAsync(
                new object[] { item.Id }, ct);
            
            if (trackedRecord != null)
            {
                trackedRecord.Processed = true;
            }

            processedItems.Add(item);
        }

        // MANUAL: Commit and dispose
        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        foreach (var item in processedItems)
        {
            yield return item;
        }
    }
}
```

### Pros

✅ **Simple to Understand (per block)**
- Each block's epoch handling is self-contained
- No framework magic - clear what's happening

✅ **Full Control**
- Block controls exact lifetime of resources
- Can implement custom disposal logic

✅ **No Framework Dependency**
- Works without any epoch infrastructure
- Can be used in non-epoch scenarios

### Cons

❌ **High Duplication**
- Every block reimplements epoch tracking
- Same patterns repeated across codebase

❌ **Cannot Share Resources**
- Each block creates its own DbContext
- No way to share state across blocks in same epoch

❌ **Complex for Block Developers**
- Must understand epoch lifecycle
- Must handle subsume operations manually
- Easy to get wrong

❌ **No DI Integration**
- Cannot use DI container for epoch-scoped services
- Manual service construction and disposal

❌ **Maintenance Burden**
- Changes to epoch semantics require updating all blocks
- Risk of inconsistent implementations

### Real-World Impact

**Example**: EF Core Tracker Block
- Manually tracks DbContext instances across epochs
- Handles epoch vector subsume operations
- ~200 lines of complex tracking code
- Difficult to test and maintain

**Code Complexity**: 7/10 (high)  
**Maintainability**: 3/10 (low)  
**Developer Experience**: 4/10 (difficult)

---

## Approach 2: AsyncLocal Context

### Description

Use `AsyncLocal<T>` to provide ambient epoch context that flows through async operations.

### Code Example

```csharp
public static class EpochContext
{
    private static readonly AsyncLocal<IEpoch?> _current = new();
    
    public static IEpoch Current
    {
        get => _current.Value 
            ?? throw new InvalidOperationException("No active epoch context");
        set => _current.Value = value;
    }
    
    public static IEpoch? CurrentOrNull => _current.Value;
}

// Framework sets context
public async Task ExecuteBlockAsync(IBlock block, IEpoch epoch)
{
    EpochContext.Current = epoch;
    try
    {
        await block.ProcessAsync();
    }
    finally
    {
        EpochContext.Current = null;
    }
}

// Block uses ambient context
public class OrderProcessingBlock
{
    public async Task ProcessAsync()
    {
        // Ambient context - no parameter needed
        var dbContext = EpochContext.Current.GetService<OrderDbContext>();
        
        // Process order
    }
}
```

### Pros

✅ **Clean API**
- No need to pass epoch through every method
- Simple service resolution

✅ **Familiar Pattern**
- Similar to `HttpContext.Current` in ASP.NET
- Similar to `TransactionScope` ambient pattern

✅ **Less Parameter Passing**
- Methods don't need `IEpoch` parameters
- Cleaner method signatures

### Cons

❌ **Hidden Dependencies**
- Not clear from signature what block depends on
- Harder to test - must set up ambient context
- Violates dependency injection principles

❌ **Concurrency Concerns**
- `AsyncLocal` can be tricky with Task.Run, parallel operations
- May not flow correctly in all scenarios
- Potential for context loss in complex async flows

❌ **Performance Overhead**
- `AsyncLocal` has allocation and lookup costs
- ~2-5μs per access (not huge, but measurable)

❌ **Debugging Difficulty**
- Stack traces don't show where context is set
- Hard to track down context corruption issues

❌ **Still Need Infrastructure**
- Still need epoch manager and lifecycle tracking
- Just hides it instead of solving it

### Example Issues

**Issue 1: Lost Context in Task.Run**
```csharp
public async Task ProcessAsync()
{
    var epoch = EpochContext.Current; // Works
    
    await Task.Run(() =>
    {
        var epoch2 = EpochContext.Current; // May be null!
    });
}
```

**Issue 2: Testing Complexity**
```csharp
[Fact]
public async Task Test_ProcessOrder()
{
    // Must set up ambient context
    var epoch = CreateTestEpoch();
    EpochContext.Current = epoch;
    
    try
    {
        await block.ProcessAsync();
    }
    finally
    {
        EpochContext.Current = null; // Must clean up
    }
}
```

**Code Complexity**: 5/10 (medium)  
**Maintainability**: 5/10 (medium)  
**Developer Experience**: 6/10 (simple API, but hidden deps)

---

## Approach 3: Epoch Object with DI Scope (Recommended)

### Description

Explicit epoch object passed through context, with integrated DI scope.

### Code Example

```csharp
// Interface
public interface IEpoch : IAsyncDisposable
{
    EpochVector Vector { get; }
    T GetService<T>() where T : notnull;
    IServiceProvider ServiceProvider { get; }
}

// Block receives epoch via context
public class OrderProcessingBlock : IEpochCompatibleBlock
{
    private readonly IBlockContext _context;
    
    public async Task ProcessAsync(Order order)
    {
        // Explicit dependency
        var epoch = _context.CurrentEpoch 
            ?? throw new InvalidOperationException("Epoch not available");
        
        var dbContext = epoch.GetService<OrderDbContext>();
        
        // Process order
    }
}
```

### Pros

✅ **Explicit Dependencies**
- Clear from code what block needs
- Type-safe access to epoch
- Testable - can inject mock epoch

✅ **Good Concurrency Support**
- No ambient state to corrupt
- Each task has clear epoch reference
- Works correctly with parallel operations

✅ **Natural DI Integration**
- Standard `IServiceProvider` semantics
- Works with existing DI patterns
- Can use `AddScoped` for registration

✅ **Framework Manages Complexity**
- Blocks use simple API
- Library handles lifecycle, subsume, reference counting
- Clear separation of concerns

✅ **Testable**
- Easy to create test epochs
- Can mock `IEpoch` interface
- No ambient state to set up/tear down

### Cons

⚠️ **Requires Framework Infrastructure**
- Need `EpochManager`, lifecycle coordination
- More components to implement and maintain

⚠️ **Slightly More Verbose**
- Must access `_context.CurrentEpoch`
- One extra line vs ambient context

⚠️ **Learning Curve**
- Developers must understand epoch concept
- Need to know when to use epoch-scoped vs other lifetimes

### Comparison: Before/After

**Before (Manual)**:
```csharp
await using var dbContext = new DemoDbContext(_dbOptions);
await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
// ... process
await dbContext.SaveChangesAsync(ct);
await transaction.CommitAsync(ct);
```

**After (Epoch Object)**:
```csharp
var epoch = _context.CurrentEpoch;
var dbContext = epoch.GetService<DemoDbContext>();
// ... process
// Automatic commit and disposal at epoch completion
```

**Code Complexity**: 4/10 (low-medium)  
**Maintainability**: 8/10 (high)  
**Developer Experience**: 8/10 (good)

---

## Approach 4: Block-Level DI Scopes

### Description

Each block gets its own DI scope, managed at the block level rather than epoch level.

### Code Example

```csharp
public class DataFlowGraph
{
    public async Task ExecuteBlockAsync(IBlock block, DataRecord item)
    {
        // Create scope per block (or per item?)
        await using var scope = _serviceProvider.CreateScope();
        
        var context = new BlockContext(scope.ServiceProvider);
        await block.ProcessAsync(context, item);
    }
}

public class OrderProcessingBlock
{
    public async Task ProcessAsync(IBlockContext context, Order order)
    {
        // Service scoped to block (or item?)
        var service = context.ServiceProvider.GetService<OrderService>();
        await service.ProcessAsync(order);
    }
}
```

### Pros

✅ **Standard DI Semantics**
- Uses existing `IServiceScope` pattern
- No custom scope management needed

✅ **Simple Implementation**
- Framework just creates/disposes scopes
- No epoch tracking complexity

✅ **Good Testability**
- Can create scopes in tests
- Standard DI testing patterns work

### Cons

❌ **Wrong Granularity**
- Services scoped per block, not per epoch
- Blocks can't share state for same epoch

❌ **Doesn't Solve Core Problem**
- Still can't have shared epoch-scoped services
- Multiple blocks processing same epoch get different instances

❌ **Unclear Lifetime**
- When is scope created/disposed?
- Per item? Per batch? Per epoch?

❌ **No Epoch Awareness**
- Blocks don't know what epoch they're in
- Can't coordinate across epoch boundaries

### Example Problem

```csharp
// Block A processes epoch 1
var scopeA = CreateScope();
var dbA = scopeA.GetService<DbContext>(); // Instance 1

// Block B processes epoch 1 (same epoch!)
var scopeB = CreateScope();
var dbB = scopeB.GetService<DbContext>(); // Instance 2 (different!)

// Changes made in Block A are not visible in Block B
```

**Code Complexity**: 3/10 (low)  
**Maintainability**: 6/10 (medium)  
**Developer Experience**: 5/10 (doesn't solve the problem)

---

## Approach 5: Hybrid Approach

### Description

Combine multiple approaches - AsyncLocal for convenience, with explicit epoch object as fallback.

### Code Example

```csharp
public class DataFlowGraph
{
    public async Task ExecuteBlockAsync(IBlock block, IEpoch epoch)
    {
        // Set ambient context
        EpochContext.Current = epoch;
        
        // Also provide explicit context
        var context = new BlockContext(epoch);
        
        try
        {
            await block.ProcessAsync(context);
        }
        finally
        {
            EpochContext.Current = null;
        }
    }
}

public class OrderProcessingBlock
{
    public async Task ProcessAsync(IBlockContext context)
    {
        // Option 1: Use ambient context
        var db1 = EpochContext.Current.GetService<DbContext>();
        
        // Option 2: Use explicit context
        var db2 = context.CurrentEpoch.GetService<DbContext>();
        
        // Both work, but which to use?
    }
}
```

### Pros

✅ **Flexibility**
- Can use either pattern
- Ambient for convenience, explicit for clarity

✅ **Gradual Migration**
- Can migrate from one pattern to another over time

### Cons

❌ **Inconsistency**
- Two ways to do the same thing
- Team must decide which to use

❌ **Confusion**
- Developers unsure which pattern to follow
- Code reviews become about style, not logic

❌ **Double Maintenance**
- Must maintain both ambient and explicit patterns
- More code, more tests

❌ **Performance Cost**
- Paying for both AsyncLocal and explicit tracking

❌ **Complexity**
- Worst of both worlds

**Code Complexity**: 8/10 (high)  
**Maintainability**: 4/10 (low)  
**Developer Experience**: 3/10 (confusing)

---

## Decision Matrix

### Criteria Weights

| Criterion | Weight | Justification |
|-----------|--------|---------------|
| Explicitness | 20% | Important for maintainability |
| Testability | 20% | Critical for quality |
| Concurrency | 15% | Must work correctly |
| DI Integration | 15% | Want standard patterns |
| Complexity | 15% | Simpler is better |
| Developer Experience | 15% | Adoption and productivity |

### Weighted Scores

| Approach | Explicit | Test | Concur | DI | Complex | DevEx | **Total** |
|----------|----------|------|--------|-------|---------|-------|-----------|
| Manual | 15 | 15 | 10 | 0 | 5 | 6 | **51** |
| AsyncLocal | 5 | 10 | 5 | 10 | 10 | 9 | **49** |
| **Epoch Object** | **15** | **15** | **15** | **15** | **10** | **12** | **82** ⭐ |
| Block Scopes | 10 | 15 | 15 | 15 | 12 | 8 | **75** |
| Hybrid | 10 | 10 | 10 | 10 | 3 | 5 | **48** |

**Winner**: Epoch Object with DI Scope (82 points)

---

## Detailed Trade-Off Analysis

### Epoch Object vs AsyncLocal

**When Epoch Object is Better**:
- Testing: Explicit dependencies easier to mock
- Concurrency: No ambient state to corrupt
- Debugging: Clear data flow in stack traces
- Maintainability: Explicit dependencies self-document

**When AsyncLocal Could Be Better**:
- Deeply nested calls: No need to pass epoch through every layer
- Existing ambient pattern: Team already familiar with pattern

**Verdict**: Explicit epoch object wins - benefits outweigh convenience.

### Epoch Object vs Manual Tracking

**When Epoch Object is Better**:
- Shared services: Multiple blocks can share state
- DI integration: Standard registration patterns
- Maintenance: One implementation, not per block
- Complexity: Framework handles hard parts

**When Manual Could Be Better**:
- Simple scenarios: One block, one resource
- Full control: Custom disposal logic needed

**Verdict**: Epoch object wins - scales better, less duplication.

### Epoch Object vs Block Scopes

**When Epoch Object is Better**:
- Shared state: Blocks in same epoch share services
- Epoch awareness: Blocks know what epoch they're in
- Transaction coordination: Natural commit point

**When Block Scopes Could Be Better**:
- Isolation: Want blocks fully isolated from each other
- Simpler mental model: One scope per block

**Verdict**: Epoch object wins - solves the core problem (epoch-scoped sharing).

---

## Approach 6: Stream-Coupled DI Scopes

### Description

Couple DI scope directly to `IEpochStream` and propagate scope through pipeline with streams.

### Code Example

```csharp
// Enhanced IEpochStream with scope
public interface IEpochStream<out T> : IAsyncDisposable
{
    EpochVector Vector { get; }
    IAsyncEnumerable<T> Items { get; }
    IServiceProvider ServiceProvider { get; } // NEW
    T GetService<T>() where T : notnull;       // NEW
}

// Source creates stream with scope
public class SourceBlock
{
    private readonly IServiceProvider _rootProvider;
    
    public async IAsyncEnumerable<IEpochStream<T>> ProduceAsync()
    {
        var scope = _rootProvider.CreateScope(); // Eager creation
        var stream = new EpochStream<T>(
            vector: new EpochVector(...),
            items: ProduceItems(),
            scope: scope); // Scope is part of stream
        
        yield return stream;
    }
}

// Block propagates scope
public class TransformBlock
{
    public async IAsyncEnumerable<IEpochStream<TOut>> ProcessAsync(
        IAsyncEnumerable<IEpochStream<TIn>> input)
    {
        await foreach (var inputStream in input)
        {
            // Access services directly from stream
            var service = inputStream.GetService<MyService>();
            
            // Create output stream, PROPAGATING scope
            yield return new EpochStream<TOut>(
                vector: inputStream.Vector,
                items: TransformItems(inputStream.Items, service),
                scope: inputStream._scope); // PROPAGATE (same instance)
        }
    }
}
```

### Pros

✅ **Direct Coupling**
- Scope travels with stream (no indirection)
- Service resolution directly on stream
- Clear that scope belongs to this epoch stream

✅ **Eager Creation**
- Scope created at source (point of origin)
- No lazy lookup or dictionary access
- Scope exists from stream creation

✅ **Simpler for Linear Pipelines**
- Natural propagation pattern
- No centralized manager needed
- Explicit in block code

✅ **Simpler Block API**
- `stream.GetService<T>()` instead of `context.CurrentEpoch.GetService<T>()`
- No IBlockContext needed
- More direct

### Cons

❌ **No Clean Fan-In Solution** (Critical)
- When two epoch streams merge, their scopes must also merge
- Options: pick one scope (loses services), create new (breaks continuity), merge providers (not supported)
- No satisfactory solution without reverting to centralized management

❌ **Unclear Subsume Semantics**
- Scope is embedded in stream, tied to specific vector
- No clear way to extend scope lifetime to cover subsumed vector
- No notification mechanism for subsume operations

❌ **Breaks Service Sharing at Fan-In**
- Different scopes before merge point
- Cannot guarantee same service instances after merge
- Violates core requirement: "same epoch = same services"

❌ **Reference Counting Still Needed**
- With scope propagation, multiple streams share same scope
- Must track when all streams are disposed before disposing scope
- Complexity moved, not eliminated

❌ **Disposal Complexity**
- Who owns the scope for disposal?
- Last consumer? Shared ownership? Tracking wrapper?
- Not simpler than reference counting

### Fan-In Problem Example

**Scenario**:
```
Source A: {vector={A=1}, scope=scopeA}
Source B: {vector={B=1}, scope=scopeB}
BufferNode: Merges to {vector={A=1,B=1}, scope=???}
```

**Problem**: Two different epochs (different scopes) become one unified epoch. Which scope?

**Options Evaluated**:
1. Pick scopeA - loses scopeB services ❌
2. Pick scopeB - loses scopeA services ❌
3. Create new scope - loses both, fresh services ❌
4. Merge providers - not supported by DI, arbitrary resolution order ❌
5. Reintroduce manager - defeats purpose ❌

**Current Approach Solution**: EpochManager uses `NotifyEpochSubsumed()` - one scope "wins" and covers merged vector space. Clean, explicit, working.

**Stream-Coupled Approach**: No clean solution.

### Evaluation

**Feasibility**: ❌ Not viable as general solution  
**Complexity**: 6/10 (appears simple but hides complexity at fan-in)  
**Developer Experience**: 7/10 (good for linear, fails for complex)  
**Maintainability**: 4/10 (breaks down at fan-in)

**Verdict**: ❌ **Rejected** - Fan-in is critical requirement

**Detailed Analysis**: See `/research/epoch-scope-propagation/` for complete evaluation including:
- Detailed architecture analysis
- Comparison matrix across 10 dimensions
- Fan-in problem deep dive
- Use case testing

**Research Date**: 2025-11-14  
**Related Issue**: Design question from #415 implementation

---

## Conclusion

**Recommended Approach**: **Epoch Object with DI Scope**

**Rationale**:
1. **Explicitness**: Clear dependencies improve maintainability
2. **Correctness**: Works properly with concurrency
3. **DI Integration**: Leverages standard patterns
4. **Developer Experience**: Simple API, framework handles complexity
5. **Testability**: Easy to test with mock epochs
6. **Fan-In Support**: Handles all topologies correctly (critical requirement)
7. **Subsume Semantics**: Explicit support via NotifyEpochSubsumed

**Rejected Alternatives**:
- **Manual Tracking**: Too much duplication, no service sharing
- **AsyncLocal Context**: Hidden dependencies, concurrency concerns
- **Block-Level Scopes**: Wrong granularity, doesn't solve core problem
- **Hybrid Approach**: Inconsistent, confusing, high complexity
- **Stream-Coupled Scopes**: Fails fan-in requirement (critical)

**Next Steps**:
1. Implement core `IEpoch` and `EpochManager` infrastructure
2. Integrate with block execution context
3. Validate with comprehensive testing (including 6 fan-in scenarios)
4. Document patterns and best practices
5. Migrate existing examples to demonstrate benefits
