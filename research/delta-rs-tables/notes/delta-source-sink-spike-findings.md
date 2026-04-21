# Delta source/sink spike findings (Issue #218)

## What was implemented

- Added runnable spike project: `poc/DataFlow.DeltaSpike/`
- Added minimal adapter:
  - `IDeltaTableClient`
  - `DeltaLakeNetTableClient` (local filesystem + optional storage options)
- Added test coverage in `poc/DataFlow.Tests/DeltaSpikeTableClientTests.cs`

## Measured behavior

### Local append + incremental read across restart

Command:

```bash
cd poc
dotnet run --project DataFlow.DeltaSpike/DataFlow.DeltaSpike.csproj -- --table-path /tmp/dataflow-delta-spike/manual-table
```

Observed:

- Commit 1 created table version `1` with 2 rows.
- Restarted client instance appended Commit 2 at version `2` with 2 rows.
- Incremental read from cursor `1` returned only commit-2 rows.

### CDF-first and fallback behavior

- Table is created with `delta.enableChangeDataFeed=true`.
- Adapter attempts CDF-first when configured.
- Current `DeltaLake.Net` API (0.31.1) does not expose a dedicated CDF reader in .NET.
- Adapter falls back to version-diff read and returns expected rows.

## ABFSS connectivity/auth result (containerized .NET runtime)

Command:

```bash
cd poc
dotnet run --project DataFlow.DeltaSpike/DataFlow.DeltaSpike.csproj -- \
  --table-path /tmp/dataflow-delta-spike/manual-table-abfss \
  --abfss-uri abfss://container@storageaccount.dfs.core.windows.net/a/b/demo-table
```

Observed result:

- Runtime accepted ABFSS URI and attempted Azure token acquisition.
- Failure was authentication/environment related (`DeltaRuntimeException` via token request to IMDS), not a missing Hadoop runtime failure.
- No extra Hadoop runtime dependency was required to execute the attempt from containerized .NET.

## Known API gaps captured

1. No direct .NET CDF read API currently exposed in `DeltaLake.Net` for ergonomic `fromVersion`/`toVersion` change streaming.
2. Fallback version-diff logic is currently required for incremental read semantics in this spike.
