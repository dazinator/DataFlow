# Design: Serialized DbContext Access for Epoch-Scoped Transactions

**Date**: 2025-11-15  
**Status**: Design Proposal  
**Related**: Issue #125 - De-scoped concurrent transactions, exploring serialized access pattern

---

## Overview

This design explores a pattern where multiple concurrent blocks can participate in a single epoch-scoped transaction through **serialized access** to a shared DbContext. While concurrent transactional operations are not viable (see ADR), we can provide a mechanism for concurrent blocks to queue transactional work that executes serially.

---

## Design Questions Addressed

### 1. Generic Serialized Access Pattern

**Question**: Does the epoch provide access to a generic method which can provide serialised usage of a dependency/service from epoch DI scope based on a channel to reduce many writers to single reader callback access pattern?

**Proposed Design**:

Add a new method to `IEpoch`:

```csharp
public interface IEpoch : IAsyncDisposable
{
    EpochVector Vector { get; }
    T GetService<T>() where T : notnull;
    IServiceProvider ServiceProvider { get; }
    
    /// <summary>
    /// Executes an operation with serialized access to an epoch-scoped service.
    /// Multiple concurrent callers will have their operations queued and executed sequentially.
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

**Implementation Approach**:

```csharp
internal sealed class Epoch : IEpoch
{
    private readonly ConcurrentDictionary<Type, SemaphoreSlim> _serviceLocks = new();
    
    public async Task<TResult> ExecuteSerializedAsync<TService, TResult>(
        Func<TService, Task<TResult>> operation,
        CancellationToken cancellationToken = default) 
        where TService : notnull
    {
        var semaphore = _serviceLocks.GetOrAdd(
            typeof(TService), 
            _ => new SemaphoreSlim(1, 1));
        
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var service = GetService<TService>();
            return await operation(service);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
```

---

### 2. EF Core Example with Multiple Blocks

See complete examples in sections below.

---

### 3. Transaction Lifecycle Hooks

**Proposed Design**: Epoch Lifecycle Hooks

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

---

## Complete Example: Order Processing Pipeline

See the implementation handover document for full code examples.

---

## Implementation Phases

### Phase 1: Core Infrastructure
- Add `ExecuteSerializedAsync` to `IEpoch`
- Implement semaphore-based serialization
- Unit tests

### Phase 2: Lifecycle Hooks
- Design and implement hooks interface
- Integrate into `EpochCoordinator`
- Configuration API

### Phase 3: DbContext Examples
- Complete order processing example
- Transaction lifecycle tests
- Documentation

---

## Trade-offs

### Pros
✅ Enables transactional semantics across multiple blocks  
✅ Simple API  
✅ Works with any service type  
✅ Clear transaction boundaries  

### Cons
❌ Serialization creates bottleneck  
❌ Not truly concurrent  
❌ Requires discipline  

---

## References

- [ADR: Epoch Transaction De-Scope](../../../docs/adr/poc/2025-11-15-epoch-transaction-descope.md)
- Issue #125
