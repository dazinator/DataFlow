# [Implementation] Delta-rs Table Source/Sink Blocks for DataFlow POC

## Context and Objectives

### Problem Statement

We need to decouple DataFlow pipelines with a durable intermediate store while preserving stream-like behavior. Delta Lake tables are a candidate for:

- Intake ledger style sources
- Persistent sinks that can feed downstream DataFlows

This implementation should establish a production-ready POC integration for multi-tenant Azure workloads.

### Research Background

Research documentation: `/research/delta-rs-tables/`

Key artifacts:
- Findings: `/research/delta-rs-tables/README.md`
- Design: `/research/delta-rs-tables/design/delta-rs-source-sink-architecture.md`
- Notes: `/research/delta-rs-tables/notes/exploration-notes.md`

Key findings:
1. `DeltaLake.Net` is the most direct .NET path aligned to delta-rs.
2. POC architecture supports continuous source polling and scoped actor execution patterns needed for this integration.
3. ADLS Gen2/ABFSS is suitable for Azure production; local filesystem tables are suitable for local dev tests.
   - Expected runtime model is plain containerized .NET (no Hadoop cluster dependency).
   - Explicitly validate this assumption in Spike 1.
4. Table-per-tenant is the recommended baseline for noisy-neighbor isolation.
5. CDF should be used when available, but implementation requires a version-diff fallback mode.

### Objectives

- [ ] Add Delta integration abstraction layer in POC
- [ ] Implement Delta source actor with checkpointed incremental consumption
- [ ] Implement Delta sink actor for append writes
- [ ] Add tenant routing and cursor persistence abstractions
- [ ] Add lease-based worker ownership for scale-out scheduling
- [ ] Validate local + ADLS Gen2 connectivity paths
- [ ] Add integration tests for incremental read and replay safety

## Implementation Guidance

### Recommended Approach

Implement a small adapter layer and keep DataFlow blocks independent of raw vendor API usage.

Core interfaces:

```csharp
public interface IDeltaTableClient
{
    Task<long> GetLatestVersionAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<T> ReadChangesAsync<T>(long fromExclusiveVersion, long toInclusiveVersion, CancellationToken cancellationToken);
    Task<DeltaCommitResult> AppendAsync<T>(IReadOnlyCollection<T> items, CancellationToken cancellationToken);
}

public interface IDeltaCursorStore
{
    Task<long?> GetLastConsumedVersionAsync(string tenantId, string sourceId, CancellationToken cancellationToken);
    Task SaveLastConsumedVersionAsync(string tenantId, string sourceId, long version, CancellationToken cancellationToken);
}
```

### Suggested Phases

#### Phase 1: Foundation

- Add adapter interfaces + `DeltaLakeNetClient` implementation
- Add tenant routing (`tenantId -> tableUri`)
- Add basic local filesystem integration test

#### Phase 2: Source and Sink Blocks

- Implement `DeltaTableSourceActor<T>` and `DeltaTableSinkActor<T>`
- Add cursor checkpointing and cancellation-safe polling loop
- Add idempotency handling for restart replay window

#### Phase 3: Scale-Out Scheduler and Azure Validation

- Add lease-store based tenant sharding across workers
- Add ADLS Gen2 integration test plan (environment-gated)
- Add observability metrics/events for lag and commit throughput

## Testing and Validation

### Unit Tests

1. Source actor emits only unseen versions after checkpoint
2. Source actor handles empty polls without busy-looping
3. Sink actor appends batch and reports commit metadata
4. Cursor store updates only after successful downstream handling
5. Lease store prevents dual ownership for same tenant shard

### Integration Tests

1. End-to-end local flow: Delta source -> transform -> Delta sink
2. Restart recovery: no lost events, bounded duplicates
3. Multi-tenant parallel processing: independent cursors and isolation
4. Fallback behavior when CDF is not available

### Edge Cases

- Table not yet created
- Schema evolution between versions
- Transient storage auth/network failures
- High-volume tenant causing lag while others continue processing

## Constraints and Requirements

- Use pull-based execution semantics already present in DataFlow POC
- Preserve cancellation support in all long-running loops
- Avoid hard-coupling blocks to a single storage/auth configuration model
- Keep default topology as table-per-tenant unless explicit shared-table design is approved

## Workflow Labels

Apply these labels to the **new implementation work item created from this handover** (not to this research PR):

- Duty: `workflow:implementation`
- Type: Feature

## Recommended Next-Phase GitHub Issues

Create the following issues in order, with the listed duty label per issue.

Created from this handover:
- #218 `[Spike] Delta source/sink feasibility in POC`
- #219 `[Spike] SQL-backed lease ownership for tenant/table shards`
- #220 `[ADR] Lease + checkpoint transaction boundary decision`
- #221 `[Implementation] DeltaTableSourceActor/DeltaTableSinkActor with checkpoint + lease integration`

### Issue 1 — `[Spike] Delta source/sink feasibility in POC`

**Duty label**
- `workflow:research`

**Goal**
- Build a runnable `poc/` spike that proves local Delta read/write and incremental read behavior.

**Scope**
- Implement minimal `IDeltaTableClient` adapter with local filesystem table.
- Validate CDF-first read and version-diff fallback path.
- Validate ABFSS access/auth from a containerized .NET runtime and confirm no Hadoop runtime dependency is required.
- Capture measured behavior and known API gaps.

**Definition of done**
- Runnable spike committed under `poc/`.
- Test/demo output showing append + incremental read across restarts.
- ABFSS connectivity/auth result documented, including whether any extra runtime dependencies are required.
- Short findings summary added to `research/delta-rs-tables/notes/`.

### Issue 2 — `[Spike] SQL-backed lease ownership for tenant/table shards`

**Duty label**
- `workflow:research`

**Goal**
- Prove scale-out ownership with existing platform stack (Azure SQL + SQL lock tooling).

**Scope**
- Implement `IDeltaLeaseStore` prototype backed by Azure SQL lease table.
- Use optimistic lease renewal with expiration and takeover on timeout.
- Validate `DistributedLock` SQL Server provider role (coarse coordination only; lease table remains source of truth).

**Definition of done**
- Two-worker simulation showing single active owner per shard.
- Demonstrated lease expiry and safe ownership takeover.
- Documented failure modes and retry/backoff policy.

### Issue 3 — `[ADR] Lease + checkpoint transaction boundary decision`

**Duty label**
- `workflow:research`

**Goal**
- Record architecture decision for locking/leasing and checkpoint consistency model.

**Scope**
- Compare candidate options:
  - SQL lease table + optimistic row-version updates
  - SQL distributed lock only
  - Hybrid (SQL lock for acquisition + lease table for liveness/ownership)
- Define epoch boundary and checkpoint write rule for cursor durability.
- Include architecture diagram and operational sequence.

**Definition of done**
- ADR merged under `/research/delta-rs-tables/` location agreed by maintainers.
- Explicit selected approach, rejected alternatives, and rationale.

### Issue 4 — `[Implementation] DeltaTableSourceActor/DeltaTableSinkActor with checkpoint + lease integration`

**Duty label**
- `workflow:implementation`

**Goal**
- Deliver production-ready POC implementation using outcomes of spikes + ADR.

**Scope**
- Implement source/sink actors, tenant routing, cursor store adapter to existing checkpoint infrastructure, and lease-based shard scheduling.
- Add unit/integration coverage listed in this handover.

**Definition of done**
- All scoped tests passing.
- Local and Azure-targeted configuration paths documented.
- Observability hooks added for lag, lease churn, and commit throughput.
