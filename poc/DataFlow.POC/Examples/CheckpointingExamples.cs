namespace DataFlow.POC.Examples;

using System.Text.Json;
using DataFlow.POC.Checkpointing;
using DataFlow.POC.Core;

/// <summary>
/// Example of a checkpoint-aware source block that saves its offset to checkpoints.
/// This allows the source to resume from the last checkpoint position after a restart.
/// </summary>
public class CheckpointAwareQueueSource
{
    private long _currentOffset = 0;
    private readonly string _blockId;

    public CheckpointAwareQueueSource(string blockId, long initialOffset = 0)
    {
        _blockId = blockId ?? throw new ArgumentNullException(nameof(blockId));
        _currentOffset = initialOffset;
    }

    /// <summary>
    /// Example of producing items while being checkpoint-aware.
    /// When the epoch is checkpointing, the source contributes its current offset.
    /// </summary>
    public async IAsyncEnumerable<int> ProduceAsync(IEpoch epoch, CancellationToken cancellationToken = default)
    {
        // Simulate producing items
        for (int i = 0; i < 100; i++)
        {
            yield return i;
            _currentOffset++;
            
            // Check if we're at an epoch boundary and should checkpoint
            if (epoch.IsCheckpointing)
            {
                // Contribute checkpoint state within a serialized operation
                // This ensures thread-safe access to the checkpoint
                await epoch.QueueSerializedOperationAsync<DummyService>(async (svc, ctx) =>
                {
                    // Use SetState helper for canonical, ergonomic API
                    ctx.Checkpoint?.SetState(_blockId, new { offset = _currentOffset });
                    await Task.CompletedTask;
                }, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Restores the source state from a checkpoint.
    /// This would typically be called during dataflow initialization.
    /// </summary>
    public void RestoreFromCheckpoint(ICheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        if (checkpoint.TryGetBlockState(_blockId, out var state))
        {
            _currentOffset = state.GetProperty("offset").GetInt64();
        }
    }

    // Dummy service for example purposes
    private class DummyService { }
}

/// <summary>
/// Example showing how to configure checkpointing in a dataflow.
/// </summary>
public static class CheckpointingExamples
{
    /// <summary>
    /// Example 1: Simple checkpoint strategy - checkpoint every 10 epochs
    /// </summary>
    public static void ConfigureCheckpointingEveryNEpochs()
    {
        // Create a checkpoint strategy
        var strategy = new Checkpointing.Strategies.EveryNEpochsStrategy(10);

        // Create epoch coordinator with checkpoint strategy
        // var coordinator = new EpochCoordinator(serviceProvider.GetRequiredService<IServiceScopeFactory>(), 
        //     checkpointStrategy: strategy);
    }

    /// <summary>
    /// Example 2: Time-based checkpoint strategy - checkpoint every 5 minutes
    /// </summary>
    public static void ConfigureCheckpointingTimeBased()
    {
        // Create a time-based checkpoint strategy
        var strategy = new Checkpointing.Strategies.TimeBasedStrategy(TimeSpan.FromMinutes(5));

        // Create epoch coordinator with checkpoint strategy
        // var coordinator = new EpochCoordinator(serviceProvider.GetRequiredService<IServiceScopeFactory>(), 
        //     checkpointStrategy: strategy);
    }

    /// <summary>
    /// Example 3: Block contributing to checkpoint
    /// </summary>
    public static async Task BlockContributingToCheckpoint(IEpoch epoch)
    {
        // Check if epoch is being checkpointed
        if (epoch.IsCheckpointing)
        {
            // Contribute block state within serialized operation
            await epoch.QueueSerializedOperationAsync<MyService>(async (svc, ctx) =>
            {
                // Get block's current state
                var blockState = GetBlockState();
                
                // Add to checkpoint
                ctx.Checkpoint?.AddBlockState("my-block-id", blockState);
                
                await Task.CompletedTask;
            });
        }
    }

    /// <summary>
    /// Example 4: Persisting checkpoint (application-specific)
    /// </summary>
    public static async Task PersistCheckpointExample(IEpoch epoch)
    {
        // This example shows how an application might persist checkpoints
        // using EpochHooks
        
        var hooks = new EpochHooks
        {
            OnCommitEpoch = async (e, ct) =>
            {
                // First, commit the transaction
                await e.QueueSerializedOperationAsync<MyDbContext>(async (db, ctx) =>
                {
                    await db.SaveChangesAsync(ct);
                    
                    // If checkpointing, save checkpoint to database
                    if (ctx.Checkpoint != null)
                    {
                        // Serialize checkpoint to your persistence format
                        // For example, save to database table:
                        // db.Checkpoints.Add(new CheckpointEntity
                        // {
                        //     CheckpointId = ctx.Checkpoint.CheckpointId,
                        //     EpochVector = SerializeVector(ctx.Checkpoint.EpochVector),
                        //     BlockStates = SerializeStates(ctx.Checkpoint.BlockStates),
                        //     Timestamp = ctx.Checkpoint.Timestamp
                        // });
                    }
                    
                    await db.Database.CommitTransactionAsync(ct);
                }, ct);
            }
        };
    }

    /// <summary>
    /// Example 5: Complete end-to-end recovery scenario
    /// This shows how an application provides a checkpoint for resuming execution.
    /// </summary>
    public static class EndToEndRecoveryExample
    {
        // Step 1: Define a simple checkpoint store
        public class SimpleCheckpointStore
        {
            private readonly Dictionary<string, ICheckpoint> _checkpoints = new();
            private string? _latestCheckpointId;

            public Task SaveCheckpointAsync(ICheckpoint checkpoint)
            {
                _checkpoints[checkpoint.CheckpointId] = checkpoint;
                _latestCheckpointId = checkpoint.CheckpointId;
                return Task.CompletedTask;
            }

            public Task<ICheckpoint?> GetLatestCheckpointAsync()
            {
                if (_latestCheckpointId != null && _checkpoints.TryGetValue(_latestCheckpointId, out var checkpoint))
                {
                    return Task.FromResult<ICheckpoint?>(checkpoint);
                }
                return Task.FromResult<ICheckpoint?>(null);
            }
        }

        // Step 2: Define a resumable source block
        public class ResumableMessageSource
        {
            private long _currentOffset;
            private readonly string _blockId;

            public ResumableMessageSource(string blockId)
            {
                _blockId = blockId;
                _currentOffset = 0;
            }

            // Restore from checkpoint before execution starts
            public void RestoreFromCheckpoint(ICheckpoint checkpoint)
            {
                if (checkpoint.TryGetBlockState(_blockId, out var state))
                {
                    _currentOffset = state.GetProperty("offset").GetInt64();
                    Console.WriteLine($"Restored {_blockId} to offset {_currentOffset}");
                }
            }

            // Save to checkpoint during execution
            public async IAsyncEnumerable<string> ProduceAsync(IEpoch epoch, CancellationToken ct = default)
            {
                // Simulate producing messages
                for (int i = 0; i < 100; i++)
                {
                    yield return $"Message at offset {_currentOffset}";
                    _currentOffset++;

                    if (epoch.IsCheckpointing)
                    {
                        await epoch.QueueSerializedOperationAsync<DummyServiceType>(async (svc, ctx) =>
                        {
                            ctx.Checkpoint?.SetState(_blockId, new { offset = _currentOffset });
                            await Task.CompletedTask;
                        }, ct);
                    }
                }
            }
            
            private class DummyServiceType { }
        }

        // Step 3: Application orchestration
        public class DataFlowApplication
        {
            private readonly SimpleCheckpointStore _checkpointStore = new();
            
            public async Task ExecuteAsync(bool recoverFromCheckpoint)
            {
                // 1. Create blocks
                var sourceBlock = new ResumableMessageSource("message-source");

                // 2. RECOVERY: Restore from checkpoint if requested
                if (recoverFromCheckpoint)
                {
                    var checkpoint = await _checkpointStore.GetLatestCheckpointAsync();
                    if (checkpoint != null)
                    {
                        Console.WriteLine($"Recovering from checkpoint: {checkpoint.CheckpointId}");
                        Console.WriteLine($"Checkpoint created at: {checkpoint.Timestamp}");
                        Console.WriteLine($"Epoch vector: {checkpoint.EpochVector}");
                        
                        // Restore each block's state
                        sourceBlock.RestoreFromCheckpoint(checkpoint);
                    }
                    else
                    {
                        Console.WriteLine("No checkpoint found, starting from beginning");
                    }
                }

                // 3. Configure checkpointing
                var strategy = new Checkpointing.Strategies.EveryNEpochsStrategy(5);
                // var coordinator = new EpochCoordinator(scopeFactory, checkpointStrategy: strategy);

                // 4. Configure checkpoint persistence in hooks
                var hooks = new EpochHooks
                {
                    OnCommitEpoch = async (epoch, ct) =>
                    {
                        await epoch.QueueSerializedOperationAsync<DummyServiceType>(async (svc, ctx) =>
                        {
                            if (ctx.Checkpoint != null)
                            {
                                await _checkpointStore.SaveCheckpointAsync(ctx.Checkpoint);
                                Console.WriteLine($"Checkpoint saved: {ctx.Checkpoint.CheckpointId}");
                            }
                            await Task.CompletedTask;
                        }, ct);
                    }
                };

                // 5. Execute dataflow - blocks start from restored state
                // (sourceBlock will resume from _currentOffset if restored from checkpoint)
            }
            
            private class DummyServiceType { }
        }

        // Usage example:
        public static async Task DemoRecovery()
        {
            var app = new DataFlowApplication();

            // First execution - no checkpoint, starts from offset 0
            Console.WriteLine("=== First Execution ===");
            await app.ExecuteAsync(recoverFromCheckpoint: false);
            // Executes and creates checkpoints at epochs 5, 10, 15, etc.

            // Simulate failure and restart
            Console.WriteLine("\n=== After Failure - Recovering ===");
            
            // Second execution - recovers from last checkpoint
            await app.ExecuteAsync(recoverFromCheckpoint: true);
            // Resumes from last checkpointed offset instead of starting over
        }
    }

    // Dummy types for examples
    private class MyService { }
    private class MyDbContext
    {
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Database Database => null!;
    }
    private class Database
    {
        public Task CommitTransactionAsync(CancellationToken ct) => Task.CompletedTask;
    }
    
    private static JsonElement GetBlockState() => JsonSerializer.SerializeToElement(new { offset = 42, processed = true });
}
