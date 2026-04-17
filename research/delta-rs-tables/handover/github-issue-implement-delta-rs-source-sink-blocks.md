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
