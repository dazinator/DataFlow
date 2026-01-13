# Prototype Code

This directory contains the working prototype code from research validation.

## Purpose

This prototype demonstrates the migration pattern for upgrading benchmarks from obsolete `BuildDataFlow` method to modern DI patterns using `BlockHelpers` and `GraphHelpers`.

## Key Files

### SimpleEtlPOC.cs

**What it demonstrates:**
- Modern `BuildDataFlow` implementation
- Usage of `GraphHelpers.CreateGraphBuilder()`
- Usage of `BlockHelpers.CreateProducer()` for sources
- Usage of `BlockHelpers.CreateActor()` for transformers
- Direct block connections (no buffer blocks needed)
- Automatic epoch management via BlockHelpers

**Key changes from obsolete version:**
- ❌ Removed `[Obsolete]` attribute
- ❌ Removed `throw NotSupportedException`
- ❌ Removed `EpochSegmenterBlock` usage
- ❌ Removed `EpochBufferBlock` instances
- ✅ Added `GraphHelpers.CreateGraphBuilder()` pattern
- ✅ Added `BlockHelpers` for all blocks
- ✅ Simplified topology (direct connections)

## Topology

**Data Flow:**
```
ProduceRawRecords
    ↓
DataSource (Producer)
    ↓ (broadcast to N validators)
Validators (Actors × maxConcurrency)
    ↓ (broadcast to M enrichers)
Enrichers (Actors × maxConcurrency)
    ↓ (converge to collector)
Collector (Actor)
```

**Concurrency Model:**
- Multiple validator instances process concurrently
- Multiple enricher instances process concurrently
- Broadcast connections enable fan-out
- Convergence at collector

## Validation Results

**Test Configuration:**
- Records: 100
- Concurrency: 1
- Iterations: 1

**Performance:**
- Non-POC: 287 ms (348 rec/sec)
- POC: 178 ms (561 rec/sec)
- **Improvement: 38% faster**

**Status:** ✅ Validated - builds and runs successfully

## How to Use

This prototype code was already integrated into the actual `SimpleEtlPOC.cs` file in `poc/DataFlow.Benchmarks/`. The implementation team should:

1. Review this prototype for understanding
2. Test the integrated version with larger datasets
3. Apply similar pattern to other benchmarks if needed

## Pattern Summary

```csharp
public static DataFlowGraph BuildDataFlow(
    IServiceProvider serviceProvider,
    int recordCount,
    int maxConcurrency = 4)
{
    // 1. Create builder
    var builder = GraphHelpers.CreateGraphBuilder("name", serviceProvider);

    // 2. Create source
    var source = BlockHelpers.CreateProducer("source", ctx => ProduceData(...));

    // 3. Create actors with scope factories
    var services = new ServiceCollection();
    services.AddScoped<MyActor>();
    var scopeFactory = services.BuildServiceProvider()
        .GetRequiredService<IServiceScopeFactory>();
    
    var actors = new List<IBlock>();
    for (int i = 0; i < maxConcurrency; i++)
    {
        actors.Add(BlockHelpers.CreateActor<TIn, TOut, MyActor>(
            $"actor-{i}", scopeFactory));
    }

    // 4. Add blocks and connect
    builder.AddBlock(source);
    foreach (var actor in actors)
    {
        builder.AddBlock(actor);
        builder.Connect(source, actor);
    }

    // 5. Build
    return builder.Build();
}
```

## Dependencies

**Project References:**
- `poc/DataFlow.Tests` - Required for `BlockHelpers` and `GraphHelpers`

**Using Statements:**
```csharp
using DataFlow.POC.Tests.TestHelpers;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
```

## Related Documentation

- **Research Findings**: `/research/benchmark-obsolete-migration/README.md`
- **Implementation Guide**: `/research/benchmark-obsolete-migration/handover/README.md`
- **Validation Results**: `/research/benchmark-obsolete-migration/notes/validation-results.md`
