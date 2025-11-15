# Epoch-Scoped Transaction Coordination - De-Scope Decision

**Status**: De-scoped for v2  
**Date**: 2025-11-15  
**Decision**: Defer epoch-scoped concurrent transaction coordination to future releases

---

## Summary

Investigation into epoch-scoped transaction coordination revealed fundamental technical constraints with the Microsoft stack (SQL Server, ADO.NET, EF Core) that make **concurrent** transactional operations across multiple blocks within a single epoch **not viable** for production use with Azure SQL.

**Key Finding**: While epoch-scoped services (including DbContext) are fully functional for **sequential** access patterns, enabling **concurrent** transactional access would require MSDTC (Microsoft Distributed Transaction Coordinator), which is **not supported on Azure SQL** (only on Azure SQL Managed Instance).

---

## Technical Constraints

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

## Current Working Solution

### Sequential Access Pattern

The **current implementation** provides epoch-scoped DbContext instances that work correctly for **sequential** access patterns:

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

### Limitations

1. **No concurrent transactional operations within a single epoch**
   - Blocks must execute sequentially if they need to share a transaction
   - Concurrent operations require separate transactions

2. **Workaround for concurrent scenarios**
   - Use message queuing/channel pattern
   - Serialize transactional operations through a single writer
   - Each concurrent block queues work to be executed serially

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

**Why De-scope for v2**:

1. **Target deployment** is Azure SQL (not Managed Instance)
2. **Current sequential pattern** meets immediate needs
3. **Industry patterns** suggest moving away from distributed transactions
4. **Complexity vs. value** trade-off favors deferring this work
5. **Alternative patterns** (logical transactions, idempotent writes) are more scalable

**v2 Focus**: Prioritize other features that provide more immediate value without the technical constraints.

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
