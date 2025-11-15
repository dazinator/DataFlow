# Implementation Plan: Epoch-Scoped Serialized Transaction Access

**Date**: 2025-11-15  
**Status**: Ready for Implementation  
**Parent Issue**: #125

---

## Overview

This document provides a structured implementation plan for adding serialized DbContext access patterns to epoch-scoped services, enabling transaction semantics across multiple blocks.

---

## Multi-Phase Implementation Plan

### Phase 1: Core Serialized Access Infrastructure

**Goal**: Add generic serialized access mechanism to epochs

**Tasks**:
1. Update `IEpoch` interface with `ExecuteSerializedAsync` methods
2. Implement in `Epoch` class using `SemaphoreSlim`
3. Add comprehensive unit tests
4. Update epoch documentation

**Files to Modify**:
- `poc/DataFlow.POC/Core/IEpoch.cs`
- `poc/DataFlow.POC/Core/Epoch.cs`
- `poc/DataFlow.POC.Tests/Core/EpochSerializedAccessTests.cs` (new)

**Test Coverage**:
- Serialization correctness (100 concurrent operations)
- Different service types can run concurrently
- Exception propagation
- Cancellation support
- Disposal handling

**Estimated Effort**: 2-3 days

**Success Criteria**:
- All unit tests pass
- Zero race conditions detected
- < 100μs overhead per operation

---

### Phase 2: Lifecycle Hooks Infrastructure

**Goal**: Add epoch lifecycle hook support

**Tasks**:
1. Design `IEpochLifecycleHooks` interface
2. Add hook registration to `EpochCoordinator`
3. Add configuration API for hooks
4. Integrate hook calls at epoch creation/completion/failure
5. Add tests for hook execution order and error handling

**Files to Modify**:
- `poc/DataFlow.POC/Core/IEpochLifecycleHooks.cs` (new)
- `poc/DataFlow.POC/Core/EpochCoordinator.cs`
- `poc/DataFlow.POC/Core/EpochConfiguration.cs` (new)
- `poc/DataFlow.POC.Tests/Core/EpochLifecycleHooksTests.cs` (new)

**Test Coverage**:
- Hook execution on epoch created
- Hook execution on epoch completed
- Hook execution on epoch failed
- Multiple hooks registered
- Hook error handling

**Estimated Effort**: 3-4 days

**Success Criteria**:
- Hooks execute at correct lifecycle points
- Errors in hooks handled gracefully
- Configuration API is intuitive

---

### Phase 3: DbContext Transaction Hooks

**Goal**: Implement DbContext-specific transaction lifecycle

**Tasks**:
1. Create `DbContextTransactionHooks<T>` implementation
2. Add transaction begin/commit/rollback logic
3. Integration tests with actual DbContext
4. Error scenario testing (rollback verification)

**Files to Create**:
- `poc/EpochAnchoringDemo/Core/DbContextTransactionHooks.cs`
- `poc/EpochAnchoringDemo.Tests/DbContextTransactionLifecycleTests.cs`

**Test Coverage**:
- Transaction starts on epoch creation
- Transaction commits on epoch completion
- Transaction rollbacks on epoch failure
- Interim SaveChanges within transaction
- Multiple epochs have independent transactions

**Estimated Effort**: 2-3 days

**Success Criteria**:
- All database changes commit atomically per epoch
- Failures trigger complete rollback
- No orphaned transactions

---

### Phase 4: Multi-Block Example with Interim Operations

**Goal**: Create complete end-to-end example

**Tasks**:
1. Create order processing pipeline with 3+ blocks
2. Demonstrate serialized DbContext access across blocks
3. Show interim SaveChanges (ID generation)
4. Full transaction lifecycle (begin→interim saves→commit)
5. End-to-end tests

**Files to Create**:
- `poc/EpochAnchoringDemo/Blocks/OrderCreationBlock.cs`
- `poc/EpochAnchoringDemo/Blocks/OrderLineCreationBlock.cs`
- `poc/EpochAnchoringDemo/Blocks/OrderTotalCalculationBlock.cs`
- `poc/EpochAnchoringDemo/OrderProcessingPipeline.cs`
- `poc/EpochAnchoringDemo.Tests/OrderProcessingEndToEndTests.cs`

**Test Coverage**:
- Complete pipeline execution
- Order+lines+totals all committed
- Failure in any block rolls back entire epoch
- Different epochs process independently
- Change visibility across blocks

**Estimated Effort**: 4-5 days

**Success Criteria**:
- Pipeline processes orders correctly
- All-or-nothing transaction semantics
- Clear demonstration of pattern benefits

