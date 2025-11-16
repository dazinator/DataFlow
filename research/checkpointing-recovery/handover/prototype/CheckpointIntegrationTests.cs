namespace DataFlow.POC.Checkpointing.Tests;

using DataFlow.POC.Checkpointing;
using DataFlow.POC.Checkpointing.Examples;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Integration tests demonstrating end-to-end checkpoint and recovery scenarios.
/// </summary>
public class CheckpointIntegrationTests
{
    [Fact]
    public async Task EndToEnd_CheckpointAndRecover_PreservesProgress()
    {
        // This test simulates a complete workflow:
        // 1. Start processing messages
        // 2. Create checkpoints periodically
        // 3. Simulate failure
        // 4. Recover from checkpoint
        // 5. Verify processing continues from checkpoint position
        
        var store = new InMemoryCheckpointStore();
        var strategy = new EveryNEpochsStrategy(2); // Checkpoint every 2 epochs
        
        // PHASE 1: Initial run that creates checkpoints
        {
            var coordinator = new CheckpointCoordinator(store, strategy);
            var source = new CheckpointAwareMessageSource(100);
            var accumulator = new CheckpointAwareAccumulator();
            
            coordinator.RegisterBlock("source", source);
            coordinator.RegisterBlock("accumulator", accumulator);
            
            var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
            
            // Process 5 messages
            await foreach (var message in source.ProduceAsync().Take(5))
            {
                await accumulator.ProcessAsync(message);
            }
            
            // Complete epoch 1 (no checkpoint due to strategy)
            var epoch1 = new Epoch(new EpochVector(new Dictionary<string, long> { ["source1"] = 1 }), scopeFactory.CreateScope());
            await coordinator.OnEpochCompletedAsync(epoch1);
            
            // Process 5 more messages
            await foreach (var message in source.ProduceAsync().Skip(5).Take(5))
            {
                await accumulator.ProcessAsync(message);
            }
            
            // Complete epoch 2 (checkpoint created!)
            var epoch2 = new Epoch(new EpochVector(new Dictionary<string, long> { ["source1"] = 2 }), scopeFactory.CreateScope());
            await coordinator.OnEpochCompletedAsync(epoch2);
            
            // At this point: offset=10, count=10, checkpoint saved
            Assert.Equal(10, source.CurrentOffset);
            Assert.Equal(10, accumulator.ProcessedCount);
            
            var checkpoints = await store.ListCheckpointsAsync();
            Assert.Single(checkpoints);
        }
        
        // PHASE 2: Simulate failure and recovery
        {
            var coordinator = new CheckpointCoordinator(store, strategy);
            var source = new CheckpointAwareMessageSource(100); // Fresh instance
            var accumulator = new CheckpointAwareAccumulator(); // Fresh instance
            
            // Initially at 0
            Assert.Equal(0, source.CurrentOffset);
            Assert.Equal(0, accumulator.ProcessedCount);
            
            // Register and restore
            coordinator.RegisterBlock("source", source);
            coordinator.RegisterBlock("accumulator", accumulator);
            
            var restored = await coordinator.RestoreFromLatestCheckpointAsync();
            Assert.True(restored);
            
            // Verify restored state
            Assert.Equal(10, source.CurrentOffset);
            Assert.Equal(10, accumulator.ProcessedCount);
            
            // Continue processing from checkpoint position
            var messagesProcessed = 0;
            await foreach (var message in source.ProduceAsync().Take(5))
            {
                await accumulator.ProcessAsync(message);
                messagesProcessed++;
                
                // Verify messages start from offset 10
                Assert.True(message.Offset >= 10);
            }
            
            Assert.Equal(5, messagesProcessed);
            Assert.Equal(15, source.CurrentOffset); // Continued from 10 to 15
            Assert.Equal(15, accumulator.ProcessedCount);
        }
    }
    
    [Fact]
    public async Task MultipleCheckpoints_RestoresFromLatest()
    {
        var store = new InMemoryCheckpointStore();
        var strategy = new EveryNEpochsStrategy(1); // Checkpoint every epoch
        var coordinator = new CheckpointCoordinator(store, strategy);
        
        var source = new CheckpointAwareMessageSource(100);
        coordinator.RegisterBlock("source", source);
        
        var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        
        // Create multiple checkpoints at different offsets
        for (int i = 1; i <= 3; i++)
        {
            // Process some messages
            await foreach (var _ in source.ProduceAsync().Skip(source.CurrentOffset).Take(10))
            {
                // Just consume
            }
            
            var epoch = new Epoch(
                new EpochVector(new Dictionary<string, long> { ["source1"] = i }),
                scopeFactory.CreateScope());
            await coordinator.OnEpochCompletedAsync(epoch);
            
            await Task.Delay(10); // Ensure different timestamps
        }
        
        // Should have 3 checkpoints
        var checkpoints = await store.ListCheckpointsAsync();
        Assert.Equal(3, checkpoints.Count);
        
        // Create new coordinator and restore
        var newCoordinator = new CheckpointCoordinator(store, strategy);
        var newSource = new CheckpointAwareMessageSource(100);
        newCoordinator.RegisterBlock("source", newSource);
        
        await newCoordinator.RestoreFromLatestCheckpointAsync();
        
        // Should restore from latest checkpoint (offset 30)
        Assert.Equal(30, newSource.CurrentOffset);
    }
    
