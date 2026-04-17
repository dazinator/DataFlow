# Design: Delta-rs Source/Sink Architecture for DataFlow POC

## 1. Goals

- Use Delta Lake tables as durable ingress/egress boundaries between independent DataFlows.
- Preserve DataFlow pull-based/backpressure model.
- Support multi-tenant isolation and horizontal scale on Azure.

## 2. Integration Options

### Option A (Recommended): `DeltaLake.Net` (delta-dotnet)

- Direct .NET wrapper around delta-rs and delta-kernel bridge.
- Strong alignment with requested delta-rs direction.
- Azure ABFSS usage demonstrated in project README.

### Option B (Alternative): Flowtide Delta connector

- Useful reference for Delta integration patterns.
- Less directly aligned to “delta-rs from DataFlow POC” requirement.

## 3. Proposed Block Model

### Source: `DeltaTableSourceActor<T>`

Responsibilities:

1. Track per-tenant source cursor (`tableUri`, last consumed version, watermark timestamp).
2. Poll table on interval.
3. Read incremental changes:
   - Preferred: CDF window (fromVersion -> toVersion)
   - Fallback: append files/actions between versions
4. Emit items as epoch stream to downstream blocks.

### Sink: `DeltaTableSinkActor<T>`

Responsibilities:

1. Buffer/batch records from stream.
2. Write append transactions to tenant table.
3. Emit commit metadata (table version, row count, commit id) for observability.

### Supporting Components

- `IDeltaTableClientFactory` (per-tenant client creation)
- `IDeltaTenantRoutingStrategy` (resolve table URI from tenant)
- `IDeltaCursorStore` (checkpoint/cursor persistence)
- `IDeltaLeaseStore` (worker ownership for tenant/table partitions)

### Relationship to Existing DataFlow Checkpointing

This intentionally overlaps with existing DataFlow checkpoint/recovery concepts and should **reuse** them rather than introduce a parallel state model.

- `IDeltaCursorStore` is intended to persist Delta source position (version/watermark) as block state at epoch checkpoint boundaries.
- The existing `ICheckpoint`/`ICheckpointStrategy` mechanism remains the source of truth for when checkpoints are taken.
- Existing EF Core checkpoint persistence patterns can back `IDeltaCursorStore` (directly or via an adapter), so Delta cursor state and other block checkpoint state commit together.
- Recommended implementation direction: treat Delta cursor state as a checkpoint payload concern, not a separate independent persistence pipeline.

## 4. Multi-Tenant Topologies

### Topology 1: Table-per-tenant (Recommended)

Pros:
- Strong isolation (throughput, retention policies, schema evolution blast radius)
- Easier tenant-level data governance and deletion workflows
- Simplifies noisy-neighbor containment

Cons:
- More tables to manage
- More metadata/list operations

### Topology 2: Shared table with `TenantId`

Pros:
- Fewer tables and simpler infra footprint
- Easier global analytics

Cons:
- Weaker noisy-neighbor isolation
- Requires strict partitioning and compaction discipline
- More contention risk under skewed tenant traffic

## 5. Scheduler/Execution Model

### Model A: Long-running hosted source workers (Recommended)

- Keep one durable scheduler/poller plane.
- Shard tenants across workers with lease table.
- Each worker hosts one or more long-running DataFlow source pipelines.
- Natural fit for incremental polling and backpressure.

### Model B: External scheduler dispatching short-lived runs

- Operationally harder: dedupe, overlap control, completion detection, idempotency.
- Better for sparse/batch workloads, but higher coordination complexity.

## 6. Object Storage Strategy

### Local Development

- Local filesystem path-backed Delta tables for deterministic tests.
- Optional Azure emulator path (Azurite) for storage auth/config testing.

### Azure Production

- ADLS Gen2 (ABFSS) as primary target.
- Azure Blob also viable; ADLS Gen2 preferred for hierarchical namespace and lake workloads.

## 7. Change Data Feed (CDF) Guidance

- delta-rs protocol support indicates CDF availability.
- .NET wrapper capability should be validated with a focused spike in POC.
- Implementation must include fallback path when CDF is unavailable for a table or API surface.

## 8. Scale-Out Pattern

- Store ownership leases per tenant/table partition.
- Use optimistic lease renewal with expiration.
- Keep cursor checkpoint external and atomic with successful downstream commit where feasible.
- Enforce single-writer rule per tenant table sink pipeline (or per partition) to reduce conflict risk.

### Practical implementation with current application stack

- **Lease state store:** Azure SQL table (for example: `TenantLease(LeaseKey, OwnerId, ExpiresUtc, Epoch, RowVersion)`).
- **Acquire/renew pattern:** optimistic update (compare `RowVersion` and `ExpiresUtc`) plus periodic renewal heartbeat.
- **Distributed coordination:** prefer existing SQL-based tooling first (`DistributedLock` with SQL Server provider package) for coarse coordination, with lease table as source of truth.
- **Checkpoint integration:** persist cursor/version in existing DataFlow checkpoint payload at epoch boundaries, using existing EF Core-backed checkpoint persistence.
- **No Redis requirement:** this model stays within currently available platform primitives (Azure SQL + existing lock package + checkpoint infrastructure).

## 9. Risks and Mitigations

1. **Library maturity/API gaps in .NET**
   - Mitigation: adapter abstraction + compatibility test suite.
2. **Tenant skew creating hot partitions**
   - Mitigation: dynamic lease rebalancing and per-tenant throughput limits.
3. **Duplicate processing on restarts**
   - Mitigation: idempotent sink writes + cursor checkpointing after commit.
4. **CDF incompatibilities across table versions/configs**
   - Mitigation: runtime feature detection and version-diff fallback mode.
