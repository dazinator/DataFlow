# Migration Guide: Mandatory Epochs

**Version**: 2.0  
**Status**: Deprecated plain blocks, unified epoch-based architecture  
**Timeline**: Plain variants will be removed in v3.0 (Q1 2027)

---

## Overview

DataFlow is transitioning to a **unified epoch-based architecture**. All blocks now work with epoch streams by default. Plain (non-epoch) sources are automatically wrapped in single-epoch streams, eliminating the need for duplicate block implementations.

### Key Changes

- ✅ **Single Mental Model**: Everything is epoch-based
- ✅ **Code Reduction**: Eliminated ~120 lines of duplicated code
- ✅ **Performance**: <5% overhead (research validated: 4.08%)
- ✅ **Simplified API**: One block type instead of two variants

---

## Quick Migration

### Before (Plain Blocks - Deprecated)

```csharp
// Old way - using plain blocks
builder
    .AddBlock(new PlainSourceBlock<int, MySource>(
        new BlockContext("source"), scopeFactory))
    .AddBlock(new ActorBlock<int, string, MyActor>(
        new BlockContext("transform"), scopeFactory))
    .AddBlock(new BatchBlock<string>(
        new BlockContext("batcher"), maxBatchSize: 100));
```

### After (Unified Epoch-Based)

```csharp
// New way - automatic epoch wrapping
builder
    .AddBlock(new PlainSourceAdapter<int, MySource>(
        new BlockContext("source"), scopeFactory, "source"))
    .AddBlock(new EpochActorBlock<int, string, MyActor>(
        new BlockContext("transform"), scopeFactory))
    .AddBlock(new EpochBatchBlock<string>(
        new BlockContext("batcher"), maxBatchSize: 100));
```

---

## Migration Scenarios

### Scenario 1: Plain Source with IPlainSourceActor

**Before**:
```csharp
public class MyDataSource : IPlainSourceActor<int>
{
    public async IAsyncEnumerable<int> ProduceAsync(IActorExecutionContext context)
    {
        for (int i = 0; i < 100; i++)
            yield return i;
    }
}

// Usage
var source = new PlainSourceBlock<int, MyDataSource>(
    new BlockContext("source"), scopeFactory);
```

**After (Option 1: Use PlainSourceAdapter)**:
```csharp
// No changes to MyDataSource needed!
var source = new PlainSourceAdapter<int, MyDataSource>(
    new BlockContext("source"), scopeFactory, sourceName: "source");
```

**After (Option 2: Make Source Epoch-Aware)**:
```csharp
public class MyDataSource : ISourceActor<int>
{
    public async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(IActorExecutionContext context)
    {
        var items = ProduceItems();
        yield return items.WrapInSingleEpoch("source");
    }

    private async IAsyncEnumerable<int> ProduceItems()
    {
        for (int i = 0; i < 100; i++)
            yield return i;
    }
}

// Usage
var source = new EpochSourceBlock<int, MyDataSource>(
    new BlockContext("source"), scopeFactory);
```

---

### Scenario 2: Plain ActorBlock

**Before**:
```csharp
public class MyTransformer : IStreamActor<int, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<int> input, IActorExecutionContext context)
    {
        await foreach (var item in input)
            yield return item.ToString();
    }
}

var actor = new ActorBlock<int, string, MyTransformer>(
    new BlockContext("transform"), scopeFactory);
```

**After**:
```csharp
// No changes to MyTransformer needed!
var actor = new EpochActorBlock<int, string, MyTransformer>(
    new BlockContext("transform"), scopeFactory);

// EpochActorBlock automatically handles both single-epoch and multi-epoch inputs
```

---

### Scenario 3: Plain BatchBlock

**Before**:
```csharp
var batcher = new BatchBlock<int>(
    new BlockContext("batcher"), 
    maxBatchSize: 100, 
    windowPeriod: TimeSpan.FromSeconds(5));
```

**After**:
```csharp
var batcher = new EpochBatchBlock<int>(
    new BlockContext("batcher"), 
    maxBatchSize: 100, 
    windowPeriod: TimeSpan.FromSeconds(5));

// EpochBatchBlock automatically handles single-epoch and multi-epoch inputs
```

---

### Scenario 4: ProducerBlock

**Before**:
```csharp
var producer = new ProducerBlock<int>(
    new BlockContext("producer"),
    ctx => GetItems());

async IAsyncEnumerable<int> GetItems()
{
    for (int i = 0; i < 100; i++)
        yield return i;
}
```

