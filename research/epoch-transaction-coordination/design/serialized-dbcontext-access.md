# Design: Serialized DbContext Access for Epoch-Scoped Transactions

**Date**: 2025-11-15  
**Status**: ✅ Implemented  
**Related**: Issue #125 - Channel-backed serialized access pattern
**Implementation**: `/poc/DataFlow.POC/Core/SerializedServiceExecutor.cs`

---

## Overview

This design implements a pattern where multiple concurrent blocks can participate in a single epoch-scoped transaction through **serialized access** to a shared DbContext using a **channel-backed execution model**. While concurrent transactional operations are not viable (see ADR), we provide a mechanism for concurrent blocks to queue transactional work that executes serially.

---

## Design Questions Addressed

### 1. Generic Serialized Access Pattern

**Question**: Does the epoch provide access to a generic method which can provide serialised usage of a dependency/service from epoch DI scope based on a channel to reduce many writers to single reader callback access pattern?

**✅ Implemented Design**:

Added new methods to `IEpoch`:

```csharp
public interface IEpoch : IAsyncDisposable
{
    EpochVector Vector { get; }
    T GetService<T>() where T : notnull;
    IServiceProvider ServiceProvider { get; }
    
    /// <summary>
    /// Executes an operation with serialized access to an epoch-scoped service.
    /// Multiple concurrent callers will have their operations queued via a channel
    /// and executed sequentially by a single reader task.
    /// </summary>
    Task<TResult> ExecuteSerializedAsync<TService, TResult>(
        Func<TService, Task<TResult>> operation,
        CancellationToken cancellationToken = default) 
        where TService : notnull;
        
    /// <summary>
    /// Executes an operation with serialized access (void return).
    /// </summary>
    Task ExecuteSerializedAsync<TService>(
        Func<TService, Task> operation,
        CancellationToken cancellationToken = default) 
        where TService : notnull;
}
```

**Channel-Based Implementation**:

```csharp
internal sealed class SerializedServiceExecutor<TService> : IAsyncDisposable
{
    private readonly TService _service;
    private readonly Channel<OperationRequest> _channel;
    private readonly Task _readerTask;
    
    public SerializedServiceExecutor(TService service)
    {
        _service = service;
        
        // Unbounded channel: multi-writer, single-reader
        _channel = Channel.CreateUnbounded<OperationRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        
        // Start the reader task that processes operations sequentially
        _readerTask = Task.Run(() => ProcessOperationsAsync());
    }
    
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<TService, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<TResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new OperationRequest<TResult>(operation, tcs, cancellationToken);
        
        // Write to channel (non-blocking for unbounded channel)
        _channel.Writer.TryWrite(request);
        
        // Wait for the reader task to process this operation
        return await tcs.Task;
    }
    
    private async Task ProcessOperationsAsync()
    {
        await foreach (var request in _channel.Reader.ReadAllAsync())
        {
            await request.ExecuteAsync(_service);
        }
    }
}
```

---

### 2. EF Core Example with Multiple Blocks

**✅ Implemented Pattern**:

```csharp
// Block 1: Add records to transaction
public class OrderLineProcessorBlock : IProcessor<OrderLine>
{
    public async Task ProcessAsync(
        IEpochStream<OrderLine> input,
        CancellationToken cancellationToken)
    {
        await foreach (var line in input.Items)
        {
            // Use serialized access to shared DbContext
            await input.EpochScope!.ExecuteSerializedAsync<DemoDbContext>(async db =>
            {
                db.OrderLines.Add(new OrderLineEntity
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity
                });
            }, cancellationToken);
        }
    }
}

// Block 2: Update order totals in same transaction
public class OrderTotalCalculatorBlock : IProcessor<Order>
{
    public async Task ProcessAsync(
        IEpochStream<Order> input,
        CancellationToken cancellationToken)
    {
        await foreach (var order in input.Items)
        {
            // Both blocks share same DbContext via serialized channel
            var total = await input.EpochScope!.ExecuteSerializedAsync<DemoDbContext, decimal>(
                async db =>
                {
                    var lines = await db.OrderLines
                        .Where(ol => ol.OrderId == order.Id)
                        .ToListAsync(cancellationToken);
                    
                    return lines.Sum(ol => ol.UnitPrice * ol.Quantity);
                }, cancellationToken);
            
            await input.EpochScope!.ExecuteSerializedAsync<DemoDbContext>(async db =>
            {
                var orderEntity = await db.Orders.FindAsync(order.Id);
                orderEntity.Total = total;
            }, cancellationToken);
        }
    }
}
```

**Key Benefits**:
- ✅ Multiple blocks can participate in same epoch transaction
- ✅ Operations are serialized via channel (thread-safe)
- ✅ Single reader task processes operations sequentially
- ✅ Clean async/await semantics via TaskCompletionSource
- ✅ Proper error propagation and cancellation support

---

### 3. Reader Task and Execution Model Integration

**Question**: How does the reader task work within the existing execution model which is graph node based?

**✅ Implementation**:

The reader task is **owned by the epoch** and managed through its lifecycle:

