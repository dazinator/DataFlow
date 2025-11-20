# PlainSourceAdapter: Legacy Source Compatibility

## Overview

`PlainSourceAdapter<T, TActor>` is a source block adapter that wraps plain source actors to work in the unified epoch-based architecture. It automatically wraps plain `IAsyncEnumerable<T>` streams in single-epoch containers, enabling legacy sources to work seamlessly with epoch-aware blocks.

### Key Features

- **Automatic Epoch Wrapping**: Plain streams are automatically wrapped in single-epoch streams
- **Legacy Compatibility**: Enables existing plain sources to work with epoch-based architecture
- **DI Scope Management**: Each source actor runs in its own `IServiceScope`
- **Simplified Migration**: No changes needed to existing source actor implementations
- **Output**: Produces `IAsyncEnumerable<IEpochStream<T>>` for epoch-aware pipelines

### Migration Note

⚠️ **PlainSourceBlock has been removed** in v3.0. Use `PlainSourceAdapter` for legacy plain sources, or migrate to `EpochSourceBlock` for new epoch-aware source implementations.

## Architecture

### Components

```
┌────────────────────────────────────────────────────────┐
│           PlainSourceAdapter<T, TActor>                │
│                                                         │
│  ┌──────────────────────────────────────────────┐    │
│  │  DI Scope                                     │    │
│  │  ┌────────────────────────────────────────┐ │    │
│  │  │  TActor : IPlainSourceActor<T>          │ │    │
│  │  │  • ProduceAsync() → IAsyncEnumerable<T> │ │    │
│  │  │  • No epoch knowledge (legacy)           │ │    │
│  │  └────────────────────────────────────────┘ │    │
│  └──────────────────────────────────────────────┘    │
│                       │                                │
│                       ▼                                │
│              .WrapInSingleEpoch()                     │
│                       │                                │
│                       ▼                                │
│         IAsyncEnumerable<IEpochStream<T>>             │
│         (all items in single epoch)                   │
└────────────────────────────────────────────────────────┘
```

### Data Flow

```
PlainSourceAdapter → IAsyncEnumerable<IEpochStream<T>> → EpochActorBlock
                                                       OR
                                                          EpochBatchBlock
```

## Usage

### Basic Implementation

```csharp
// Legacy plain source actor (no changes needed)
public class MyDataProducer : IPlainSourceActor<int>
{
    public async IAsyncEnumerable<int> ProduceAsync(
        [EnumeratorCancellation] IActorExecutionContext context)
    {
        for (int i = 0; i < 100; i++)
        {
            yield return i;
        }
    }
}

// Wrap in adapter for epoch-based pipeline
var adapter = new PlainSourceAdapter<int, MyDataProducer>(
    new BlockContext("source"),
    serviceScopeFactory,
    "source-name"); // source name used in epoch vector

// Output is IAsyncEnumerable<IEpochStream<int>>
// All 100 items will be in a single epoch
```

### With Epoch Segmentation

If you need custom epoch boundaries, use `EpochSegmenterBlock` after the adapter (though this is uncommon since the adapter already creates epochs):

```csharp
var adapter = new PlainSourceAdapter<int, MyDataProducer>(
    new BlockContext("source"),
    serviceScopeFactory,
    "source");

// Re-segment if needed (flattens and re-epochs)
var segmenter = new EpochSegmenterBlock<int>(
    new BlockContext("segmenter"),
    EpochSegmentationPolicy.ByCount(10, "source"));
```

**Note**: For most cases, if you need custom epoch boundaries, prefer migrating to `EpochSourceBlock` with an epoch-aware actor.

## Migration from PlainSourceBlock

### Before (v2.x with PlainSourceBlock)
```csharp
var source = new PlainSourceBlock<int, MyProducer>(
    "source",
    serviceScopeFactory);
```

### After (v3.0 with PlainSourceAdapter)
```csharp
var source = new PlainSourceAdapter<int, MyProducer>(
    new BlockContext("source"),
    serviceScopeFactory,
    "source");
```

### Recommended (New Code)
```csharp
// Migrate to epoch-aware source
var source = new EpochSourceBlock<int, MyEpochProducer>(
    new BlockContext("source"),
    serviceScopeFactory);
```

## When to Use

- **Use PlainSourceAdapter when**: You have existing plain source actors that you cannot immediately migrate
- **Use EpochSourceBlock when**: Writing new code or when you need epoch-aware source logic
- **Performance**: PlainSourceAdapter has minimal overhead (<5% as validated in benchmarks)

## See Also

- [EpochSourceBlock](./epoch-source-block.md) - Epoch-aware source implementation
- [EpochSegmenterBlock](./epoch-segmenter-block.md) - Custom epoch segmentation
- [Migration Guide](/poc/docs/migrations/v3-epoch-only.md) - Complete v3.0 migration guide