**After (Option 1: Wrap in Extension Method)**:
```csharp
var producer = new ProducerBlock<IEpochStream<int>>(
    new BlockContext("producer"),
    ctx => GetItems().WrapInSingleEpoch("producer"));

async IAsyncEnumerable<int> GetItems()
{
    for (int i = 0; i < 100; i++)
        yield return i;
}
```

**After (Option 2: Create Epoch Source)**:
```csharp
public class MyProducer : ISourceActor<int>
{
    public async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(IActorExecutionContext context)
    {
        yield return GetItems().WrapInSingleEpoch("producer");
    }

    private async IAsyncEnumerable<int> GetItems()
    {
        for (int i = 0; i < 100; i++)
            yield return i;
    }
}

var producer = new EpochSourceBlock<int, MyProducer>(
    new BlockContext("producer"), scopeFactory);
```

---

## Extension Methods

### WrapInSingleEpoch

Wraps a plain `IAsyncEnumerable<T>` in a single epoch stream:

```csharp
IAsyncEnumerable<int> plainStream = GetData();
IAsyncEnumerable<IEpochStream<int>> epochStream = 
    plainStream.WrapInSingleEpoch("my-source");
```

**Parameters**:
- `source`: The plain stream to wrap
- `sourceName`: Name of the source (used in epoch vector)
- `cancellationToken`: Optional cancellation token

**Returns**: An epoch stream containing all items in a single epoch

---

### WrapInEpoch

Wraps a plain stream using an existing IEpoch instance (for coordinator scenarios):

```csharp
IAsyncEnumerable<int> plainStream = GetData();
IEpoch epoch = GetCurrentEpoch();
IAsyncEnumerable<IEpochStream<int>> epochStream = 
    plainStream.WrapInEpoch(epoch);
```

---

## PlainSourceAdapter

Adapter block for legacy plain sources:

```csharp
public class PlainSourceAdapter<T, TActor> : BlockBase<object, IEpochStream<T>>
    where TActor : IPlainSourceActor<T>
```

**Usage**:
```csharp
var adapter = new PlainSourceAdapter<int, MyPlainSource>(
    blockContext,
    scopeFactory,
    sourceName: "my-source");
```

**What it does**:
1. Resolves TActor from DI scope
2. Calls `ProduceAsync()` to get plain stream
3. Automatically wraps output in single epoch using `WrapInSingleEpoch()`
4. Returns `IAsyncEnumerable<IEpochStream<T>>`

---

## Complete Example

### Before: Mixed Plain and Epoch Blocks

```csharp
// Define plain source
public class NumberSource : IPlainSourceActor<int>
{
    public async IAsyncEnumerable<int> ProduceAsync(IActorExecutionContext context)
    {
        for (int i = 1; i <= 1000; i++)
            yield return i;
    }
}

// Define transformer
public class StringTransformer : IStreamActor<int, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<int> input, IActorExecutionContext context)
    {
        await foreach (var item in input)
            yield return $"Item-{item}";
    }
}

// Build graph with plain blocks
services.AddScoped<NumberSource>();
services.AddScoped<StringTransformer>();

var builder = new DataFlowGraphBuilder("my-flow", logger);
builder
    .AddBlock(new PlainSourceBlock<int, NumberSource>(
        new BlockContext("source"), scopeFactory))
    .AddBlock(new ActorBlock<int, string, StringTransformer>(
        new BlockContext("transform"), scopeFactory))
    .AddBlock(new BatchBlock<string>(
        new BlockContext("batcher"), 100))
    .Connect("source", "transform")
    .Connect("transform", "batcher");
```

### After: Unified Epoch-Based Architecture

```csharp
// Define plain source (NO CHANGES NEEDED!)
public class NumberSource : IPlainSourceActor<int>
{
    public async IAsyncEnumerable<int> ProduceAsync(IActorExecutionContext context)
    {
        for (int i = 1; i <= 1000; i++)
            yield return i;
    }
}

// Define transformer (NO CHANGES NEEDED!)
public class StringTransformer : IStreamActor<int, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<int> input, IActorExecutionContext context)
    {
        await foreach (var item in input)
            yield return $"Item-{item}";
    }
}

// Build graph with unified blocks
services.AddScoped<NumberSource>();
services.AddScoped<StringTransformer>();

var builder = new DataFlowGraphBuilder("my-flow", logger);
builder
    .AddBlock(new PlainSourceAdapter<int, NumberSource>(
        new BlockContext("source"), scopeFactory, "source"))
    .AddBlock(new EpochActorBlock<int, string, StringTransformer>(
        new BlockContext("transform"), scopeFactory))
    .AddBlock(new EpochBatchBlock<string>(
        new BlockContext("batcher"), 100))
    .Connect("source", "transform")
    .Connect("transform", "batcher");

var graph = builder.Build();
```

