# ADR: De-Scope Epoch-Scoped Concurrent Transactions for v2

**Date**: 2025-11-15  
**Status**: Accepted  
**Context**: Issue #125 - Investigate Epoch-Scoped Transaction Coordination Model

---

## Context

During POC development, we investigated the feasibility of enabling **concurrent transactional operations** across multiple blocks within a single epoch. The goal was to allow multiple concurrent blocks to share a single database transaction within epoch boundaries.

### What We Built

We successfully implemented **epoch-scoped DI services** (Issue #429), which enables:
- Epoch-scoped service lifetime aligned with epoch boundaries
- Multiple blocks can resolve the same service instance within an epoch
- Proper disposal when epoch completes

This foundation works well for **sequential** access patterns but revealed constraints for **concurrent** transactional scenarios.

---

## Decision

**We will de-scope epoch-scoped concurrent transaction coordination for v2.**

Instead, we will:
1. Document the sequential access pattern that works today
2. Defer concurrent transaction support to a future release
3. Focus v2 effort on higher-value features without technical constraints

---

## Rationale

### Technical Constraints

Three fundamental limitations of the Microsoft stack make concurrent epoch transactions infeasible for our target deployment (Azure SQL):

#### 1. MSDTC Requirement

When multiple `SqlConnection` instances participate in a `TransactionScope`, the transaction is **automatically escalated to MSDTC** (Microsoft Distributed Transaction Coordinator).

**Critical Issue**: Azure SQL does **not support MSDTC**. Only Azure SQL Managed Instance supports it (higher cost).

#### 2. Thread-Safety

Both `DbContext` and `SqlConnection` are **not thread-safe**:
- Cannot be used concurrently from multiple threads
- Even with MARS (Multiple Active Result Sets), severe concurrency limitations exist
- Only one active SQL data reader per connection

#### 3. Connection Pooling Limitations

Opening multiple connections early (before work happens) does **not** avoid MSDTC promotion:
- Promotion happens based on connection count, not activity
- No way to have multiple connections in a single local transaction

### What Works Today

The current implementation provides **epoch-scoped DbContext instances** that work correctly for:

✅ **Sequential access** - Multiple blocks accessing same DbContext instance sequentially  
✅ **Epoch isolation** - Different epochs get different DbContext instances  
✅ **Concurrent epochs** - Multiple epochs can execute concurrently (each with its own DbContext)  
✅ **Change tracking** - Changes visible across blocks within same epoch

This pattern is **sufficient for many use cases** and aligns with industry best practices.

### Alternative Patterns

Industry-standard streaming frameworks (Kafka Streams, Flink, Beam, Kinesis, Databricks) use **logical transactions** instead of physical database transactions:

- Each block writes on its own connection
- Writes are idempotent
- Epoch completion determines commit of logical state
- Checkpoints ensure recovery

This pattern **scales better** and **avoids MSDTC entirely**.

---

## Consequences

### Positive

1. **Avoid technical dead-end**: MSDTC is not viable for Azure SQL
2. **Focus on value**: Redirect effort to features that work within constraints
3. **Industry alignment**: Sequential/logical transaction patterns are proven at scale
4. **Working solution**: Current sequential pattern meets immediate needs

### Negative

1. **Limited concurrency**: Transactional operations must be sequential within an epoch
2. **Future work**: If concurrent transactions become critical, will require:
   - Migration to Azure SQL Managed Instance, OR
   - Implement serialized transaction service pattern, OR
   - Adopt event sourcing/CQRS patterns

### Neutral

1. **Documentation burden**: Must clearly document the sequential access limitation
2. **Test coverage**: Tests demonstrate working pattern but not concurrent scenario

---

## Workarounds

If concurrent transactional operations are needed in the future:

### Option 1: Serialized Transaction Service

```csharp
public class EpochTransactionService
{
    private readonly Channel<TransactionOperation> _operations;
    
    public async Task<T> EnqueueOperation<T>(Func<DbContext, Task<T>> operation)
    {
        // Queue operation to be executed serially
        // Single consumer executes all operations sequentially
    }
}
```

**Trade-off**: Serializes transaction work, but preserves concurrent non-transactional work.

### Option 2: Azure SQL Managed Instance

Enable MSDTC support by migrating to Managed Instance (higher cost).

### Option 3: Logical Transactions

Adopt event sourcing or CQRS patterns for eventual consistency.

---

## Implementation

### Documentation Updates

- ✅ Research documentation: `/research/epoch-transaction-coordination/README.md`
- ✅ ADR: This document
- ⏳ Update POC README with transaction limitations
- ⏳ Update issue with final status

### Test Coverage

Existing tests in `EpochScopedDbContextTests.cs` demonstrate:
- Sequential access pattern
- Epoch isolation
- Concurrent epoch execution

**Not tested**: Concurrent transactional access (by design - not supported).

---

## References

- **Issue**: #125 - Investigate Epoch-Scoped Transaction Coordination Model
- **Foundation**: #429 - Epoch-Scoped DI Services
- **Analysis**: [Claude conversation on distributed transactions](https://claude.ai/chat/bc110c2d-6d5a-48e0-964a-12db389375eb)
- **Tests**: `poc/EpochAnchoringDemo.Tests/EpochScopedDbContextTests.cs`
- **Research**: `/research/epoch-transaction-coordination/README.md`

---

## Revision History

| Date | Change |
|------|--------|
| 2025-11-15 | Initial decision to de-scope for v2 |
