# ADR: Formalized Epoch System with EpochSourceNode and EpochProcessorNode

**Date**: 2025-11-16  
**Status**: Accepted  
**Context**: Issue #456 - Epoch Node Architecture Implementation

---

## Context

The POC initially explored an **out-of-band epoch control plane** design where epoch propagation occurred via events, separate from the data flow channels. This pattern aimed to provide zero hot-path overhead by separating control signals from data.

However, as the implementation evolved, we identified several limitations with the event-based approach and developed a simpler, more robust alternative: the **formalized epoch system** using `EpochSourceNode` and `EpochProcessorNode`.

---

## Options Considered

### Option 1: Out-of-Band Event-Based Control Plane (Original Design)

**Architecture:**
- Separate `EpochManager` for centralized coordination
- Event-based epoch propagation (not through data channels)
- Blocks implement `IEpochPublisher`/`IEpochSubscriber` interfaces
- Manual alignment detection via `EpochProgress` tracking

**Pros:**
- Zero hot-path overhead (no control signals in data channels)
- Theoretically clean separation of data and control planes
- Flexible multi-source coordination

**Cons:**
- **High complexity**: Event-based coordination across all blocks
- **Tight coupling**: All blocks must implement epoch interfaces
- **Complex alignment logic**: Manual tracking of epoch progress per block
- **State management burden**: Each block maintains epoch state
- **Difficult to reason about**: Event timing and ordering complexities
- **Hard to test**: Asynchronous event coordination makes testing difficult

### Option 2: Formalized Epoch System with Specialized Nodes (Chosen)

**Architecture:**
- `EpochSourceNode`: Creates and publishes epochs to a channel stream
- `EpochProcessorNode`: Consumes epochs, drains operation queues, executes lifecycle hooks
- Channel-based communication (not events)
- Explicit lifecycle via `EpochHooks` (OnBeginEpoch, OnCommitEpoch, OnEpochError)
- Serialized operation execution via `IEpoch.QueueSerializedOperationAsync`

**Pros:**
- **Simplicity**: Channel-based communication is easier to understand
- **Explicit lifecycle**: Hooks make transaction boundaries clear
- **No special interfaces**: Blocks don't need epoch-specific implementations
- **BufferNode pattern**: Follows established DataFlow specialized node pattern
- **Easy to test**: Deterministic channel-based flow
- **Configurable concurrency**: 1-N processors for serial vs parallel execution
- **MSDTC-safe**: Fully serial execution prevents distributed transaction escalation
- **Better DI integration**: Each epoch has its own DI scope

**Cons:**
- Epochs are explicitly managed (not automatic propagation)
- Requires explicit epoch creation and publishing

---

## Decision

**We chose Option 2: Formalized Epoch System with `EpochSourceNode` and `EpochProcessorNode`.**

The key insight is that **simplicity and explicitness** are more valuable than the theoretical benefits of event-based separation. The formalized system provides:

1. **Clear ownership**: `EpochSourceNode` creates epochs, `EpochProcessorNode` processes them
2. **Explicit lifecycle**: Hooks execute at well-defined points
3. **Type-safe operations**: `QueueSerializedOperationAsync<TService>` provides compile-time safety
4. **Configurable concurrency**: Multiple processors enable parallel epoch processing
5. **MSDTC-safe design**: Serial execution within epochs prevents transaction escalation

### Implementation Details

```csharp
// Create coordinator and source
var coordinator = new EpochCoordinator(scopeFactory);
var source = new EpochSourceNode(coordinator);

// Configure lifecycle hooks
var hooks = new EpochHooks
{
    OnBeginEpoch = async (epoch, ct) =>
    {
        await epoch.QueueSerializedOperationAsync<DbContext>(
            async db => await db.Database.BeginTransactionAsync(ct), ct);
    },
    OnCommitEpoch = async (epoch, ct) =>
    {
        await epoch.QueueSerializedOperationAsync<DbContext>(async db =>
        {
            await db.SaveChangesAsync(ct);
            await db.Database.CommitTransactionAsync(ct);
        }, ct);
    }
};

// Create processor(s)
var processor = new EpochProcessorNode(source, hooks);

// Process epochs
var vector = EpochVector.FromSingleSource("source", sequenceNumber);
var epoch = await coordinator.GetOrCreateEpochAsync("source", vector);
await epoch.QueueSerializedOperationAsync<DbContext>(async db =>
{
    db.Orders.Add(order);
});
await source.PublishEpochAsync(epoch);

// Complete
source.SignalCompletion();
await processor.CompletionTask;
```

---

## Consequences

### Positive

1. **Reduced complexity**: Channel-based communication is simpler than event coordination
2. **Better testability**: Deterministic flow makes testing straightforward
3. **Clear lifecycle**: Explicit hooks make transaction boundaries obvious
4. **Industry alignment**: Follows BufferNode specialized node pattern
5. **MSDTC-safe**: Serial execution prevents distributed transaction issues
6. **DI integration**: Each epoch has its own scope for scoped services
7. **Configurable concurrency**: Easy to scale with multiple processors

### Negative

1. **Explicit management**: Epochs must be created and published explicitly
2. **Not automatic**: No automatic epoch propagation through the graph

### Neutral

1. **Different mental model**: Developers must understand epoch nodes vs automatic propagation
2. **Migration needed**: Old out-of-band patterns need to be updated

---

## Related

- **Related issue**: uniun-technology/lib-dataflow#456 (Epoch Node Architecture)
- **Related issue**: uniun-technology/lib-dataflow#125 (Parent - Epoch-Scoped Transaction Coordination)
- **Design document**: `/research/epoch-transaction-coordination/handover/epoch-node-architecture.md`
- **Related ADR**: `2025-11-15-epoch-transaction-descope.md` (Serial execution decision)
- **Supersedes**: Out-of-band epoch control plane design (poc/docs/design/epoch-control-plane.md)

---

## Validation

The formalized epoch system has been validated through:

- ✅ 17 comprehensive tests (all passing)
- ✅ Multi-processor concurrency tests (1, 2, 4 processors)
- ✅ Transaction isolation validation
- ✅ Lifecycle hook execution tests
- ✅ Error handling tests
- ✅ DbContext scoping tests
- ✅ End-to-end integration tests

**Performance characteristics** (from validation report):
- Epoch creation: Expected < 1μs
- Operation throughput: Tests show 32k ops/sec with Task.Yield overhead
- Multi-processor scaling: Linear scaling demonstrated
- Memory: No leaks observed in comprehensive test runs
