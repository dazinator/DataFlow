# Epoch Transaction Coordination - Prototype v2 (Simplified Fully-Serial Design)

## Overview

This prototype demonstrates **fully serial** epoch-scoped transaction coordination using a channel-backed pattern. Unlike v1, this design ensures ALL operations execute serially (not just per service type), which:

- ✅ Avoids MSDTC escalation risks
- ✅ Prevents concurrency bugs from multiple DbContext types sharing a connection
- ✅ Simpler architecture (no dictionary of executors, single operations queue)

## Key Changes from v1

| Aspect | v1 (Concurrent per Service Type) | v2 (Fully Serial) |
|--------|-----------------------------------|-------------------|
| Concurrency | Serial per service type | Fully serial across all operations |
| Architecture | `Dictionary<Type, SerializedServiceExecutor<T>>` | Single `Channel<IEpochOperation>` |
| Type resolution | Generic executor per service type | Encapsulated in operation instance |
| MSDTC risk | ⚠️ Possible if different DbContext types | ✅ Eliminated (fully serial) |

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│ Epoch Instance                                               │
│                                                               │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ Single Operations Channel<IEpochOperation>           │   │
│  │ (Unbounded, Multi-Writer, Single-Reader)             │   │
│  └──────────────────────────────────────────────────────┘   │
│                         │                                    │
│                         ├─ Block 1: QueueOperation<DbContext>│
│                         ├─ Block 2: QueueOperation<DbContext>│
│                         ├─ Block 3: QueueOperation<OtherSvc> │
│                         │                                    │
│                    ┌────▼────────────┐                       │
│                    │ Reader Task     │                       │
│                    │ (Serial Execute)│                       │
│                    └─────────────────┘                       │
│                                                               │
│  Operations are FULLY SERIAL across all service types        │
└───────────────────────────────────────────────────────────────┘
```

## Core Components

### 1. IEpochOperation Interface

Non-generic interface that all operations implement:

```csharp
public interface IEpochOperation
{
    Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
```

### 2. EpochOperation<TService> Implementation

Generic wrapper that encapsulates type resolution:

```csharp
internal class EpochOperation<TService> : IEpochOperation
    where TService : notnull
{
    private readonly Func<TService, Task> _operation;
    
    public async Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        var service = serviceProvider.GetRequiredService<TService>();
        await _operation(service);
    }
}
```

### 3. IEpoch.QueueSerializedOperationAsync<TService>

Generic helper method that creates typed operation and queues it:

```csharp
public interface IEpoch
{
    Task QueueSerializedOperationAsync<TService>(
        Func<TService, Task> operation,
        CancellationToken cancellationToken = default)
        where TService : notnull;
}
```

Implementation:
```csharp
public Task QueueSerializedOperationAsync<TService>(
    Func<TService, Task> operation,
    CancellationToken cancellationToken = default)
    where TService : notnull
{
    var epochOperation = new EpochOperation<TService>(operation, cancellationToken);
    
    // Write to single operations channel (fully serial)
    _operationsChannel.Writer.TryWrite(epochOperation);
    Interlocked.Increment(ref _queuedCount);
    
    return Task.CompletedTask; // Fire-and-forget
}
```

### 4. Reader Task (in EpochProcessorNode)

Processes operations serially:

```csharp
await foreach (var operation in epoch.OperationsReader.ReadAllAsync(ct))
{
    await operation.ExecuteAsync(epoch.ServiceProvider, ct);
    epoch.NotifyOperationExecuted();
}
```

## Usage Example

```csharp
public class OrderProcessorBlock : IProcessor<Order>
{
    public async Task ProcessAsync(IEpochStream<Order> input, CancellationToken ct)
    {
        await foreach (var order in input.Items)
        {
            // Operations queued and executed fully serially
            await input.EpochScope!.QueueSerializedOperationAsync<OrderDbContext>(
                async db =>
                {
                    var entity = new OrderEntity { OrderId = order.OrderId, ... };
                    db.Orders.Add(entity);
                    await db.SaveChangesAsync(ct);
                },
                ct);
        }
    }
}