1. **Creation**: Reader task starts when first `ExecuteSerializedAsync` call occurs
2. **Execution**: Runs as background task, processes channel operations
3. **Lifecycle**: Tied to epoch disposal - waits for all operations to complete
4. **Cleanup**: Disposed when epoch is disposed

```csharp
internal sealed class Epoch : IEpoch
{
    private readonly ConcurrentDictionary<Type, IAsyncDisposable> _serializedExecutors;
    
    private SerializedServiceExecutor<TService> GetOrCreateExecutor<TService>()
    {
        return _serializedExecutors.GetOrAdd(typeof(TService), _ =>
        {
            var service = GetService<TService>();
            return new SerializedServiceExecutor<TService>(service);
        });
    }
    
    public async ValueTask DisposeAsync()
    {
        // Dispose all serialized executors (waits for reader tasks)
        foreach (var executor in _serializedExecutors.Values)
        {
            await executor.DisposeAsync();
        }
        
        _scope.Dispose();
    }
}
```

**Integration Points**:
- ✅ No changes needed to graph node execution model
- ✅ Reader task is internal to epoch implementation
- ✅ Blocks interact via standard async/await patterns
- ✅ Executor lifecycle managed by epoch disposal
- ✅ Each service type gets its own executor/reader task

---

### 4. Transaction Lifecycle Hooks

**Future Enhancement** (not yet implemented):

Epoch lifecycle hooks could be added for transaction management:

```csharp
public interface IEpochLifecycleHooks
{
    Task OnEpochCreatedAsync(IEpoch epoch, CancellationToken cancellationToken);
    Task OnEpochCompletedAsync(IEpoch epoch, CancellationToken cancellationToken);
    Task OnEpochFailedAsync(IEpoch epoch, Exception exception, CancellationToken cancellationToken);
}
```

**Configuration**:

```csharp
builder.ConfigureEpochs(epochConfig =>
{
    epochConfig.OnEpochCreated(async (epoch, ct) =>
    {
        await epoch.ExecuteSerializedAsync<DemoDbContext>(async db =>
        {
            await db.Database.BeginTransactionAsync(ct);
        }, ct);
    });
    
    epochConfig.OnEpochCompleted(async (epoch, ct) =>
    {
        await epoch.ExecuteSerializedAsync<DemoDbContext>(async db =>
        {
            await db.SaveChangesAsync(ct);
            await db.Database.CommitTransactionAsync(ct);
        }, ct);
    });
});
```

This would enable automatic transaction begin/commit around epoch boundaries.

---

## Implementation Status

### ✅ Completed (Phase 1)

**Core Infrastructure**:
- ✅ Added `ExecuteSerializedAsync` to `IEpoch` interface
- ✅ Implemented channel-backed `SerializedServiceExecutor<TService>`
- ✅ Multi-writer, single-reader pattern using `System.Threading.Channels`
- ✅ TaskCompletionSource for async/await semantics
- ✅ Per-service-type executor instances
- ✅ Proper lifecycle management (disposal, cancellation)
- ✅ Comprehensive unit tests (10 tests, all passing)

**Test Coverage** (`SerializedExecutionTests.cs`):
- ✅ Single operation execution
- ✅ Void operation execution
- ✅ Concurrent operations execute in order
- ✅ Exception propagation
- ✅ Cancellation handling
- ✅ Multiple concurrent blocks share same service
- ✅ Different service types use different executors
- ✅ Same service type uses same executor
- ✅ Epoch disposal shuts down cleanly
- ✅ Order preservation of submitted operations

**Files**:
- `/poc/DataFlow.POC/Core/SerializedServiceExecutor.cs` - Channel-backed executor
- `/poc/DataFlow.POC/Core/IEpoch.cs` - Updated interface
- `/poc/DataFlow.POC/Core/Epoch.cs` - Updated implementation
- `/poc/EpochAnchoringDemo.Tests/SerializedExecutionTests.cs` - Test coverage

### 🔮 Future Enhancements (Phase 2)

**Lifecycle Hooks** (not yet implemented):
- ⏳ Design and implement `IEpochLifecycleHooks` interface
- ⏳ Integrate into `EpochCoordinator`
- ⏳ Configuration API for hooks
- ⏳ Transaction begin/commit automation

**DbContext Examples** (can be added as needed):
- ⏳ Complete order processing example
- ⏳ Transaction lifecycle tests
- ⏳ End-user documentation

---

## Architecture Details

### Channel-Backed Pattern

**Why Channels?**
- ✅ Built-in support for multi-writer, single-reader scenarios
- ✅ Efficient async enumeration via `ReadAllAsync()`
- ✅ No manual lock management
- ✅ Clean separation of concerns
- ✅ Natural backpressure via unbounded channel + TaskCompletionSource

**Pattern Flow**:

```
[Block 1] --write--> |                    |
[Block 2] --write--> | Channel (unbounded)| --read--> [Reader Task] --executes--> Service
[Block 3] --write--> |                    |
                           ↓
                   TaskCompletionSource
                           ↓
                      await result
```

