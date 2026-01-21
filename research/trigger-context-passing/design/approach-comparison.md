# Approach Comparison: Trigger Context Passing

## Date: 2026-01-20

## Approach 1: Extend IExecutionContext with TriggerContext

### Design

```csharp
public interface IExecutionContext
{
    CancellationToken CancellationToken { get; }
    IServiceProvider ServiceProvider { get; }
    Guid InvocationId { get; }
    ICheckpoint? RecoveryCheckpoint { get; }
    IDataFlowMetrics? Metrics { get; }
    
    // NEW: Generic trigger context storage
    ITriggerContext? TriggerContext { get; }
}

public interface ITriggerContext
{
    // Marker interface - implementations provide typed data
}

// Example implementations
public class WebRequestTriggerContext : ITriggerContext
{
    public string RequestId { get; init; }
    public Dictionary<string, string> Headers { get; init; }
}

public class MessageQueueTriggerContext : ITriggerContext
{
    public string MessageId { get; init; }
    public string QueueName { get; init; }
    public int DeliveryCount { get; init; }
}

public class ScheduledTriggerContext : ITriggerContext
{
    public string JobName { get; init; }
    public string TenantId { get; init; }
    public DateTime ScheduledTime { get; init; }
}
```

### Usage

```csharp
// At trigger point
var triggerContext = new ScheduledTriggerContext 
{
    JobName = "DailyReport",
    TenantId = "tenant-123",
    ScheduledTime = DateTime.UtcNow
};

var context = new ExecutionContext(
    serviceProvider,
    cancellationToken,
    invocationId,
    recoveryCheckpoint: null,
    metrics: null,
    triggerContext: triggerContext  // NEW parameter
);

await graph.ExecuteAsync(context);

// In actors
public class MyActor : IStreamActor<int, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        // ❌ Problem: IActorExecutionContext doesn't have TriggerContext!
        // Need to propagate to IActorExecutionContext too
        
        await foreach (var item in input)
        {
            yield return $"Item {item}";
        }
    }
}
```

### Modified Design - Propagate to IActorExecutionContext

```csharp
public interface IActorExecutionContext
{
    CancellationToken CancellationToken { get; }
    Guid InvocationId { get; }
    void RequestRotation();
    IEpochCoordinator? EpochCoordinator { get; }
    
    // NEW: Access to trigger context
    ITriggerContext? TriggerContext { get; }
}

// Usage in actor
public class TenantAwareActor : IStreamActor<int, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        var scheduledContext = context.TriggerContext as ScheduledTriggerContext;
        var tenantId = scheduledContext?.TenantId;
        
        await foreach (var item in input)
        {
            yield return $"Tenant {tenantId}: Item {item}";
        }
    }
}
```

### Pros
✅ **Global invariant** - Available throughout execution without threading through stream
✅ **Type-safe** - Can create strongly-typed trigger context classes
✅ **Works with all block types** - Including EpochSourceBlocks
✅ **No stream pollution** - Trigger context separate from data stream
✅ **Explicit in API** - Clear that context is available
✅ **No AsyncLocal needed** - Passed explicitly through context parameter

### Cons
❌ **Breaking change** - Adds new property to IExecutionContext and IActorExecutionContext
❌ **Casting required** - Actors must cast ITriggerContext to specific type
❌ **Not truly type-safe** - Cast can fail at runtime
❌ **Propagation burden** - Must update both IExecutionContext and IActorExecutionContext

## Approach 2: Generic IExecutionContext<TTrigger>

### Design

```csharp
// Base interface (backward compatible)
public interface IExecutionContext
{
    CancellationToken CancellationToken { get; }
    IServiceProvider ServiceProvider { get; }
    Guid InvocationId { get; }
    ICheckpoint? RecoveryCheckpoint { get; }
    IDataFlowMetrics? Metrics { get; }
}

// Generic variant
public interface IExecutionContext<TTrigger> : IExecutionContext
{
    TTrigger TriggerContext { get; }
}

// Strongly-typed actor context
public interface IActorExecutionContext<TTrigger> : IActorExecutionContext
{
    TTrigger TriggerContext { get; }
}
```

### Usage

```csharp
// At trigger point
var triggerContext = new ScheduledTriggerContext 
{
    TenantId = "tenant-123"
};

var context = new ExecutionContext<ScheduledTriggerContext>(
    serviceProvider,
    cancellationToken,
    invocationId,
    triggerContext
);

await graph.ExecuteAsync(context);

// Actor with strongly-typed context
public class TenantAwareActor : IStreamActor<int, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        // ❌ Problem: RunAsync signature takes IActorExecutionContext, not IActorExecutionContext<T>
        // Would require changing actor interface to be generic
    }
}
```

### Pros
✅ **Type-safe** - Compile-time type checking for trigger context
✅ **Global invariant** - Available throughout execution
✅ **Backward compatible** - Non-generic interface still exists