    [Fact]
    public async Task RecoveryWithMissingBlock_HandlesGracefully()
    {
        // Test that recovery works even if checkpoint has state for blocks
        // that are not registered in the new dataflow configuration
        
        var store = new InMemoryCheckpointStore();
        var strategy = new EveryNEpochsStrategy(1);
        
        // Phase 1: Create checkpoint with two blocks
        {
            var coordinator = new CheckpointCoordinator(store, strategy);
            var source = new CheckpointAwareMessageSource(100);
            var accumulator = new CheckpointAwareAccumulator();
            
            coordinator.RegisterBlock("source", source);
            coordinator.RegisterBlock("accumulator", accumulator);
            
            await foreach (var message in source.ProduceAsync().Take(10))
            {
                await accumulator.ProcessAsync(message);
            }
            
            var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
            var epoch = new Epoch(new EpochVector(new Dictionary<string, long> { ["source1"] = 1 }), scopeFactory.CreateScope());
            await coordinator.OnEpochCompletedAsync(epoch);
        }
        
        // Phase 2: Restore with only source block (accumulator not registered)
        {
            var coordinator = new CheckpointCoordinator(store, strategy);
            var source = new CheckpointAwareMessageSource(100);
            // Note: NOT registering accumulator
            coordinator.RegisterBlock("source", source);
            
            // Should succeed despite missing block
            var restored = await coordinator.RestoreFromLatestCheckpointAsync();
            Assert.True(restored);
            Assert.Equal(10, source.CurrentOffset);
        }
    }
    
    [Fact]
    public async Task CheckpointWithNoBlocks_CreatesEmptyCheckpoint()
    {
        // Test that checkpoints can be created even with no checkpoint-aware blocks
        
        var store = new InMemoryCheckpointStore();
        var strategy = new EveryNEpochsStrategy(1);
        var coordinator = new CheckpointCoordinator(store, strategy);
        // Not registering any blocks
        
        var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var epoch = new Epoch(new EpochVector(new Dictionary<string, long> { ["source1"] = 1 }), scopeFactory.CreateScope());
        
        // Should not throw
        await coordinator.OnEpochCompletedAsync(epoch);
        
        var checkpoints = await store.ListCheckpointsAsync();
        Assert.Single(checkpoints);
        
        var checkpoint = await store.GetCheckpointAsync(checkpoints[0]);
        Assert.NotNull(checkpoint);
        Assert.Empty(checkpoint.BlockStates); // No block states
    }
    
    [Fact]
    public async Task SpecificCheckpointRestore_WorksCorrectly()
    {
        var store = new InMemoryCheckpointStore();
        var strategy = new EveryNEpochsStrategy(1);
        var coordinator = new CheckpointCoordinator(store, strategy);
        
        var source = new CheckpointAwareMessageSource(100);
        coordinator.RegisterBlock("source", source);
        
        var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        
        // Create checkpoints at offset 10 and 20
        string firstCheckpointId;
        {
            await foreach (var _ in source.ProduceAsync().Take(10))
            {
                // Consume
            }
            var epoch1 = new Epoch(new EpochVector(new Dictionary<string, long> { ["source1"] = 1 }), scopeFactory.CreateScope());
            await coordinator.OnEpochCompletedAsync(epoch1);
            
            var checkpoints = await store.ListCheckpointsAsync();
            firstCheckpointId = checkpoints[0];
            
            await foreach (var _ in source.ProduceAsync().Skip(10).Take(10))
            {
                // Consume more
            }
            var epoch2 = new Epoch(new EpochVector(new Dictionary<string, long> { ["source1"] = 2 }), scopeFactory.CreateScope());
            await coordinator.OnEpochCompletedAsync(epoch2);
        }
        
        // Restore from first checkpoint (not latest)
        var newCoordinator = new CheckpointCoordinator(store, strategy);
        var newSource = new CheckpointAwareMessageSource(100);
        newCoordinator.RegisterBlock("source", newSource);
        
        var restored = await newCoordinator.RestoreFromCheckpointAsync(firstCheckpointId);
        Assert.True(restored);
        Assert.Equal(10, newSource.CurrentOffset); // Restored to first checkpoint, not latest
    }
}
