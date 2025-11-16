# Epoch-Scoped Transaction Coordination - Channel-Based Implementation

**Status**: ✅ Implemented (Channel-Based Pattern)  
**Date**: 2025-11-15  
**Decision**: Concurrent epoch transactions enabled via channel-backed serialized execution

---

## Summary

Investigation into epoch-scoped transaction coordination has been completed and **a channel-backed serialized execution pattern has been implemented**. This enables multiple concurrent blocks to participate in epoch-scoped transactions through serialized access to shared services like DbContext.

**Key Achievement**: While **truly concurrent** transactional operations across multiple blocks remain constrained by Microsoft stack limitations (see below), we now provide a **practical solution** that allows multiple blocks to **queue transactional operations** that execute serially via a channel-backed pattern.

---

## Implementation Overview

### Channel-Backed Serialized Execution

**New API**: `IEpoch.ExecuteSerializedAsync<TService, TResult>()`

Multiple concurrent blocks can now safely participate in epoch transactions by submitting operations to a channel. A single reader task processes these operations sequentially, ensuring thread safety while maintaining the simplicity of the epoch-scoped service model.

**Pattern**:
```
[Block 1] --write--> |                    |
[Block 2] --write--> | Channel (unbounded)| --read--> [Reader Task] --execute--> Service
[Block 3] --write--> |                    |
                           ↓
                   TaskCompletionSource
                           ↓
                      await result
```

**Usage Example**:
```csharp
// Block 1: Add order
await epoch.ExecuteSerializedAsync<OrderDbContext>(async db =>
{
    db.Orders.Add(new Order { CustomerId = customerId });
    await db.SaveChangesAsync();
});

// Block 2: Add line items (concurrent with block 1)
await epoch.ExecuteSerializedAsync<OrderDbContext>(async db =>
{
    db.OrderLines.Add(new OrderLine { OrderId = orderId, ... });
    await db.SaveChangesAsync();
});
```

**See**: [Serialized DbContext Access Design](design/serialized-dbcontext-access.md) for complete implementation details.

---

## Technical Constraints (Microsoft Stack)

### 1. Multiple Connections → MSDTC Promotion

**Rule**: The moment two different `SqlConnection` instances participate in a `TransactionScope`, the transaction is automatically escalated to MSDTC.

```csharp
using (var scope = new TransactionScope())
{
    using var c1 = new SqlConnection(connString);
    using var c2 = new SqlConnection(connString);

    c1.Open(); // First enlistment, local transaction
    c2.Open(); // Second enlistment → MSDTC promotion
}
```

This is **deterministic behavior** in `System.Transactions`, even if no SQL statements have been executed.

**Why This Matters**: 
- Azure SQL does **not support MSDTC**
- Only Azure SQL Managed Instance supports MSDTC (higher cost)
- This makes concurrent transactional operations infeasible for our target deployment scenario

### 2. DbContext Thread-Safety

**Rule**: DbContext and SqlConnection are **not thread-safe**.

**Implications**:
- Cannot use a single `DbContext` from multiple concurrent blocks
- Cannot use a single `SqlConnection` from multiple threads
- Even with MARS (Multiple Active Result Sets), only one active SQL data reader per connection

### 3. Connection Concurrency Limits

**Rule**: Even with MARS enabled, there are severe concurrency limitations:
- Only one active SQL data reader per connection
- Downstream blocks reading different result sets concurrently is impossible with one connection

---

## Investigated Approaches

### Approach 1: Shared DbContext (Rejected)

**Idea**: All blocks in an epoch share a single DbContext instance.

**Why It Doesn't Work**:
- DbContext is not thread-safe
- Concurrent access leads to races and protocol violations

### Approach 2: Multiple DbContexts in TransactionScope (Rejected)

**Idea**: Each concurrent block has its own DbContext, all enlisted in a shared `TransactionScope`.

**Why It Doesn't Work**:
- Each DbContext opens its own SqlConnection
- Multiple SqlConnections → MSDTC promotion
- MSDTC not supported on Azure SQL

### Approach 3: Pre-open All Connections (Rejected)

**Idea**: Open all connections at epoch creation before any work happens to avoid MSDTC promotion.

**Why It Doesn't Work**:
- Opening multiple connections **still** causes MSDTC promotion
- The presence of "no work yet" does not matter
- System.Transactions promotes based on connection count, not activity

---

## What We Built

### ✅ Working Solution: Channel-Backed Serialized Execution

The **implemented solution** provides epoch-scoped transactional coordination through:

**1. Core Infrastructure** (`/poc/DataFlow.POC/Core/`)
- ✅ `SerializedServiceExecutor<TService>` - Channel-backed execution
- ✅ `IEpoch.ExecuteSerializedAsync()` - Public API
- ✅ Multi-writer, single-reader pattern
- ✅ Proper lifecycle management (tied to epoch disposal)