### Cons
❌ **Complex** - Requires generic interfaces throughout
❌ **Actor interface changes** - Would need `IStreamActor<TIn, TOut, TTrigger>`
❌ **Graph type pollution** - Graph would need to know trigger type
❌ **Implementation complexity** - Major refactoring needed

## Approach 3: Dependency Injection (ServiceProvider)

### Design

```csharp
// Register trigger context in DI container
services.AddScoped<ScheduledTriggerContext>(sp => new ScheduledTriggerContext 
{
    TenantId = "tenant-123"
});

var context = new ExecutionContext(serviceProvider, cancellationToken);
await graph.ExecuteAsync(context);

// Actor accesses via DI
public class TenantAwareActor : IStreamActor<int, string>
{
    private readonly ScheduledTriggerContext _triggerContext;
    
    public TenantAwareActor(ScheduledTriggerContext triggerContext)
    {
        _triggerContext = triggerContext;
    }
    
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input)
        {
            yield return $"Tenant {_triggerContext.TenantId}: Item {item}";
        }
    }
}
```

### Pros
✅ **Type-safe** - Compile-time type checking via DI
✅ **No interface changes** - Uses existing IServiceProvider
✅ **Standard pattern** - Familiar DI-based approach
✅ **Scoped** - Can use scoped lifetime per execution

### Cons
❌ **Indirect** - Not obvious trigger context is available
❌ **Lifetime management** - Must ensure correct scope creation
❌ **Testing complexity** - Must mock DI container in tests
❌ **Actor limitation** - Actors constructed once, may not work with rotation

## Approach 4: AsyncLocal Storage

### Design

```csharp
public static class TriggerContextAccessor
{
    private static readonly AsyncLocal<ITriggerContext?> _current = new();
    
    public static ITriggerContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}

// Usage at trigger point
TriggerContextAccessor.Current = new ScheduledTriggerContext 
{
    TenantId = "tenant-123"
};

try
{
    await graph.ExecuteAsync(context);
}
finally
{
    TriggerContextAccessor.Current = null;
}

// Actor accesses ambient context
public class TenantAwareActor : IStreamActor<int, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        var triggerContext = TriggerContextAccessor.Current as ScheduledTriggerContext;
        var tenantId = triggerContext?.TenantId;
        
        await foreach (var item in input)
        {
            yield return $"Tenant {tenantId}: Item {item}";
        }
    }
}
```

### Pros
✅ **No interface changes** - Uses ambient context pattern
✅ **Works everywhere** - Available to any code in async flow
✅ **Simple API** - Static accessor, no DI needed

### Cons
❌ **Reliability concerns** - AsyncLocal may not propagate across Task.Run()
❌ **Performance** - AsyncLocal has overhead
❌ **Testing complexity** - Global state complicates testing
❌ **Implicit** - Not obvious context is available
❌ **Error-prone** - Easy to forget to set/clear context
❌ **Already a concern** - Code comments already note AsyncLocal unreliability

## Approach 5: Trigger as First Stream Item

### Design

```csharp
// Trigger context becomes part of the data stream
public record TriggerMessage<TContext, TData>
{
    public TContext TriggerContext { get; init; }
    public IAsyncEnumerable<TData> DataStream { get; init; }
}

// Usage
var trigger = new TriggerMessage<ScheduledTriggerContext, int>
{
    TriggerContext = new ScheduledTriggerContext { TenantId = "tenant-123" },
    DataStream = GetDataStream()
};

// Source actor produces trigger message
public class TriggerSourceActor : ISourceActor<TriggerMessage<ScheduledTriggerContext, int>>
{
    // ...
}
```

### Pros
✅ **Type-safe** - Trigger context flows with data
✅ **Explicit data flow** - Clear what flows where

### Cons
❌ **Doesn't work with EpochSourceBlocks** - Source blocks don't have input
❌ **Stream pollution** - Trigger context mixed with data
❌ **Must copy to all items** - Every transformed item needs context
❌ **Complex** - Significantly complicates stream processing
❌ **Not a global invariant** - Would need to thread through every transform

## Recommendation

Based on analysis, **Approach 1 (Extend IExecutionContext)** is the best option:

1. ✅ Works with all block types (including sources)
2. ✅ Global invariant - available everywhere
3. ✅ No AsyncLocal reliability issues
4. ✅ Relatively simple implementation
5. ✅ Clear API - explicitly passed in context

**Trade-offs accepted**:
- Breaking change (but we're in POC, acceptable)
- Need to cast (but can provide helper methods)
- Need to propagate to IActorExecutionContext (one-time effort)

**Next steps**:
1. Create prototype implementing Approach 1
2. Validate it works for all 3 trigger scenarios
3. Measure performance overhead
4. Document final recommendation
