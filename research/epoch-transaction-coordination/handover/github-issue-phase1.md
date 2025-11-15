# Phase 1: Add Serialized Access to Epoch Infrastructure

**Epic**: Epoch-Scoped Serialized Transaction Access  
**Phase**: 1 of 5  
**Dependencies**: None  
**Estimated Effort**: 2-3 days

---

## Goal

Add generic serialized access mechanism to epoch infrastructure, enabling multiple blocks to safely share epoch-scoped services through automatic operation queuing.

---

## Context

Currently, epoch-scoped services can be accessed via `epoch.GetService<T>()`, but this doesn't protect against concurrent access. For services like `DbContext` that are not thread-safe, we need a mechanism to serialize operations while maintaining the convenience of epoch scoping.

See [Design Document](../design/serialized-dbcontext-access.md) for complete design.

---

## Tasks

### 1. Update IEpoch Interface

**File**: `poc/DataFlow.POC/Core/IEpoch.cs`

Add new methods:

```csharp
/// <summary>
/// Executes an operation with serialized access to an epoch-scoped service.
/// Multiple concurrent callers will have their operations queued and executed sequentially.
/// </summary>
/// <typeparam name="TService">The type of service to access</typeparam>
/// <typeparam name="TResult">The result type</typeparam>
/// <param name="operation">The operation to execute with the service</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>The result of the operation</returns>
Task<TResult> ExecuteSerializedAsync<TService, TResult>(
    Func<TService, Task<TResult>> operation,
    CancellationToken cancellationToken = default) 
    where TService : notnull;
    
/// <summary>
/// Executes an operation with serialized access to an epoch-scoped service (void return).
/// </summary>
Task ExecuteSerializedAsync<TService>(
    Func<TService, Task> operation,
    CancellationToken cancellationToken = default) 
    where TService : notnull;
```

### 2. Implement in Epoch Class

**File**: `poc/DataFlow.POC/Core/Epoch.cs`

Add private field:
```csharp
private readonly ConcurrentDictionary<Type, SemaphoreSlim> _serviceLocks = new();
```

Implement methods using `SemaphoreSlim` for serialization (see design doc for complete implementation).

### 3. Update Disposal

Ensure semaphores are properly disposed in `DisposeAsync`:
```csharp
foreach (var semaphore in _serviceLocks.Values)
{
    semaphore.Dispose();
}
_serviceLocks.Clear();
```

### 4. Create Comprehensive Unit Tests

**File**: `poc/DataFlow.POC.Tests/Core/EpochSerializedAccessTests.cs` (new)

Test cases:
- `ExecuteSerializedAsync_SerializesAccess` - 100 concurrent increments, verify no race conditions
- `ExecuteSerializedAsync_DifferentServices_CanRunConcurrently` - Verify different service types don't block each other
- `ExecuteSerializedAsync_PropagatesExceptions` - Exceptions from operations are propagated
- `ExecuteSerializedAsync_RespectsCancellation` - Cancellation tokens work correctly
- `ExecuteSerializedAsync_AfterDisposal_ThrowsObjectDisposedException`
- `ExecuteSerializedAsync_MultipleWaiters_ExecuteInOrder`

### 5. Update Documentation

**File**: `poc/DataFlow.POC/Core/IEpoch.cs`

Add XML documentation with:
- Purpose and use cases
- Thread-safety guarantees
- Example usage
- Performance considerations

---

## Acceptance Criteria

- [ ] `IEpoch` interface updated with new methods
- [ ] `Epoch` class implements serialization using `SemaphoreSlim`
- [ ] All unit tests pass (6+ test cases)
- [ ] Zero race conditions detected in concurrent tests (100+ operations)
- [ ] Serialization overhead < 100μs per operation
- [ ] Semaphores properly disposed
- [ ] XML documentation complete
- [ ] Code review completed

---

## Testing Notes

Create a helper service for testing:

```csharp
public class CounterService
{
    public int Value { get; set; }
}

public class SlowService
{
    public async Task<int> SlowOperation()
    {
        await Task.Delay(100);
        return 42;
    }
}
```

---

## Non-Goals (Out of Scope)

- Transaction lifecycle (Phase 2)
- DbContext-specific functionality (Phase 3)
- Multi-block examples (Phase 4)

---

## References

- [Design Document](../design/serialized-dbcontext-access.md#1-generic-serialized-access-pattern)
- [Implementation Plan](./implementation-plan.md#phase-1-core-serialized-access-infrastructure)
- Current `IEpoch`: `poc/DataFlow.POC/Core/IEpoch.cs`
- Current `Epoch`: `poc/DataFlow.POC/Core/Epoch.cs`

---

## Next Phase

After completion, proceed to **Phase 2: Implement Epoch Lifecycle Hooks**
