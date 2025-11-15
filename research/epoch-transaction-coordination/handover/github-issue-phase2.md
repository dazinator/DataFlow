# Phase 2: Implement Epoch Lifecycle Hooks

**Epic**: Epoch-Scoped Serialized Transaction Access  
**Phase**: 2 of 5  
**Dependencies**: Phase 1 (Serialized Access)  
**Estimated Effort**: 3-4 days

---

## Goal

Add lifecycle hook support to epoch infrastructure, enabling custom logic at epoch creation, completion, and failure events. This provides the foundation for transaction management.

---

## Tasks

### 1. Design Lifecycle Hooks Interface

**File**: `poc/DataFlow.POC/Core/IEpochLifecycleHooks.cs` (new)

```csharp
public interface IEpochLifecycleHooks
{
    /// <summary>
    /// Called when a new epoch is created, before any blocks access it.
    /// </summary>
    Task OnEpochCreatedAsync(IEpoch epoch, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Called when all work for an epoch is complete, before disposal.
    /// </summary>
    Task OnEpochCompletedAsync(IEpoch epoch, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Called when an epoch fails/aborts.
    /// </summary>
    Task OnEpochFailedAsync(IEpoch epoch, Exception exception, CancellationToken cancellationToken = default);
}
```

### 2. Add Configuration API

**File**: `poc/DataFlow.POC/Core/EpochConfiguration.cs` (new)

```csharp
public class EpochConfiguration
{
    private readonly List<Func<IServiceProvider, IEpochLifecycleHooks>> _hookFactories = new();
    private Func<IEpoch, CancellationToken, Task>? _onCreated;
    private Func<IEpoch, CancellationToken, Task>? _onCompleted;
    private Func<IEpoch, Exception, CancellationToken, Task>? _onFailed;
    
    public void AddLifecycleHooks<T>() where T : IEpochLifecycleHooks;
    public void OnEpochCreated(Func<IEpoch, CancellationToken, Task> handler);
    public void OnEpochCompleted(Func<IEpoch, CancellationToken, Task> handler);
    public void OnEpochFailed(Func<IEpoch, Exception, CancellationToken, Task> handler);
}
```

### 3. Update EpochCoordinator

**File**: `poc/DataFlow.POC/Core/EpochCoordinator.cs`

Add:
- Private field for hooks: `private readonly IEpochLifecycleHooks? _lifecycleHooks;`
- Constructor parameter (optional)
- Hook calls at appropriate points:
  - `GetOrCreateEpochAsync`: Call `OnEpochCreatedAsync` after creating epoch
  - `NotifyEpochCompletedAsync`: Call `OnEpochCompletedAsync` before disposal
  - Error handling: Call `OnEpochFailedAsync` on exceptions

### 4. Error Handling

Ensure proper error handling:
- Hook errors don't prevent epoch disposal
- Failed hooks trigger `OnEpochFailedAsync`
- Log hook errors
- Propagate exceptions after cleanup

### 5. Create Tests

**File**: `poc/DataFlow.POC.Tests/Core/EpochLifecycleHooksTests.cs` (new)

Test cases:
- `OnEpochCreated_CalledWhenEpochCreated`
- `OnEpochCompleted_CalledWhenNotified`
- `OnEpochFailed_CalledOnException`
- `MultipleHooks_ExecuteInOrder`
- `HookError_TriggersOnEpochFailed`
- `InlineHandlers_ExecuteCorrectly`
- `HookError_DoesNotPreventDisposal`

---

## Acceptance Criteria

- [ ] `IEpochLifecycleHooks` interface created
- [ ] `EpochConfiguration` class with fluent API
- [ ] Hooks integrated into `EpochCoordinator`
- [ ] Error handling tested
- [ ] All tests pass
- [ ] Documentation complete

---

## Example Usage

```csharp
var coordinator = new EpochCoordinator(scopeFactory, hooks);

// Or with configuration:
builder.ConfigureEpochs(config =>
{
    config.OnEpochCreated(async (epoch, ct) =>
    {
        var logger = epoch.GetService<ILogger>();
        logger.LogInformation("Epoch {Vector} created", epoch.Vector);
    });
});
```

---

## References

- [Design Document](../design/serialized-dbcontext-access.md#3-transaction-lifecycle-hooks)
- [Implementation Plan](./implementation-plan.md#phase-2-lifecycle-hooks-infrastructure)

---

## Next Phase

After completion, proceed to **Phase 3: DbContext Transaction Lifecycle Hooks**
