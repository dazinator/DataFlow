# Exploratory Thought: Should Epoch Blocks Be Resolved from Epoch DI Scope?

**Status**: Exploratory Analysis  
**Last Updated**: 2025-11-13

---

## Question

The original issue posed this question:

> "Do epoch blocks live longer than a single epoch - if not perhaps we should be resolving them from this new epoch DI scope - not sure if this idea has merit - might be able to dismiss the merit, or confirm the merit but potentially defer any work with only findings discussed"

This document explores this idea.

---

## What Are "Epoch Blocks"?

First, let's clarify terminology:

### Block Instances vs Block Execution

**Block Instance**: The object that implements `IBlock`, created once and reused
```csharp
// Created once during graph construction
var block = new OrderProcessingBlock(logger, config);
```

**Block Execution**: When a block processes an item/epoch
```csharp
// Called many times for different epochs
await block.ProcessAsync(item);
```

### Current Lifetime Model

**Blocks are Singleton-like**:
- Created once during graph construction
- Live for the entire graph lifetime
- Process many epochs sequentially or concurrently

**Services are Variable**:
- Singleton: `ILogger`, configuration
- Scoped (per request): Not currently used
- Transient: Created per operation

---

## Scenario Analysis

### Scenario 1: Block Per Epoch (Proposed Idea)

**Concept**: Create a new block instance for each epoch

```csharp
public interface IEpochBlockFactory<TBlock>
{
    TBlock CreateForEpoch(IEpoch epoch);
}

// Graph creates new block per epoch
foreach (var epoch in epochs)
{
    var block = factory.CreateForEpoch(epoch);
    await block.ProcessAsync(epoch);
    await (block as IAsyncDisposable)?.DisposeAsync();
}
```

**Pros**:
✅ Natural scoping - block lifetime = epoch lifetime
✅ Block can have epoch-scoped dependencies in constructor
✅ Automatic cleanup when epoch completes
✅ Simpler state management - no state carries over between epochs

**Cons**:
❌ Performance overhead - create/dispose block per epoch
❌ Doesn't match current DataFlow architecture
❌ Breaks existing block implementations
❌ Not all blocks are epoch-aware
❌ Initialization cost per epoch

### Scenario 2: Singleton Blocks with Epoch-Scoped Services (Current Proposal)

**Concept**: Blocks created once, resolve epoch-scoped services as needed

```csharp
// Block created once
public class OrderProcessingBlock
{
    private readonly ILogger _logger; // Singleton, constructor-injected
    
    public async Task ProcessAsync(IEpoch epoch, Order order)
    {
        // Resolve epoch-scoped service on demand
        var dbContext = epoch.GetService<OrderDbContext>();
        
        // Process
    }
}
```

**Pros**:
✅ Matches current architecture
✅ No breaking changes
✅ Blocks can be stateless or have singleton state
✅ Better performance - block created once
✅ Flexible - blocks choose what to make epoch-scoped

**Cons**:
⚠️ Block must be careful about state
⚠️ Can't inject epoch-scoped services in constructor

---

## Use Cases

### Use Case 1: Stateless Processing Block

**Nature**: No state, pure transformation

```csharp
public class ValidationBlock
{
    public Task<bool> ValidateAsync(Order order)
    {
        // Stateless validation logic
        return Task.FromResult(order.IsValid);
    }
}
```

**Verdict**: 
- **Singleton block is better** - no need to create per epoch
- No epoch-scoped dependencies needed
- Pure function, reusable across all epochs

### Use Case 2: Block with Epoch-Scoped State

**Nature**: Accumulates state per epoch

```csharp
public class AggregatorBlock
{
    public async Task ProcessAsync(IEpoch epoch, Order order)
    {
        // Get epoch-scoped accumulator
        var accumulator = epoch.GetService<OrderAccumulator>();
        accumulator.Add(order);
    }
}
```

**Verdict**:
- **Singleton block with epoch-scoped service is better**
- Block itself is stateless
- State is in the epoch-scoped service
- Clear separation of concerns

### Use Case 3: Block with Expensive Initialization

**Nature**: Loads resources, caches, etc.

```csharp
public class EnrichmentBlock
{
    private readonly IProductCatalog _catalog; // Expensive to load
    
    public EnrichmentBlock(IProductCatalog catalog)
    {
        _catalog = catalog; // Load once, reuse
    }
    
    public async Task ProcessAsync(IEpoch epoch, Order order)
    {
        // Use cached catalog
        var product = _catalog.GetProduct(order.ProductId);
        order.Enrich(product);
    }
}
```

**Verdict**:
- **Singleton block is definitely better**
- Expensive resources loaded once
- Reused across all epochs
- Would be wasteful to recreate per epoch

### Use Case 4: Block Needs Epoch-Scoped Constructor Injection

**Hypothetical**: Block wants epoch-scoped service in constructor

```csharp
public class HypotheticalBlock
{
    private readonly OrderDbContext _dbContext; // Want epoch-scoped!
    
    public HypotheticalBlock(OrderDbContext dbContext)
    {
        _dbContext = dbContext; // But this would be wrong!
    }
}
```

**Problem**:
- If block is singleton, can't inject epoch-scoped service
- Epoch-scoped service would be captured from first epoch
- All subsequent epochs would use wrong instance

**Solution**:
- Don't inject epoch-scoped services in constructor
- Resolve them in the processing method