---

### Phase 5: Documentation and Examples

**Goal**: Complete user-facing documentation

**Tasks**:
1. Update POC README with serialized access pattern
2. Create user guide for epoch transactions
3. Document best practices and patterns
4. Add code examples to documentation
5. Update architecture documentation

**Files to Modify**:
- `poc/EpochAnchoringDemo/README.md`
- `poc/docs/guides/epoch-transactions.md` (new)
- `docs/design/epoch-scoped-services/patterns.md` (new)

**Estimated Effort**: 2-3 days

**Success Criteria**:
- Clear explanation of when to use pattern
- Code examples are copy-paste ready
- Trade-offs documented
- Migration guide from sequential pattern

---

## Issue Breakdown

### Option A: Single Tracking Issue

Create one tracking issue that covers all phases with checklist:

```markdown
## Implement Epoch-Scoped Serialized Transaction Access

### Phase 1: Core Infrastructure
- [ ] Add ExecuteSerializedAsync to IEpoch
- [ ] Implement in Epoch class
- [ ] Unit tests for serialization
- [ ] Documentation updates

### Phase 2: Lifecycle Hooks
- [ ] Design IEpochLifecycleHooks
- [ ] Integrate into EpochCoordinator
- [ ] Configuration API
- [ ] Tests for hooks

### Phase 3: DbContext Transactions
- [ ] DbContextTransactionHooks implementation
- [ ] Integration tests
- [ ] Rollback scenarios

### Phase 4: Multi-Block Example
- [ ] Order processing blocks
- [ ] End-to-end tests
- [ ] Interim operations demo

### Phase 5: Documentation
- [ ] User guide
- [ ] Best practices
- [ ] Migration guide
```

### Option B: Multi-Issue Plan (Recommended)

Create separate issues for each phase with dependencies:

1. **Issue**: "Phase 1: Add Serialized Access to Epoch Infrastructure" (no dependencies)
2. **Issue**: "Phase 2: Implement Epoch Lifecycle Hooks" (depends on Phase 1)
3. **Issue**: "Phase 3: DbContext Transaction Lifecycle Hooks" (depends on Phase 2)
4. **Issue**: "Phase 4: Complete Order Processing Example" (depends on Phase 3)
5. **Issue**: "Phase 5: Documentation for Epoch Transactions" (depends on Phase 4)

---

## Recommended Approach

**Use Option B (Multi-Issue)**:
- Easier to track progress per phase
- Can be assigned to different developers
- Clearer PR scope
- Better for iterative development

**Create parent epic issue** that links to all phase issues for tracking.

---

## Implementation Order Justification

1. **Phase 1 first**: Core mechanism needed for everything else
2. **Phase 2 next**: Hooks infrastructure enables transaction lifecycle
3. **Phase 3 uses Phase 2**: DbContext hooks implement lifecycle interface
4. **Phase 4 uses all**: Example demonstrates complete pattern
5. **Phase 5 last**: Documentation after implementation proven

---

## Testing Strategy

### Unit Tests (Per Phase)
- Test individual components in isolation
- Mock dependencies where appropriate
- Fast execution (< 1s per test)

### Integration Tests (Phase 3+)
- Real DbContext with in-memory database
- Transaction lifecycle verification
- Multiple epochs

### End-to-End Tests (Phase 4)
- Complete pipeline execution
- Full transaction semantics
- Performance benchmarks

### Performance Targets
- Serialization overhead: < 100μs per operation
- Transaction begin/commit: < 10ms
- Pipeline throughput: > 1000 items/sec (simple operations)

---

## Risk Mitigation

### Risk: Serialization Bottleneck
**Mitigation**: 
- Performance tests in Phase 1
- Document when NOT to use pattern
- Provide escape hatches (direct access for read-only)

### Risk: Hook Complexity
**Mitigation**:
- Simple interface design
- Clear error handling
- Comprehensive tests

### Risk: Transaction Deadlocks
**Mitigation**:
- Document transaction scope rules
- Timeout configuration
- Deadlock detection tests

---

## Next Steps

1. **Review this plan** with maintainers
2. **Create issues** (recommend Option B)
3. **Start with Phase 1** implementation
4. **Review after each phase** before proceeding

---

## References

- [Design Document](../design/serialized-dbcontext-access.md)
- [ADR: Epoch Transaction De-Scope](../../../docs/adr/poc/2025-11-15-epoch-transaction-descope.md)
- Issue #125 - Parent investigation
- Issue #429 - Epoch-scoped DI foundation