**Key Points**:
- ✅ Source actor unchanged
- ✅ Transformer actor unchanged  
- ✅ Only block instantiation updated
- ✅ Same graph topology
- ✅ Automatic epoch wrapping

---

## Performance Impact

**Research Validation**: Single-epoch wrapping overhead measured at **4.08%** (well within acceptable <5% threshold)

### Benchmark Results

| Scenario | Plain Stream | Single-Epoch Wrapped | Overhead |
|----------|-------------|---------------------|----------|
| Throughput (items/sec) | 20.5M | 19.7M | 4.08% |
| Memory allocation | Baseline | +metadata only | Negligible |

**Real-World Impact**: In production pipelines with I/O (database, files, network), the overhead is typically <2% as I/O dominates processing time.

---

## Migration Timeline

### v2.0 (Current - Q4 2025)
- ✅ `SingleEpochExtensions` available
- ✅ `PlainSourceAdapter` available
- ✅ Epoch blocks fully functional
- ⚠️ Plain blocks marked as `[Obsolete]` with warnings
- ✅ Both APIs supported

### v2.x (Q1-Q4 2026)
- ⚠️ Deprecation warnings encouraged
- 📖 Documentation updated to unified approach
- 📖 Examples migrated to epoch blocks
- ✅ Both APIs continue to work

### v3.0 (Q1 2027)
- ❌ Plain blocks removed
- ✅ Only epoch-based blocks remain
- ✅ "Epoch" prefix potentially dropped (all blocks are epoch-based)

---

## FAQ

### Q: Do I need to change my source actors?

**A**: No! Your existing `IPlainSourceActor<T>` implementations work as-is with `PlainSourceAdapter`.

### Q: What if I have a lot of plain sources?

**A**: Use `PlainSourceAdapter` as a drop-in replacement. No source code changes needed - just change block instantiation.

### Q: Can I mix plain and epoch sources?

**A**: Yes! `PlainSourceAdapter` automatically wraps plain sources in single epochs, making them compatible with epoch-aware blocks.

### Q: What about performance?

**A**: Single-epoch wrapping adds 4.08% overhead (validated through benchmarking). In real pipelines with I/O, the impact is typically <2%.

### Q: When should I convert to epoch-aware sources?

**A**: Convert when:
- You need multi-epoch segmentation
- You want native epoch control
- You're creating new sources (best practice)

Keep plain sources with `PlainSourceAdapter` when:
- Legacy code that works well
- Simple single-stream scenarios
- Not ready for epoch segmentation logic

### Q: What happens if I don't migrate?

**A**: Your code will continue to work in v2.x with deprecation warnings. In v3.0 (Q1 2027), plain blocks will be removed and you'll need to migrate.

---

## Support

### Getting Help

- **Documentation**: `/docs/` (general documentation)
- **Research**: `/research/mandatory-epochs/`
- **Examples**: See examples in `/docs/examples/`

### Reporting Issues

If you encounter migration challenges:
1. Check this guide and research documentation
2. Review existing issues for similar problems
3. Create a new issue with `migration` label

---

## Summary

**Migration Steps**:
1. ✅ Replace `PlainSourceBlock` with `PlainSourceAdapter` (or make source epoch-aware)
2. ✅ Replace `ActorBlock` with `EpochActorBlock`
3. ✅ Replace `BatchBlock` with `EpochBatchBlock`
4. ✅ Replace `ProducerBlock` with wrapped epoch stream
5. ✅ Test your pipeline
6. ✅ Remove deprecation warnings from build

**Benefits**:
- Single mental model (everything is epoch-based)
- Reduced code duplication
- Improved maintainability
- Foundation for advanced epoch features

**Timeline**:
- v2.0-v2.x: Both APIs work (with warnings)
- v3.0: Plain blocks removed

---

**Ready to migrate?** Start with one flow, test thoroughly, then proceed with the rest!