**Key Implementation Details**:

1. **Unbounded Channel**: No blocking on writes
2. **Single Reader**: Sequential execution guaranteed
3. **TaskCompletionSource**: Provides async/await for each operation
4. **Error Handling**: Exceptions captured and propagated to caller
5. **Cancellation**: Checked before execution, propagated correctly
6. **Disposal**: Graceful shutdown - completes pending operations

### Comparison to Semaphore Approach

| Aspect | Semaphore (Old Design) | Channel (Implemented) |
|--------|------------------------|----------------------|
| Concurrency Control | `SemaphoreSlim.WaitAsync()` | Channel reader task |
| Reader Tasks | N/A - blocks wait inline | Single dedicated task per service |
| Execution Model | Synchronous lock/release | Async channel enumeration |
| Integration | Simple but less idiomatic | Aligns with DataFlow patterns |
| Testability | Harder to verify ordering | Easy to verify FIFO ordering |
| Performance | Good for low contention | Better for high throughput |
| Scalability | One semaphore per service type | One channel + task per service type |

---

## Complete Example: Order Processing Pipeline

**Scenario**: Process orders with multiple blocks participating in a single epoch transaction.

```csharp
public class OrderPipelineConfig : IDataFlowConfiguration
{
    public void Configure(DataFlowBuilder builder)
    {
        builder
            // Source: read orders from database
            .AddProducer<Order>("order-source", sp => 
                sp.GetRequiredService<OrderSourceBlock>())
            
            // Transform: validate and enrich orders
            .AddTransform<Order, ValidatedOrder>("validator", sp =>
                sp.GetRequiredService<OrderValidatorBlock>())
            .ReceiveFrom("order-source")
            
            // Process: save to database using serialized DbContext access
            .AddProcessor<ValidatedOrder>("order-saver", sp =>
                sp.GetRequiredService<OrderSaverBlock>())
            .ReceiveFrom("validator");
    }
}

public class OrderSaverBlock : IProcessor<ValidatedOrder>
{
    public async Task ProcessAsync(
        IEpochStream<ValidatedOrder> input,
        CancellationToken cancellationToken)
    {
        await foreach (var order in input.Items)
        {
            // Multiple concurrent workers can safely call this
            // Operations are queued and executed serially
            await input.EpochScope!.ExecuteSerializedAsync<OrderDbContext>(
                async db =>
                {
                    var entity = new OrderEntity
                    {
                        OrderId = order.OrderId,
                        CustomerId = order.CustomerId,
                        Total = order.Total,
                        Status = OrderStatus.Pending
                    };
                    
                    db.Orders.Add(entity);
                    
                    // Add order lines
                    foreach (var line in order.Lines)
                    {
                        db.OrderLines.Add(new OrderLineEntity
                        {
                            OrderId = order.OrderId,
                            ProductId = line.ProductId,
                            Quantity = line.Quantity,
                            UnitPrice = line.UnitPrice
                        });
                    }
                    
                    await db.SaveChangesAsync(cancellationToken);
                },
                cancellationToken);
        }
    }
}
```

**Benefits of This Pattern**:
1. ✅ **Thread-Safe**: Multiple concurrent blocks can participate safely
2. ✅ **Transactional**: All operations in same epoch share same DbContext
3. ✅ **Simple API**: Blocks just call `ExecuteSerializedAsync`
4. ✅ **Error Handling**: Exceptions propagate correctly
5. ✅ **Cancellation**: Proper cancellation support
6. ✅ **Testable**: Easy to unit test with mocked services

---

## Implementation Phases

### ✅ Phase 1: Core Infrastructure (Completed)
- ✅ Add `ExecuteSerializedAsync` to `IEpoch`
- ✅ Implement channel-based serialization
- ✅ Unit tests
- ✅ Documentation

### 🔮 Phase 2: Lifecycle Hooks (Future)
- ⏳ Design and implement hooks interface
- ⏳ Integrate into `EpochCoordinator`
- ⏳ Configuration API

### 🔮 Phase 3: Advanced Examples (Future)
- ⏳ Complete order processing example
- ⏳ Transaction lifecycle tests
- ⏳ End-user documentation

---

## Trade-offs

### Pros
✅ Enables transactional semantics across multiple blocks  
✅ Simple API for block authors  
✅ Works with any service type  
✅ Clear transaction boundaries  
✅ Channel-backed pattern is idiomatic for DataFlow  
✅ Natural integration with epoch lifecycle  
✅ Excellent test coverage  

### Cons
❌ Serialization creates bottleneck for transaction operations  
❌ Not truly concurrent (by design - necessary for thread safety)  
❌ Requires discipline from block authors  
❌ Adds overhead for simple scenarios  

---

## References

- [ADR: Epoch Transaction De-Scope](../../../docs/adr/poc/2025-11-15-epoch-transaction-descope.md)
- Issue #125
- Implementation: `/poc/DataFlow.POC/Core/SerializedServiceExecutor.cs`
- Tests: `/poc/EpochAnchoringDemo.Tests/SerializedExecutionTests.cs`
