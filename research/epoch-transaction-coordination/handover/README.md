# Implementation Handover: Epoch-Scoped Serialized Transaction Access

**Date**: 2025-11-15  
**Status**: Ready for Implementation  
**Parent Issue**: #125

---

## Quick Navigation

- **Design**: [Serialized DbContext Access Design](../design/serialized-dbcontext-access.md)
- **Implementation Plan**: [5-Phase Implementation Plan](./implementation-plan.md)
- **Issue Templates**: 
  - [Phase 1: Serialized Access Infrastructure](./github-issue-phase1.md)
  - [Phase 2: Lifecycle Hooks](./github-issue-phase2.md)
  - More phases in implementation-plan.md

---

## What This Provides

A pattern for multiple concurrent blocks to participate in a single epoch-scoped transaction through **serialized access** to shared services like DbContext.

### Key Capabilities

1. **Generic Serialization**: Any epoch-scoped service can be accessed safely
2. **Transaction Lifecycle**: Hooks for begin/commit/rollback
3. **Multi-Block Support**: Multiple blocks share same DbContext/transaction
4. **Interim Operations**: Support for SaveChanges within transaction
5. **Automatic Queueing**: Concurrent operations queue automatically

---

## Design Overview

### API Surface

```csharp
// Serialized access to any service
await epoch.ExecuteSerializedAsync<DemoDbContext, Order>(async db =>
{
    db.Orders.Add(order);
    await db.SaveChangesAsync(); // Interim save for ID generation
    return order;
});

// Transaction lifecycle
builder.ConfigureEpochs(config =>
{
    config.OnEpochCreated(async (epoch, ct) =>
    {
        await epoch.ExecuteSerializedAsync<DemoDbContext>(db =>
            db.Database.BeginTransactionAsync(ct));
    });
    
    config.OnEpochCompleted(async (epoch, ct) =>
    {
        await epoch.ExecuteSerializedAsync<DemoDbContext>(async db =>
        {
            await db.SaveChangesAsync(ct);
            await db.Database.CommitTransactionAsync(ct);
        });
    });
});
```

---

## Implementation Phases

| Phase | Goal | Effort | Dependencies |
|-------|------|--------|--------------|
| 1 | Core serialized access | 2-3 days | None |
| 2 | Lifecycle hooks | 3-4 days | Phase 1 |
| 3 | DbContext transactions | 2-3 days | Phase 2 |
| 4 | Multi-block example | 4-5 days | Phase 3 |
| 5 | Documentation | 2-3 days | Phase 4 |

**Total**: 13-18 days

---

## Creating Issues

### Recommended: Multi-Issue Approach

Create 5 separate issues (one per phase) for:
- Better progress tracking
- Clearer PR scope
- Parallel development potential

### Issue Creation Steps

1. Create parent epic issue linking to all phases
2. Create Phase 1 issue (use template `github-issue-phase1.md`)
3. After Phase 1 complete, create Phase 2 issue
4. Continue sequentially

### Issue Labels

Suggest:
- `enhancement`
- `poc`
- `epoch-transactions`
- `multi-phase`

---

## Testing Strategy

### Unit Tests (Each Phase)
- Fast (< 1s per test)
- Isolated components
- Mock dependencies

### Integration Tests (Phase 3+)
- Real DbContext
- Transaction verification
- Multiple epochs

### E2E Tests (Phase 4)
- Complete pipeline
- Full transaction semantics
- Performance benchmarks

### Performance Targets
- Serialization: < 100μs overhead
- Transaction begin/commit: < 10ms
- Pipeline: > 1000 items/sec

---

## Success Criteria

### Phase 1
- [ ] Zero race conditions in 100+ concurrent operations
- [ ] Serialization overhead < 100μs

### Phase 2
- [ ] Hooks execute at correct lifecycle points
- [ ] Errors handled gracefully

### Phase 3
- [ ] All DB changes commit atomically per epoch
- [ ] Failures trigger complete rollback

### Phase 4
- [ ] Complete order pipeline works end-to-end
- [ ] All-or-nothing transaction semantics

### Phase 5
- [ ] Documentation clear and copy-paste ready
- [ ] Migration guide from sequential pattern

---

## Risk Mitigation

### Serialization Bottleneck
- Performance tests in Phase 1
- Document when NOT to use
- Provide read-only escape hatches

### Hook Complexity
- Simple interface
- Clear error handling
- Comprehensive tests

### Transaction Deadlocks
- Document scope rules
- Timeout configuration
- Deadlock detection tests

---

## References

- [Original Issue #125](https://github.com/uniun-technology/lib-dataflow/issues/125)
- [ADR: Epoch Transaction De-Scope](../../../docs/adr/poc/2025-11-15-epoch-transaction-descope.md)
- [Research: Epoch Transaction Coordination](../README.md)
- [Issue #429: Epoch-Scoped DI Services](https://github.com/uniun-technology/lib-dataflow/issues/429)

---

## Questions?

For questions about the design or implementation plan, see:
- Design document for technical details
- Implementation plan for task breakdown
- Issue templates for specific phase guidance
