namespace DataFlow.POC.Checkpointing.Tests;

using DataFlow.POC.Checkpointing;
using DataFlow.POC.Checkpointing.Examples;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Tests for checkpoint creation and recovery functionality.
/// </summary>
public class CheckpointTests
{
    [Fact]
    public async Task CheckpointStore_SaveAndRetrieve_WorksCorrectly()
    {
        // Arrange
        var store = new InMemoryCheckpointStore();
        var epochVector = new EpochVector(new Dictionary<string, long> { ["source1"] = 5 });
        var checkpoint = new Checkpoint
        {
            CheckpointId = "test-checkpoint-1",
            EpochVector = epochVector,
            Timestamp = DateTimeOffset.UtcNow,
            BlockStates = new Dictionary<string, byte[]>
            {
                ["block1"] = System.Text.Encoding.UTF8.GetBytes("state1"),
                ["block2"] = System.Text.Encoding.UTF8.GetBytes("state2")
            }
        };
        
        // Act
        await store.SaveCheckpointAsync(checkpoint);
        var retrieved = await store.GetCheckpointAsync("test-checkpoint-1");
        
        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("test-checkpoint-1", retrieved.CheckpointId);
        Assert.Equal(epochVector, retrieved.EpochVector);
        Assert.Equal(2, retrieved.BlockStates.Count);
        Assert.Equal("state1", System.Text.Encoding.UTF8.GetString(retrieved.BlockStates["block1"]));
    }
    
    [Fact]
    public async Task CheckpointStore_GetLatest_ReturnsNewestCheckpoint()
    {
        // Arrange
        var store = new InMemoryCheckpointStore();
        var epoch1 = new EpochVector(new Dictionary<string, long> { ["source1"] = 1 });
        var epoch2 = new EpochVector(new Dictionary<string, long> { ["source1"] = 2 });
        
        var checkpoint1 = new Checkpoint
        {
            CheckpointId = "checkpoint-1",
            EpochVector = epoch1,
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10),
            BlockStates = new Dictionary<string, byte[]>()
        };
        
        await Task.Delay(10); // Ensure different timestamps
        
        var checkpoint2 = new Checkpoint
        {
            CheckpointId = "checkpoint-2",
            EpochVector = epoch2,
            Timestamp = DateTimeOffset.UtcNow,
            BlockStates = new Dictionary<string, byte[]>()
        };
        
        // Act
        await store.SaveCheckpointAsync(checkpoint1);
        await store.SaveCheckpointAsync(checkpoint2);
        var latest = await store.GetLatestCheckpointAsync();
        
