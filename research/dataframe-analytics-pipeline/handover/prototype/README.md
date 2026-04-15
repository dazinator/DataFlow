# Prototype README

**Status**: Reference code — NOT compiled into the solution.  
**Location**: `/research/dataframe-analytics-pipeline/handover/prototype/`

---

## Contents

| File | Description |
|---|---|
| `DataFrameBlocks.cs` | Full reference implementations of all proposed DataFrame blocks |
| `DataFrameBlockTests.cs` | Unit tests demonstrating expected behaviour |

## How to Use

These files are guides for the implementation team, not working code. To validate
the prototype locally:

1. Create a new temporary console project or test project
2. Add the NuGet packages:
   - `Microsoft.Data.Analysis` 0.23.0
   - `Parquet.Net` 5.1.0
   - `Parquet.Net.Data.Analysis` 5.1.0
3. Add a project reference to `poc/DataFlow/DataFlow.csproj`
4. Copy these files into the project and resolve namespaces

## Key Design Decisions Illustrated

### Immutability convention (DataFrameFilterBlock)

```csharp
// .Filter() returns a NEW DataFrame — original is unchanged
yield return frame.Filter(_predicate(frame));
```

### Row-group streaming (ParquetSourceBlock)

```csharp
for (int i = 0; i < reader.RowGroupCount; i++)
{
    using var groupReader = reader.OpenRowGroupReader(i);
    yield return await groupReader.ReadAsDataFrameAsync();
    // Backpressure: bounded channel between source and downstream
    // pauses iteration until downstream consumes this chunk
}
```

### Partitioned fan-out (DataFramePartitionBlock)

```csharp
// Splits one DataFrame into N non-overlapping slices
// Use CompetingEdgeStrategy to route each slice to a different worker
// NO cloning needed — each slice is an independent DataFrame
```

### Clone-on-broadcast extension

```csharp
// When same DataFrame must go to multiple routes (use sparingly)
new BroadcastEdgeStrategy(
    cloneFunc: item => ((DataFrame)item).Clone(),
    BufferMode.Bounded, 100)
```