public class InventoryBlock : IProcessor<Order>
{
    public async Task ProcessAsync(IEpochStream<Order> input, CancellationToken ct)
    {
        await foreach (var order in input.Items)
        {
            // Even though different service type, still fully serial with OrderDbContext
            await input.EpochScope!.QueueSerializedOperationAsync<InventoryDbContext>(
                async db =>
                {
                    db.Inventory.Update(...);
                    await db.SaveChangesAsync(ct);
                },
                ct);
        }
    }
}
```

Operations execute in FIFO order:
1. OrderDbContext operation from OrderProcessorBlock (Order 1)
2. InventoryDbContext operation from InventoryBlock (Order 1)
3. OrderDbContext operation from OrderProcessorBlock (Order 2)
4. ...

## Benefits

### ✅ Fully Serial Execution

All operations execute sequentially, regardless of service type. Eliminates:
- MSDTC escalation (no concurrent connections)
- Concurrency bugs (no concurrent DbContext access)
- Race conditions

### ✅ Simpler Architecture

- No `Dictionary<Type, SerializedServiceExecutor<T>>`
- No per-service-type executor instantiation
- Single operations channel per epoch
- Easier to reason about execution order

### ✅ Type-Safe Generic API

Blocks still get type-safe API:
```csharp
await epoch.QueueSerializedOperationAsync<DbContext>(async db => { ... });
```

Generic method encapsulates type resolution, but execution is fully serial.

### ✅ Encapsulation via Interface

`IEpochOperation` interface allows:
- Non-generic channel storage
- Type-erased reader task
- Clean separation between queueing (generic) and execution (non-generic)

## Performance Characteristics

- **Operation Throughput**: Fully serial (one at a time)
- **Queuing**: Non-blocking (unbounded channel)
- **Type Resolution**: Per-operation overhead (acceptable for DB operations)
- **GC Pressure**: One `EpochOperation<T>` allocation per queued operation

## Integration with Epoch Nodes

This design naturally integrates with EpochProcessorNode:

```csharp
public class EpochProcessorNode : IGraphNode
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var epoch in _epochStream.ReadAllAsync(ct))
        {
            // Hook: BeginEpoch
            await _hooks.OnBeginEpochAsync(epoch, ct);
            
            // Drain operations serially
            await foreach (var operation in epoch.OperationsReader.ReadAllAsync(ct))
            {
                await operation.ExecuteAsync(epoch.ServiceProvider, ct);
                epoch.NotifyOperationExecuted(); // Track progress
            }
            
            // Hook: CommitEpoch
            await _hooks.OnCommitEpochAsync(epoch, ct);
            
            // Dispose epoch
            await epoch.DisposeAsync();
        }
    }
}
```

## Test Results

- **Unit Tests**: 10 tests (updated for v2 pattern)
- **Integration Tests**: 4 tests (updated for v2 pattern)
- **Existing Epoch Tests**: 11 tests, backward compatible ✅

**Total**: 25 tests passing

## Files

- `IEpochOperation.cs` - Non-generic operation interface
- `EpochOperation.cs` - Generic operation implementation with type encapsulation
- `IEpoch.cs` - Updated interface with single operations channel
- `Epoch.cs` - Simplified implementation (no dictionary, single channel)

## Migration from v1

1. **Remove**: `SerializedServiceExecutor<TService>` class (no longer needed)
2. **Remove**: `_executors` dictionary from `Epoch` (no longer needed)
3. **Add**: `IEpochOperation` interface
4. **Add**: `EpochOperation<TService>` implementation
5. **Add**: Single `_operationsChannel` to `Epoch`
6. **Update**: `QueueSerializedOperationAsync` to create `EpochOperation<T>` and queue to single channel
7. **Update**: Reader logic in EpochProcessorNode to call `operation.ExecuteAsync(serviceProvider, ct)`

## Why This Design is Better

### Problem with v1
v1 allowed concurrent execution per service type. This meant if you had:
- `OrderDbContext` operations
- `CustomerDbContext` operations

They could execute concurrently, which:
- ❌ Could trigger MSDTC if both share a connection
- ❌ Could cause concurrency bugs
- ❌ Was more complex (dictionary of executors)

### Solution in v2
v2 ensures ALL operations execute serially:
- ✅ No MSDTC risk (only one connection active at a time)
- ✅ No concurrency bugs (inherently thread-safe)
- ✅ Simpler code (single channel, no dictionary)

The generic helper method is just syntactic sugar - it encapsulates type resolution in the operation itself using the "closure with type parameter" pattern.

---

**Design Status**: ✅ Validated (v2 - Fully Serial)  
**MSDTC Risk**: ✅ Eliminated  
**Architecture**: Simplified (single operations queue)  
**Ready for**: EpochProcessorNode integration