**2. Features**
- ✅ Multiple blocks queue operations via channel
- ✅ Single reader task processes serially (thread-safe)
- ✅ TaskCompletionSource for async/await semantics
- ✅ Error propagation and cancellation support
- ✅ Order preservation (FIFO)
- ✅ Per-service-type executor instances

**3. Test Coverage** (25 tests, all passing)
- ✅ 11 existing epoch tests (backward compatible)
- ✅ 10 serialized execution tests
- ✅ 4 integration tests (real-world scenarios)

**Files**:
- Implementation: `/poc/DataFlow.POC/Core/SerializedServiceExecutor.cs`
- Tests: `/poc/EpochAnchoringDemo.Tests/SerializedExecutionTests.cs`
- Integration: `/poc/EpochAnchoringDemo.Tests/EpochTransactionIntegrationTests.cs`
- Design: `/research/epoch-transaction-coordination/design/serialized-dbcontext-access.md`

### Sequential Access Pattern (Already Supported)

The **existing implementation** provides epoch-scoped DbContext instances that work correctly for **sequential** access patterns:

```csharp
// Multiple blocks in the same epoch can share the same DbContext
// when they execute SEQUENTIALLY (not concurrently)

var epoch = await coordinator.GetOrCreateEpochAsync("source", vector);

// Block 1 - sequential access
var dbContext = epoch.GetService<DemoDbContext>();
dbContext.DataRecords.Add(new DataRecord { Name = "Item 1" });
await dbContext.SaveChangesAsync();

// Block 2 - sequential access to same DbContext instance
var sameContext = epoch.GetService<DemoDbContext>();
var record = await sameContext.DataRecords.FindAsync(1);
```

**Demonstrated in Tests**: `EpochScopedDbContextTests.cs` shows:
- ✅ Multiple blocks share same DbContext in same epoch (sequential)
- ✅ Different epochs get different DbContext instances
- ✅ No race conditions in concurrent **epoch** execution (different epochs)
- ✅ Changes visible across blocks within same epoch

---

## Alternative Patterns for Production

Based on industry best practices (Kafka Streams, Flink, Beam, Kinesis, Databricks), the recommended pattern for parallel pipelines with consistency requirements is:

### Logical (Application-Level) Transactions

Instead of physical DB transactions spanning multiple blocks:

1. **Each block writes on its own local connection**
2. **Writes are idempotent**
3. **Epoch completion determines commit of logical state**
4. **Checkpoints ensure recovery**

**Benefits**:
- No physical DB transaction spans multiple blocks
- Scales horizontally
- No MSDTC requirement
- Industry-proven pattern

---

## Future Work Considerations

If epoch-scoped concurrent transactions become a requirement:

### Option 1: Azure SQL Managed Instance
- Supports MSDTC
- Higher cost
- Would enable the multi-connection TransactionScope pattern

### Option 2: Serialized Transaction Service
- Queue-based pattern for concurrent blocks to participate in a shared transaction
- Blocks queue operations via channel
- Single consumer executes against shared DbContext
- Trade-off: serializes transaction work, but preserves concurrent non-transactional work

### Option 3: Event Sourcing / CQRS
- Separate read and write models
- Eventual consistency
- Better scalability characteristics

---

## Decision Rationale

**Why Implement Channel-Based Pattern**:

1. ✅ **Practical Solution**: Enables concurrent blocks to participate in epoch transactions
2. ✅ **Thread-Safe**: Channel pattern ensures safe serialization
3. ✅ **Idiomatic**: Aligns with DataFlow patterns (channels are core to the library)
4. ✅ **Minimal API Surface**: Single method `ExecuteSerializedAsync()`
5. ✅ **Backward Compatible**: Existing epoch functionality unchanged
6. ✅ **Well-Tested**: Comprehensive test coverage validates behavior

**Trade-offs Accepted**:
- ❌ Operations execute serially (not truly concurrent) - necessary for thread safety
- ❌ Adds latency for high-volume transactional workloads
- ✅ But: Simple API, thread-safe, and works with Azure SQL

**v2 Focus**: This implementation provides immediate value without complex workarounds or infrastructure changes.

---

## References

- **Claude Analysis**: [Multiple DbContext instances in single transaction](https://claude.ai/chat/bc110c2d-6d5a-48e0-964a-12db389375eb)
- **Tests**: `poc/EpochAnchoringDemo.Tests/EpochScopedDbContextTests.cs`
- **Foundation Work**: Issue #429 (Epoch-scoped DI services)
- **Related**: `UNDERSTANDING_ANCHORS_VS_CHECKPOINTS.md` in POC documentation

---

## See Also

- [Epoch Anchoring Demo README](../../poc/EpochAnchoringDemo/README.md) - Current working implementation
- [ADR: Epoch Transaction De-Scope](../../docs/adr/poc/2025-11-15-epoch-transaction-descope.md) - Architecture decision record
