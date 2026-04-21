# Delta source/sink feasibility spike (POC)

This spike provides a runnable local Delta table adapter and demo flow for:

- append/write behavior,
- incremental read across restart,
- CDF-first with version-diff fallback,
- optional ABFSS connectivity/auth probe.

## Run

```bash
cd poc
dotnet run --project DataFlow.DeltaSpike/DataFlow.DeltaSpike.csproj -- --table-path /tmp/dataflow-delta-spike/manual-table
```

Optional ABFSS probe:

```bash
cd poc
dotnet run --project DataFlow.DeltaSpike/DataFlow.DeltaSpike.csproj -- \
  --table-path /tmp/dataflow-delta-spike/manual-table-abfss \
  --abfss-uri abfss://container@storageaccount.dfs.core.windows.net/a/b/demo-table
```

If you have a valid bearer token, pass it directly or set `DELTA_SPIKE_BEARER_TOKEN`.

## Observed local output

```text
Delta spike table: /tmp/dataflow-delta-spike/manual-table
Commit 1 -> version 1, rows 2
Initial read strategy: VersionDiffFallback
Initial read rows
  id=1, payload=first-run-a, commitVersion=1
  id=2, payload=first-run-b, commitVersion=1
Commit 2 -> version 2, rows 2
Incremental read strategy: VersionDiffFallback
Incremental read diagnostic: CDF unavailable in current .NET API surface, fallback used: DeltaLake.Net 0.31.1 does not expose a dedicated CDF read API.
Incremental rows after restart
  id=3, payload=second-run-c, commitVersion=2
  id=4, payload=second-run-d, commitVersion=2
```

## Implementation notes

- `IDeltaTableClient` and `DeltaLakeNetTableClient` are implemented in `Delta/`.
- Each append stamps rows with a `commit_version` value so version-diff reads can safely return only newly committed rows.
- CDF is checked first when enabled (`delta.enableChangeDataFeed=true`), then falls back to version-diff because current `DeltaLake.Net` API does not expose a direct CDF reader.
