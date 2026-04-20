# Exploration Notes: Delta-rs Tables

## Repository Baseline Validation

- `dotnet build` from repo root fails (no solution in root) — expected structure nuance.
- `src/DataFlow.sln` baseline is currently broken before this research change (missing `src/DataFlow/DataFlow.csproj`).
- `poc/DataFlow.sln` builds and executes tests successfully, but command returned non-zero in tool wrapper despite all test projects reporting pass in output.

## POC Patterns Reviewed

- `poc/DataFlow/Blocks/EpochSourceBlock.cs`
  - Confirms source blocks can run continuously and emit asynchronously using cancellation-aware enumeration.
- `poc/DataFlow/Blocks/EpochActorBlock.cs`
  - Confirms DI scope rotation pattern for long-running workloads and safe scoped dependency usage.
- `poc/docs/guides/source-blocks.md`
  - Confirms continuous polling source pattern already documented and idiomatic in this codebase.

## External Capability Notes

- delta-rs README indicates Azure Blob + Azure ADLS Gen2 support in cloud integrations matrix.
- delta-rs README protocol section indicates Change Data Feed marked supported at writer protocol version 4.
- delta-dotnet README confirms it is a C# wrapper around delta-rs and includes an Azure ABFSS end-to-end sample.

## Decision Direction

- Prefer **table-per-tenant** in production for isolation and independent throughput scaling.
- Use **long-running poller per active tenant partition set** with lease-based sharding across workers.
- Use CDF when .NET API surface exposes required semantics; otherwise fallback to version-based incremental reads.
