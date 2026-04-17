# Prototype Notes

This research used a **documentation-level architecture prototype** rather than committed executable exploratory code.

## Why

- The issue scope was feasibility and architecture validation for a new integration boundary.
- The key risks are library capability coverage and tenancy/scheduling topology, which were validated through interface design and workflow modeling.

## Captured Prototype Artifacts

- Proposed block and adapter contracts are documented in:
  - `/research/delta-rs-tables/design/delta-rs-source-sink-architecture.md`
- Implementation-ready task breakdown is documented in:
  - `/research/delta-rs-tables/handover/github-issue-implement-delta-rs-source-sink-blocks.md`

## Implementation Spike Required

First implementation phase should include a runnable spike project in `poc/` to validate:

1. Delta append/read against local table path
2. Incremental read strategy (CDF or version-diff fallback)
3. ADLS Gen2 connection and auth path
4. Lease ownership behavior for scale-out workers (acquire/renew/expire/reacquire)
5. Checkpoint + epoch transaction boundary behavior (cursor saved only after successful epoch completion)
6. Candidate locking technology fit using existing stack (Azure SQL + `Medallion.Threading.Sql`, no Redis dependency)
