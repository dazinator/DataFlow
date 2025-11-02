# Source Anchor Demo with EF Core

A demonstration project showing how to implement **source-internal anchor management** with **Entity Framework Core** and **SQLite** for resumable data processing.

This project is part of **Phase 5** of the DataFlow POC series, building on Phase 4's streaming epoch infrastructure.

## ⚠️ Architecture Update

**Simplified architecture** based on feedback:
- ❌ Removed: `IAnchorStore`, `SqliteAnchorStore`, `AnchorCoordinator`
- ✅ Sources now manage anchors internally
- ✅ Future: Core library checkpoint system will handle persistence

## Terminology

- **Anchor**: Source-specific resume point (e.g., `lastProcessedId`) - managed internally by source
- **Checkpoint**: Graph-level consistent snapshot (NOT implemented in this demo)
- This demo focuses on **internal anchor management**; full checkpoint support would aggregate state from all blocks at global alignment

## Quick Start

### Running the Tests

```bash
cd /path/to/lib-dataflow
dotnet test poc/EpochAnchoringDemo.Tests/EpochAnchoringDemo.Tests.csproj
```

All 6 integration tests should pass, demonstrating:
- Domain anchor management (lastProcessedId)
- Resume from saved anchor
- Per-epoch transactional writes
- Epoch lifecycle event handling
- End-to-end pipeline with interruption and resume
- No duplicate processing

### Basic Usage

```csharp
// 1. Configure database
var dbOptions = new DbContextOptionsBuilder<DemoDbContext>()
    .UseSqlite("Data Source=data.db")
    .Options;

// 2. Create source actor (streams data in epoch chunks)
// Optionally pass initialLastProcessedId for resume (from checkpoint in production)
var sourceActor = new DatabaseSourceActor(
    dbOptions,
    epochSize: 100,  // 100 records per epoch
    sourceId: "my-flow",
    initialLastProcessedId: 0);  // 0 = start from beginning

// 3. Create write block (processes with per-epoch transactions)
var writeBlock = new WriteContextBlock(dbOptions);

// 4. Execute pipeline
var context = new ActorExecutionContext(cancellationToken);
var inputEpochs = sourceActor.ProduceEpochsAsync(context);
var outputEpochs = writeBlock.ProcessAsync(inputEpochs);

await foreach (var epochStream in outputEpochs)
{
    await coordinator.OnEpochCreatedAsync(epochStream.Epoch);
    
    await foreach (var item in epochStream.Items)
    {
        // Process items (already marked as processed by WriteContextBlock)
    }
    
    await coordinator.OnEpochCompletedAsync(epochStream.Epoch);
    // Anchor automatically saved
}
```

## Project Structure

```
EpochAnchoringDemo/
├── Core/
│   ├── IAnchorStore.cs              # Anchor persistence interface
│   ├── SqliteAnchorStore.cs         # SQLite implementation
│   ├── IEpochLifecycleObserver.cs   # Lifecycle event observer
│   └── AnchorCoordinator.cs         # Auto-anchor on epoch completion
├── Blocks/
│   ├── DatabaseSourceActor.cs       # EF Core source with epoch streaming
│   └── WriteContextBlock.cs         # Per-epoch transactional processing
└── Models/
    ├── DataRecord.cs                # Sample entity
    └── DemoDbContext.cs             # EF Core context

EpochAnchoringDemo.Tests/
└── EpochAnchoringIntegrationTests.cs  # 11 integration tests
```

## Key Concepts

### Anchoring

An **anchor** is a source-specific resume point that marks the last successfully completed epoch for that source. When the source resumes after interruption, it reads its anchor and continues from the next epoch.

Benefits:
- **Source-local state**: Each source maintains its own anchor
- **Fast resumption**: Skip already-processed epochs
- **Epoch alignment**: Anchors stored as epoch vectors for coordination

### Epoch Lifecycle

Each epoch goes through a lifecycle:

1. **Created**: New epoch starts, observers notified via `OnEpochCreatedAsync`
2. **Processing**: Items stream through the epoch
3. **Completed**: All items processed, observers notified via `OnEpochCompletedAsync`
4. **Checkpointed**: Anchor saved to durable storage

### Transactional Boundaries

Each epoch runs in its own database transaction:

```
Epoch 1 [────Transaction 1────] ✓ Committed
Epoch 2 [────Transaction 2────] ✓ Committed
Epoch 3 [────Transaction 3────] ✗ Rolled back (error)
```

If epoch 3 fails, epochs 1 and 2 remain committed. On restart, only epoch 3 needs to be retried.

## Architecture

```
┌──────────────────────────────────────────────────────┐
│                Processing Pipeline                    │
└──────────────────────────────────────────────────────┘

┌────────────────────┐        ┌─────────────────────┐
│DatabaseSourceActor │        │  WriteContextBlock  │
│                    │        │                     │
│• Reads from DB     │───────▶│• Per-epoch DbContext│
│• Emits epochs      │ Epochs │• SQLite transaction │
│• Resumes from      │        │• Marks processed    │
│  saved anchor      │        │                     │
└────────┬───────────┘        └─────────┬───────────┘
         │                              │
         │                              │
         ▼                              ▼
┌────────────────────┐        ┌─────────────────────┐
│   IAnchorStore     │        │AnchorCoordinator│
│                    │◀───────│                     │
│• Persists epochs   │  Save  │• Observes lifecycle │
│• Enables resume    │        │• Auto-checkpoints   │
└────────────────────┘        └─────────────────────┘
```

## Performance

From Phase 5 testing:

| Metric | Value |
|--------|-------|
| Overhead vs continuous | +0.2% |
| Memory increase | +5% |
| Checkpoint latency | ~2ms per epoch |
| Epoch granularity sweet spot | 100-1000 items |

See [PHASE5_EFCORE_ANCHORING_DEMO.md](../PHASE5_EFCORE_ANCHORING_DEMO.md) for detailed analysis.

## Integration with Phase 4

This demo builds on Phase 4's infrastructure:

- `ISourceActor<T>` - Base class for source actors
- `IEpochStream<T>` - Streaming epoch segments
- `EpochVector` - Epoch identification
- `CompletionBasedEpochProgress` - Progress tracking

Phase 5 adds:
- Checkpoint persistence (`IAnchorStore`)
- Lifecycle observers (`IEpochLifecycleObserver`)
- EF Core integration patterns
- Transactional write blocks

## Success Criteria (All Met ✅)

| Criterion | Status |
|-----------|--------|
| ✅ Correct checkpoint resume | Verified in tests |
| ✅ No duplicate processing | Verified in tests |
| ✅ Per-epoch transactions | Demonstrated |
| ✅ Lifecycle hooks fire | Verified in tests |
| ✅ Overhead ≤ 5% | +0.2% measured |
| ✅ Memory comparable | +5% measured |

## Documentation

Full documentation: [PHASE5_EFCORE_ANCHORING_DEMO.md](../PHASE5_EFCORE_ANCHORING_DEMO.md)

Includes:
- Architecture details
- Implementation patterns
- Test results and validation
- Performance characteristics
- Design decisions
- Future work considerations

## License

This is part of the lib-dataflow repository. See repository root for license information.

## Related Work

- **Phase 4**: [PHASE4_SOURCE_ACTOR_AND_STREAMING_BUFFERS.md](../PHASE4_SOURCE_ACTOR_AND_STREAMING_BUFFERS.md)
- **Phase 3**: [PHASE3_EPOCH_STREAM_SEGMENTATION.md](../PHASE3_EPOCH_STREAM_SEGMENTATION.md)
- **Issue #119**: Phase 4 implementation tracking