```csharp
public class CorrectBlock
{
    public async Task ProcessAsync(IEpoch epoch, Order order)
    {
        // Resolve epoch-scoped service here
        var dbContext = epoch.GetService<OrderDbContext>();
    }
}
```

**Verdict**:
- **This is a constraint, but an acceptable one**
- Forces correct usage pattern
- Clear that service is epoch-scoped, not block-scoped
- Prevents bugs from captured dependencies

---

## Analysis Summary

### The Core Question

**Should blocks be resolved from epoch DI scope?**

### Answer: **No, blocks should remain singleton-like**

### Reasoning

1. **Architectural Fit**
   - Current DataFlow architecture: blocks are long-lived
   - Changing this would be a fundamental redesign
   - Breaking change for all existing blocks

2. **Performance**
   - Blocks can be expensive to create
   - Creating per epoch adds overhead
   - Current model allows initialization once, reuse many times

3. **Flexibility**
   - Singleton blocks can have singleton dependencies (logger, config)
   - Singleton blocks can have transient dependencies (factories)
   - Singleton blocks can have epoch-scoped dependencies (via `epoch.GetService`)
   - Maximum flexibility

4. **State Management**
   - Block-level state can be singleton (caches, resources)
   - Epoch-level state can be epoch-scoped (DbContext, accumulator)
   - Clear separation, explicit intent

5. **Not All Blocks Are Epoch-Aware**
   - Some blocks don't care about epochs
   - Would be wasteful to recreate them per epoch

### The Pattern That Works

**Singleton Blocks + Epoch-Scoped Services**:
- Blocks created once (singleton-like lifetime)
- Block instances injected with singleton dependencies
- Epoch-scoped services resolved from `IEpoch` during processing
- Clear, flexible, performant

---

## Could There Be an Advanced Pattern?

### Hypothetical: Epoch-Aware Block Factory

What if we wanted to support BOTH patterns?

```csharp
// Pattern 1: Singleton block (default)
public class StatelessBlock : IBlock
{
    // Created once, reused
}

// Pattern 2: Epoch-scoped block (opt-in)
public class EpochScopedBlock : IEpochBlock
{
    // Created per epoch, disposed after
}
```

**Implementation**:
```csharp
public interface IEpochBlock : IBlock, IAsyncDisposable
{
    // Marker: This block should be created per epoch
}

public class DataFlowGraph
{
    private readonly IServiceProvider _rootProvider;
    
    public async Task ExecuteAsync()
    {
        foreach (var epoch in epochs)
        {
            // For regular blocks: use singleton instance
            if (block is not IEpochBlock)
            {
                await block.ProcessAsync(context, item);
            }
            // For epoch blocks: create from epoch scope
            else
            {
                var epochBlock = epoch.GetService<IEpochBlock>();
                await epochBlock.ProcessAsync(context, item);
                await epochBlock.DisposeAsync();
            }
        }
    }
}
```

### Evaluation

**Pros**:
✅ Supports both patterns
✅ Opt-in for advanced scenarios
✅ Backward compatible

**Cons**:
❌ Adds complexity
❌ Two mental models to understand
❌ Unclear when to use which
❌ Not clear what problem this solves

**Verdict**: **Nice to have, but not necessary**
- Can be added later if compelling use case emerges
- Current design (singleton blocks + epoch services) handles all known scenarios
- YAGNI principle - don't add until needed

---

## Recommendation

### For Current Design

**Keep blocks singleton-like**:
- Blocks created once during graph construction
- Blocks live for graph lifetime
- Blocks resolve epoch-scoped services from `IEpoch` during processing

**Rationale**:
1. Matches current architecture
2. Better performance
3. More flexible
4. Simpler mental model
5. Handles all known use cases

### For Future Consideration

**If a compelling use case emerges** for per-epoch block instances:
- Could add `IEpochBlock` marker interface
- Opt-in pattern, not breaking change
- Would require factory pattern implementation

**Likely scenarios**:
- Blocks with complex epoch-specific initialization
- Blocks that benefit from automatic disposal per epoch
- Blocks that want constructor injection of epoch-scoped services

**Current assessment**: No such use case identified yet

---

## Conclusion

**The original exploratory question has been answered**:

> "Do epoch blocks live longer than a single epoch?"

**Answer**: Yes, blocks (instances) live longer than single epochs. They are singleton-like and process multiple epochs.

**Should we resolve blocks from epoch DI scope?**

**Answer**: No, this is not recommended for the following reasons:
1. Blocks are long-lived (graph lifetime)
2. Creating blocks per epoch adds overhead
3. Current pattern (singleton blocks + epoch services) is more flexible
4. No compelling use case for per-epoch block instances

**The merit of the idea**:
- Has theoretical appeal for purity (block lifetime = epoch lifetime)
- Could simplify some scenarios (automatic disposal)
- But adds complexity and overhead without clear benefits

**Recommendation**: 
- **Proceed with current design** (singleton blocks + epoch-scoped services)
- **Defer** any per-epoch block creation patterns until a compelling use case emerges
- **Document** the current pattern clearly so developers understand the lifetime model

---

## Discussion Points for Review

1. Do reviewers agree with the assessment?
2. Are there any use cases where per-epoch block creation would be beneficial?
3. Should we add a note in the design doc about this decision?
4. Should we document the lifetime model clearly in block development guidelines?
