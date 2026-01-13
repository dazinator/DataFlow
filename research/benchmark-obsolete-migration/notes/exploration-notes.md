# Exploration Notes: Benchmark Migration

## Current State Analysis

### Obsolete BuildDataFlow Method

Located in `poc/DataFlow.Benchmarks/SimpleEtlPOC.cs`:

```csharp
[Obsolete("This method uses removed EpochSegmenterBlock. Use ConfigureEpochs() for graph-level epoch configuration.")]
public static DataFlowGraph BuildDataFlow(
    IServiceProvider serviceProvider,
    int recordCount,
    int maxConcurrency = 4)
{
    throw new NotSupportedException(
        "BuildDataFlow is obsolete. EpochSegmenterBlock has been removed. " +
        "Use graph-level ConfigureEpochs() API for epoch segmentation.");
}
```

**Problems:**
1. Method throws `NotSupportedException`
2. Referenced `EpochSegmenterBlock` has been removed
3. Used direct block instantiation without modern DI patterns
4. Hardcoded service providers for validators, enrichers, and collectors

### Affected Benchmarks

1. **PythonComparativeBenchmark.cs** (line 148)
   - Calls `SimpleEtlPOC.BuildDataFlow(serviceProvider, config.RecordCount, config.MaxConcurrency)`
   - Used for comparative analysis with Python implementations
   
2. **SimpleComparisonBenchmark.cs** (line 157)
   - Calls `SimpleEtlPOC.BuildDataFlow(serviceProvider, recordCount, maxConcurrency)`
   - Compares POC vs Non-POC implementations
   
3. **DirectComparisonBenchmark.cs** (line 183)
   - Calls `SimpleEtlPOC.BuildDataFlow(serviceProvider, recordCount, maxConcurrency)`
   - Direct execution for profiling with dotnet-counters

## Modern Pattern Analysis

### From RevisedDiRegistrationTests.cs

**Key Patterns:**

1. **Service Registration:**
```csharp
services.AddDataFlows("global", df =>
{
    df.AddScopedBlock("producer", sp => BlockHelpers.CreateProducer(...));
    df.AddScopedBlock("transformer", sp => BlockHelpers.CreateActor<...>(...));
    df.AddGraph("graph-name", g =>
    {
        g.UseBlock("producer")
         .UseBlock("transformer")
         .Connect("producer", "transformer");
    });
});
```

2. **Graph Builder Creation:**
```csharp
var builder = GraphHelpers.CreateGraphBuilder("test", serviceProvider);
builder.UseBlock("producer")
    .UseBlock("transformer")
    .Connect("producer", "transformer");
var graph = builder.Build();
```

3. **Graph Execution:**
```csharp
var graph = serviceProvider.GetRequiredKeyedService<DataFlowGraph>("global:graph-name");
using var scope = serviceProvider.CreateScope();
var context = new ExecutionContext(scope.ServiceProvider, CancellationToken.None);
await graph.ExecuteAsync(context);
```

### From BlockLifetimeAndGraphReuseTests.cs

**Actor Registration Patterns:**

```csharp
services.AddScoped<InstanceTrackingActor>(); // Register actor in main container

services.AddDataFlows("global", df =>
{
    df.AddScopedBlock("shared-transformer", sp =>
    {
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return BlockHelpers.CreateActor<int, string, InstanceTrackingActor>(
            "shared-transformer", scopeFactory);
    });
});
```

**Or using AddActorBlock:**
```csharp
services.AddDataFlows("global", df =>
{
    df.AddActorBlock<int, string, TransformActor<int, string>>("transformer");
});
```

## Migration Strategy

### Option 1: Full DI Registration (Ideal for Reusability)

**Pros:**
- Most aligned with modern patterns
- Reusable across multiple benchmark runs
- Follows BlockLifetimeAndGraphReuseTests pattern

**Cons:**
- More setup code
- May be overkill for one-time benchmark execution

### Option 2: GraphHelpers.CreateGraphBuilder Pattern (Recommended)

**Pros:**
- Simpler than full DI registration
- Still uses ServiceProvider for actor scoping
- Follows RevisedDiRegistrationTests pattern
- Minimal changes to benchmark structure

**Cons:**
- Less reusable (builds graph each time)
- Still requires some DI setup for actors

### Option 3: Hybrid Approach

**Pros:**
- Balances simplicity and modern patterns
- Use GraphHelpers for builder creation
- Use minimal DI for actor scope factories
- Good for benchmarks that run multiple times

**Cons:**
- Slightly more complex than Option 2

## Recommended Approach: Option 2 (GraphHelpers Pattern)

### Implementation Plan

1. **Update SimpleEtlPOC.BuildDataFlow:**
   - Remove `[Obsolete]` attribute
   - Remove `throw NotSupportedException`
   - Implement using `GraphHelpers.CreateGraphBuilder()`
   - Use `BlockHelpers.CreateProducer()` and `BlockHelpers.CreateActor()`
   - Remove EpochSegmenterBlock references

2. **Actor Registration:**
   - Register actors in a minimal ServiceCollection
   - Get `IServiceScopeFactory` for actor blocks
   - Use `BlockHelpers.CreateActor<TIn, TOut, TActor>(name, scopeFactory)`

3. **Graph Building:**
   - Use `GraphHelpers.CreateGraphBuilder("SimpleEtlBenchmark-POC", serviceProvider)`
   - Add blocks using builder
   - Connect blocks
   - Return `builder.Build()`

### Key Changes to SimpleEtlPOC

**Before:**
```csharp
var segmenter = new EpochSegmenterBlock<RawRecord>(...);
var validators = new List<IBlock>();
for (int i = 0; i < maxConcurrency; i++)
{
    validators.Add(new EpochActorBlock<...>(...));
}
```

**After:**
```csharp
// No segmenter needed - producers already create epoch streams
var producer = BlockHelpers.CreateProducer("data-source", 
    ctx => ProduceRawRecords(recordCount, ctx.CancellationToken));

var validatorScopeFactory = GetValidatorScopeFactory();
var validators = new List<IBlock>();
for (int i = 0; i < maxConcurrency; i++)
{
    validators.Add(BlockHelpers.CreateActor<RawRecord, ValidatedRecord, ValidatorActor>(
        $"validator-{i}", validatorScopeFactory));
}
```

## Next Steps

1. Implement prototype of new BuildDataFlow method
2. Test with one benchmark (PythonComparativeBenchmark)
3. Validate compilation and execution
4. Update remaining benchmarks
5. Run all benchmarks to verify functionality
