# Migration Guide: TransformerBlock/ProcessorBlock to ActorBlock

## Overview

`TransformerBlock<TIn, TOut>`, `SimpleTransformerBlock<TIn, TOut>`, and `ProcessorBlock<T>` are being deprecated in favor of `ActorBlock<TIn, TOut, TActor>`. This consolidation provides **DI scope safety by default**, preventing common bugs from concurrent dependency sharing.

## Why Consolidate?

### Safety Benefits

**Problem**: TransformerBlock and ProcessorBlock allow concurrent processing without DI scope isolation, which can lead to:
- Shared scoped dependencies (e.g., `DbContext`) used across threads
- State accumulation in long-running streams (memory leaks)
- Subtle concurrency bugs that only appear under load

**Solution**: ActorBlock enforces DI scope isolation:
- Each concurrent actor runs in its own DI scope
- Scoped dependencies (DbContext, caches, etc.) are properly isolated
- Optional scope rotation prevents memory accumulation
- Safety by default, not opt-in

### Unified Mental Model

- Single pattern for all stream processing operations
- Consistent approach to concurrency and lifecycle management
- Less cognitive load (fewer block types to choose from)
- First-class support for dependency injection

## Performance

ActorBlock achieves **<1% overhead** after warmup compared to plain blocks (see `/poc/docs/benchmarks/actor-block-performance-validation.md`). With proper warmup and optimization, the actor pattern has near-zero performance cost while providing significant safety benefits.

## Migration Patterns

### 1. SimpleTransformerBlock → ActorBlock (1-to-1 Transformation)

**Before:**
```csharp
var transformer = new SimpleTransformerBlock<int, string>(
    "transformer", 
    x => $"Item-{x}");
```

**After:**
```csharp
// Define a simple actor
public class SimpleTransformActor : IStreamActor<int, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return $"Item-{item}";
        }
    }
}

// Register in DI
services.AddScoped<SimpleTransformActor>();

// Create block
var transformer = new ActorBlock<int, string, SimpleTransformActor>(
    "transformer",
    serviceScopeFactory);
```

**Or use a helper method** (see "Helper Factory Methods" section below):
```csharp
var transformer = ActorBlockHelpers.CreateTransformer<int, string>(
    "transformer",
    x => $"Item-{x}",
    serviceScopeFactory);
```

### 2. TransformerBlock → ActorBlock (1-to-Many)

**Before:**
```csharp
var transformer = new TransformerBlock<int, int>(
    "transformer",
    async (x, ctx) =>
    {
        async IAsyncEnumerable<int> Generate()
        {
            yield return x;
            yield return x * 2;
            yield return x * 3;
        }
        return Generate();
    });
```

**After:**
```csharp
public class ExpandActor : IStreamActor<int, int>
{
    public async IAsyncEnumerable<int> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return item;
            yield return item * 2;
            yield return item * 3;
        }
    }
}

services.AddScoped<ExpandActor>();

var transformer = new ActorBlock<int, int, ExpandActor>(
    "transformer",
    serviceScopeFactory);
```

### 3. TransformerBlock → ActorBlock (Filtering)

**Before:**
```csharp
var transformer = new TransformerBlock<int, int>(
    "transformer",
    async (x, ctx) =>
    {
        async IAsyncEnumerable<int> Filter()
        {
            if (x % 2 == 0)
            {
                yield return x;
            }
            await Task.CompletedTask;
        }
        return Filter();
    });
```

**After:**
```csharp
public class FilterActor : IStreamActor<int, int>
{
    public async IAsyncEnumerable<int> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            if (item % 2 == 0)
            {
                yield return item;
            }
        }
        await Task.CompletedTask;
    }
}

services.AddScoped<FilterActor>();

var transformer = new ActorBlock<int, int, FilterActor>(
    "transformer",
    serviceScopeFactory);
```

### 4. ProcessorBlock → ActorBlock

**Before:**
```csharp
var processor = new ProcessorBlock<int>(
    "processor",
    async (item, ctx) =>
    {
        Console.WriteLine($"Processing: {item}");
        await Task.CompletedTask;
    });
```

**After:**
```csharp
public class LoggingActor : IStreamActor<int, object>
{
    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            Console.WriteLine($"Processing: {item}");
        }
        
        // Terminal actor - no output
        yield break;
    }
}

services.AddScoped<LoggingActor>();

var processor = new ActorBlock<int, object, LoggingActor>(
    "processor",
    serviceScopeFactory);
```

### 5. With Dependency Injection

One of the key benefits - actors can inject dependencies:

**Before (no DI support):**
```csharp
var dbContext = serviceProvider.GetRequiredService<MyDbContext>();
var processor = new ProcessorBlock<Order>(
    "processor",
    async (order, ctx) =>
    {
        // ⚠️ DANGER: DbContext shared across concurrent operations!
        await dbContext.Orders.AddAsync(order);
        await dbContext.SaveChangesAsync();
    });
```