        // Assert
        Assert.NotNull(latest);
        Assert.Equal("checkpoint-2", latest.CheckpointId);
    }
    
    [Fact]
    public void EveryNEpochsStrategy_CreatesCheckpointAtCorrectInterval()
    {
        // Arrange
        var strategy = new EveryNEpochsStrategy(3);
        var epoch1 = new EpochVector(new Dictionary<string, long> { ["source1"] = 1 });
        var epoch2 = new EpochVector(new Dictionary<string, long> { ["source1"] = 2 });
        var epoch3 = new EpochVector(new Dictionary<string, long> { ["source1"] = 3 });
        var epoch4 = new EpochVector(new Dictionary<string, long> { ["source1"] = 4 });
        
        // Act & Assert
        Assert.False(strategy.ShouldCreateCheckpoint(epoch1)); // 1st epoch
        Assert.False(strategy.ShouldCreateCheckpoint(epoch2)); // 2nd epoch
        Assert.True(strategy.ShouldCreateCheckpoint(epoch3));  // 3rd epoch - checkpoint!
        Assert.False(strategy.ShouldCreateCheckpoint(epoch4)); // 4th epoch (counter reset)
    }
    
    [Fact]
    public void TimeBasedStrategy_CreatesCheckpointAfterInterval()
    {
        // Arrange
        var strategy = new TimeBasedStrategy(TimeSpan.FromMilliseconds(100));
        var epoch1 = new EpochVector(new Dictionary<string, long> { ["source1"] = 1 });
        var epoch2 = new EpochVector(new Dictionary<string, long> { ["source1"] = 2 });
        
        // Act & Assert
        Assert.True(strategy.ShouldCreateCheckpoint(epoch1)); // First checkpoint always created
        Assert.False(strategy.ShouldCreateCheckpoint(epoch2)); // Too soon
        
        Thread.Sleep(150); // Wait for interval
        Assert.True(strategy.ShouldCreateCheckpoint(epoch2)); // Should create now
    }
    
    [Fact]
    public async Task CheckpointCoordinator_CreatesCheckpoint_WhenStrategyIndicates()
    {
        // Arrange
        var store = new InMemoryCheckpointStore();
        var strategy = new EveryNEpochsStrategy(2);
        var coordinator = new CheckpointCoordinator(store, strategy);
        
        var source = new CheckpointAwareMessageSource(100);
        coordinator.RegisterBlock("source", source);
        
        var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var epoch1 = new Epoch(new EpochVector(new Dictionary<string, long> { ["source1"] = 1 }), scopeFactory.CreateScope());
        var epoch2 = new Epoch(new EpochVector(new Dictionary<string, long> { ["source1"] = 2 }), scopeFactory.CreateScope());
        
        // Act
        await coordinator.OnEpochCompletedAsync(epoch1); // First epoch - no checkpoint
        await coordinator.OnEpochCompletedAsync(epoch2); // Second epoch - checkpoint!
        
        var checkpoints = await store.ListCheckpointsAsync();
        
        // Assert
        Assert.Single(checkpoints); // One checkpoint created
        
        var checkpoint = await store.GetCheckpointAsync(checkpoints[0]);
        Assert.NotNull(checkpoint);
        Assert.Single(checkpoint.BlockStates); // Source block saved state
    }
    
    [Fact]
    public async Task CheckpointAwareSource_SavesAndRestoresOffset()
    {
        // Arrange
        var source = new CheckpointAwareMessageSource(100);
        var epoch = new EpochVector(new Dictionary<string, long> { ["source1"] = 10 });
        
        // Consume some messages
        await foreach (var message in source.ProduceAsync().Take(5))
        {
            // Process 5 messages
        }
        
        Assert.Equal(5, source.CurrentOffset);
        
        // Act - create checkpoint
        var state = await source.CreateCheckpointAsync("checkpoint-1", epoch);
        Assert.NotNull(state);
        
        // Create new source instance and restore
        var restoredSource = new CheckpointAwareMessageSource(100);
        Assert.Equal(0, restoredSource.CurrentOffset); // Initially 0
        
        await restoredSource.RestoreFromCheckpointAsync("checkpoint-1", state);
        
        // Assert
        Assert.Equal(5, restoredSource.CurrentOffset); // Restored to 5
    }
    
    [Fact]
    public async Task CheckpointCoordinator_RestoresAllBlocks_FromCheckpoint()
    {
        // Arrange
        var store = new InMemoryCheckpointStore();
        var strategy = new EveryNEpochsStrategy(1); // Checkpoint every epoch
        var coordinator = new CheckpointCoordinator(store, strategy);
        
        var source = new CheckpointAwareMessageSource(100);
        var accumulator = new CheckpointAwareAccumulator();
        
        coordinator.RegisterBlock("source", source);
        coordinator.RegisterBlock("accumulator", accumulator);
        
        // Simulate processing some messages
        await foreach (var message in source.ProduceAsync().Take(10))
        {
            await accumulator.ProcessAsync(message);
        }
        
        Assert.Equal(10, source.CurrentOffset);
        Assert.Equal(10, accumulator.ProcessedCount);
        
        // Create checkpoint
        var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var epoch = new Epoch(new EpochVector(new Dictionary<string, long> { ["source1"] = 1 }), scopeFactory.CreateScope());
        await coordinator.OnEpochCompletedAsync(epoch);
        
        // Act - Create new coordinator and blocks, then restore
        var newCoordinator = new CheckpointCoordinator(store, strategy);
        var newSource = new CheckpointAwareMessageSource(100);
        var newAccumulator = new CheckpointAwareAccumulator();
        
        newCoordinator.RegisterBlock("source", newSource);
        newCoordinator.RegisterBlock("accumulator", newAccumulator);
        
        var restored = await newCoordinator.RestoreFromLatestCheckpointAsync();
        
        // Assert
        Assert.True(restored);
        Assert.Equal(10, newSource.CurrentOffset);
        Assert.Equal(10, newAccumulator.ProcessedCount);
    }
    
    [Fact]
    public async Task CheckpointCoordinator_HandlesBlockCheckpointFailure_Gracefully()
    {
        // Arrange
        var store = new InMemoryCheckpointStore();
        var strategy = new EveryNEpochsStrategy(1);
        var coordinator = new CheckpointCoordinator(store, strategy);
        
        var goodBlock = new CheckpointAwareMessageSource(100);
        var badBlock = new FailingCheckpointBlock();
        
        coordinator.RegisterBlock("good", goodBlock);
        coordinator.RegisterBlock("bad", badBlock);
        
        var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var epoch = new Epoch(new EpochVector(new Dictionary<string, long> { ["source1"] = 1 }), scopeFactory.CreateScope());
        
        // Act - should not throw despite bad block failing
        await coordinator.OnEpochCompletedAsync(epoch);
        
        var checkpoints = await store.ListCheckpointsAsync();
        
        // Assert - checkpoint still created with good block's state
        Assert.Single(checkpoints);
        var checkpoint = await store.GetCheckpointAsync(checkpoints[0]);
        Assert.NotNull(checkpoint);
        Assert.True(checkpoint.BlockStates.ContainsKey("good"));
        Assert.False(checkpoint.BlockStates.ContainsKey("bad")); // Failed block not included
    }
    
    [Fact]
    public async Task CheckpointCoordinator_ReturnsTrue_WhenCheckpointExists()
    {
        // Arrange
        var store = new InMemoryCheckpointStore();
        var strategy = new EveryNEpochsStrategy(1);
        var coordinator = new CheckpointCoordinator(store, strategy);
        
        var source = new CheckpointAwareMessageSource(100);
        coordinator.RegisterBlock("source", source);
        
        // Create a checkpoint
        var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var epoch = new Epoch(new EpochVector(new Dictionary<string, long> { ["source1"] = 1 }), scopeFactory.CreateScope());
        await coordinator.OnEpochCompletedAsync(epoch);
        
        // Act
        var result = await coordinator.RestoreFromLatestCheckpointAsync();
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public async Task CheckpointCoordinator_ReturnsFalse_WhenNoCheckpointExists()
    {
        // Arrange
        var store = new InMemoryCheckpointStore();
        var strategy = new EveryNEpochsStrategy(1);
        var coordinator = new CheckpointCoordinator(store, strategy);
        
        // Act
        var result = await coordinator.RestoreFromLatestCheckpointAsync();
        
        // Assert
        Assert.False(result);
    }
}

/// <summary>
/// Test helper block that always fails checkpoint creation.
/// </summary>
internal class FailingCheckpointBlock : ICheckpointAware
{
    public Task<byte[]?> CreateCheckpointAsync(string checkpointId, EpochVector epochVector, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Simulated checkpoint failure");
    }
    
    public Task RestoreFromCheckpointAsync(string checkpointId, byte[] state, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