**After (DI scope isolation):**
```csharp
public class OrderSaveActor : IStreamActor<Order, object>
{
    private readonly MyDbContext _dbContext;
    
    // SAFE: Each actor gets its own scoped DbContext
    public OrderSaveActor(MyDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            await _dbContext.Orders.AddAsync(order);
            await _dbContext.SaveChangesAsync();
        }
        
        yield break;
    }
}

services.AddScoped<MyDbContext>();
services.AddScoped<OrderSaveActor>();

var processor = new ActorBlock<Order, object, OrderSaveActor>(
    "processor",
    serviceScopeFactory);
```

### 6. With Scope Rotation (Memory Management)

For long-running streams that accumulate state:

```csharp
public class CachingActor : IStreamActor<int, int>
{
    private readonly IMemoryCache _cache;
    private int _processedCount;
    
    public CachingActor(IMemoryCache cache)
    {
        _cache = cache;
    }
    
    public async IAsyncEnumerable<int> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            _processedCount++;
            
            // Use cache...
            var result = ProcessWithCache(item);
            yield return result;
            
            // Request rotation every 1000 items to clean up cache
            if (_processedCount >= 1000)
            {
                context.RequestRotation();
                yield break;  // Actor will be recreated with fresh DI scope
            }
        }
    }
}
```

## Helper Factory Methods

To simplify common migration patterns, you can create helper factory methods:

```csharp
public static class ActorBlockHelpers
{
    /// <summary>
    /// Create ActorBlock for simple 1-to-1 transformation
    /// </summary>
    public static ActorBlock<TIn, TOut, SimpleFuncActor<TIn, TOut>> 
        CreateTransformer<TIn, TOut>(
            string name,
            Func<TIn, TOut> transform,
            IServiceScopeFactory scopeFactory)
    {
        // Register actor with transform function in DI
        // (Implementation details omitted for brevity)
        return new ActorBlock<TIn, TOut, SimpleFuncActor<TIn, TOut>>(
            name, scopeFactory);
    }
    
    /// <summary>
    /// Create ActorBlock for simple processing (terminal block)
    /// </summary>
    public static ActorBlock<T, object, SimpleProcActor<T>>
        CreateProcessor<T>(
            string name,
            Func<T, Task> processor,
            IServiceScopeFactory scopeFactory)
    {
        // Register actor with processor function in DI
        // (Implementation details omitted for brevity)
        return new ActorBlock<T, object, SimpleProcActor<T>>(
            name, scopeFactory);
    }
}
```

## Migration Checklist

When migrating a block:

- [ ] Identify the block type (Transformer/Processor)
- [ ] Extract the transformation/processing logic
- [ ] Create an actor class implementing `IStreamActor<TIn, TOut>`
- [ ] Inject any dependencies via constructor
- [ ] Register actor as scoped service in DI
- [ ] Replace block instantiation with `ActorBlock`
- [ ] Consider if scope rotation is needed
- [ ] Update tests to verify DI scope isolation
- [ ] Remove obsolete block usage

## Testing Considerations

When testing ActorBlock:

1. **DI Setup Required**: Tests must set up a service collection and scope factory:
   ```csharp
   var services = new ServiceCollection()
       .AddScoped<MyActor>()
       .BuildServiceProvider();
   
   var block = new ActorBlock<int, string, MyActor>(
       "test-block",
       services.GetRequiredService<IServiceScopeFactory>());
   ```

2. **Verify Scope Isolation**: Test that concurrent operations don't share scoped dependencies

3. **Test Rotation**: If using rotation, verify it works as expected

## Common Pitfalls

### 1. Forgetting to Register Actor in DI

**Error**: `System.InvalidOperationException: Unable to resolve service for type 'MyActor'`

**Fix**: Add `services.AddScoped<MyActor>()` to your DI registration.

### 2. Using Singleton Actor with Mutable State

**Problem**: Actors with mutable state should be scoped, not singleton.

**Fix**: Always register actors as `AddScoped<TActor>()`, not `AddSingleton<TActor>()`.

### 3. Not Handling Cancellation

**Problem**: Actor doesn't respect cancellation token.

**Fix**: Use `.WithCancellation(context.CancellationToken)` when iterating input:
```csharp
await foreach (var item in input.WithCancellation(context.CancellationToken))
{
    // ...
}
```

## Performance Tips

1. **Warmup**: Run 1000+ items through the pipeline before measuring performance
2. **Batch Operations**: For high-throughput scenarios, consider batching before ActorBlock
3. **Avoid Unnecessary Rotation**: Only rotate when needed (memory accumulation)
4. **Profile**: Use dotnet-counters or profiler to identify bottlenecks

## Further Reading

- [ActorBlock Guide](/poc/ACTOR_BLOCK.md) - Comprehensive ActorBlock documentation
- [Performance Validation](/poc/docs/benchmarks/actor-block-performance-validation.md) - ActorBlock vs plain blocks performance comparison
- [POC Glossary](/poc/docs/POC_GLOSSARY.md) - Terminology reference

## Questions?

If you encounter migration issues not covered in this guide, please:
1. Check existing tests for migration examples
2. Review the ActorBlock guide and glossary
3. Open an issue with specific migration scenario
