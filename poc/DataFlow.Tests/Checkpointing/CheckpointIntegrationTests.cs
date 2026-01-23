namespace DataFlow.POC.Tests.Checkpointing;

using System.Text.Json;
using DataFlow.POC.Checkpointing;
using DataFlow.POC.Checkpointing.Strategies;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DataFlow.POC.Registry;

public class CheckpointIntegrationTests
{
    [Fact]
    public async Task Epoch_WithCheckpoint_IsCheckpointingIsTrue()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<TestService>();
        var provider = services.BuildServiceProvider();

        var strategy = new EveryNEpochsStrategy(1); // Checkpoint every epoch
        var coordinator = new EpochCoordinator(provider.GetRequiredService<IServiceScopeFactory>(), checkpointStrategy: strategy);

        // Act
        var epoch = await coordinator.GetOrCreateEpochAsync("source1", EpochVector.FromSingleSource("source1", 1));

        // Assert
        Assert.True(epoch.IsCheckpointing);
    }

    [Fact]
    public async Task Epoch_WithoutCheckpoint_IsCheckpointingIsFalse()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<TestService>();
        var provider = services.BuildServiceProvider();

        var coordinator = new EpochCoordinator(provider.GetRequiredService<IServiceScopeFactory>()); // No strategy

        // Act
        var epoch = await coordinator.GetOrCreateEpochAsync("source1", EpochVector.FromSingleSource("source1", 1));

        // Assert
        Assert.False(epoch.IsCheckpointing);
    }

    [Fact]
    public async Task Epoch_CheckpointStrategy_CheckpointsAtCorrectIntervals()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<TestService>();
        var provider = services.BuildServiceProvider();

        var strategy = new EveryNEpochsStrategy(2); // Checkpoint every 2 epochs
        var coordinator = new EpochCoordinator(provider.GetRequiredService<IServiceScopeFactory>(), checkpointStrategy: strategy);

        // Act & Assert
        var epoch1 = await coordinator.GetOrCreateEpochAsync("source1", EpochVector.FromSingleSource("source1", 1));
        Assert.False(epoch1.IsCheckpointing); // 1st epoch - no checkpoint

        var epoch2 = await coordinator.GetOrCreateEpochAsync("source1", EpochVector.FromSingleSource("source1", 2));
        Assert.True(epoch2.IsCheckpointing); // 2nd epoch - checkpoint!

        var epoch3 = await coordinator.GetOrCreateEpochAsync("source1", EpochVector.FromSingleSource("source1", 3));
        Assert.False(epoch3.IsCheckpointing); // 3rd epoch - no checkpoint

        var epoch4 = await coordinator.GetOrCreateEpochAsync("source1", EpochVector.FromSingleSource("source1", 4));
        Assert.True(epoch4.IsCheckpointing); // 4th epoch - checkpoint!
    }

    [Fact]
    public async Task Epoch_WithCheckpoint_BlockCanContributeState()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<TestService>();
        var provider = services.BuildServiceProvider();

        var strategy = new EveryNEpochsStrategy(1);
        var coordinator = new EpochCoordinator(provider.GetRequiredService<IServiceScopeFactory>(), checkpointStrategy: strategy);
        var epoch = await coordinator.GetOrCreateEpochAsync("source1", EpochVector.FromSingleSource("source1", 1));

        var blockState = JsonSerializer.SerializeToElement(new { offset = 100 });
        ICheckpoint? capturedCheckpoint = null;

        // Act - Queue operation that contributes to checkpoint
        await epoch.QueueSerializedOperationAsync<TestService>(async (svc, ctx) =>
        {
            ctx.Checkpoint?.AddBlockState("test-block", blockState);
            capturedCheckpoint = ctx.Checkpoint;
            await Task.CompletedTask;
        });

        // Process the operation
        var source = new EpochSourceNode();
        var processor = new EpochProcessorNode(source);
        
        // Publish the epoch
        await source.PublishEpochAsync(epoch);
        
        // Signal epoch completion
        coordinator.SignalReadyForNext("source1", 
            EpochVector.FromSingleSource("source1", 1),
            EpochVector.FromSingleSource("source1", 2));

        // Complete the source
        source.SignalCompletion();

        // Wait a bit for processing
        await Task.Delay(200);

        // Assert
        Assert.NotNull(capturedCheckpoint);
        Assert.Single(capturedCheckpoint.BlockStates);
        Assert.Equal(blockState, capturedCheckpoint.BlockStates["test-block"]);

        // Cleanup
        await processor.DisposeAsync();
    }

    [Fact]
    public async Task Epoch_WithCheckpoint_MultipleBlocksCanContributeState()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<TestService>();
        var provider = services.BuildServiceProvider();

        var strategy = new EveryNEpochsStrategy(1);
        var coordinator = new EpochCoordinator(provider.GetRequiredService<IServiceScopeFactory>(), checkpointStrategy: strategy);
        var epoch = await coordinator.GetOrCreateEpochAsync("source1", EpochVector.FromSingleSource("source1", 1));

        var block1State = JsonSerializer.SerializeToElement(new { block = "block1", state = "state" });
        var block2State = JsonSerializer.SerializeToElement(new { block = "block2", state = "state" });
        var block3State = JsonSerializer.SerializeToElement(new { block = "block3", state = "state" });
        ICheckpoint? capturedCheckpoint = null;

        // Act - Multiple blocks contribute to checkpoint
        await epoch.QueueSerializedOperationAsync<TestService>(async (svc, ctx) =>
        {
            ctx.Checkpoint?.AddBlockState("block1", block1State);
            await Task.CompletedTask;
        });

        await epoch.QueueSerializedOperationAsync<TestService>(async (svc, ctx) =>
        {
            ctx.Checkpoint?.AddBlockState("block2", block2State);
            await Task.CompletedTask;
        });

        await epoch.QueueSerializedOperationAsync<TestService>(async (svc, ctx) =>
        {
            ctx.Checkpoint?.AddBlockState("block3", block3State);
            capturedCheckpoint = ctx.Checkpoint;
            await Task.CompletedTask;
        });

        // Process the operations
        var source = new EpochSourceNode();
        var processor = new EpochProcessorNode(source);
        
        // Publish the epoch
        await source.PublishEpochAsync(epoch);
        
        coordinator.SignalReadyForNext("source1", 
            EpochVector.FromSingleSource("source1", 1),
            EpochVector.FromSingleSource("source1", 2));

        source.SignalCompletion();
        await Task.Delay(200);

        // Assert
        Assert.NotNull(capturedCheckpoint);
        Assert.Equal(3, capturedCheckpoint.BlockStates.Count);
        Assert.Equal(block1State, capturedCheckpoint.BlockStates["block1"]);
        Assert.Equal(block2State, capturedCheckpoint.BlockStates["block2"]);
        Assert.Equal(block3State, capturedCheckpoint.BlockStates["block3"]);

        // Cleanup
        await processor.DisposeAsync();
    }

    [Fact]
    public async Task Epoch_WithoutCheckpoint_ContextCheckpointIsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<TestService>();
        var provider = services.BuildServiceProvider();

        var coordinator = new EpochCoordinator(provider.GetRequiredService<IServiceScopeFactory>()); // No strategy
        var epoch = await coordinator.GetOrCreateEpochAsync("source1", EpochVector.FromSingleSource("source1", 1));

        ICheckpoint? capturedCheckpoint = null;

        // Act - Queue operation
        await epoch.QueueSerializedOperationAsync<TestService>(async (svc, ctx) =>
        {
            capturedCheckpoint = ctx.Checkpoint;
            await Task.CompletedTask;
        });

        // Process the operation
        var source = new EpochSourceNode();
        var processor = new EpochProcessorNode(source);
        
        // Publish the epoch
        await source.PublishEpochAsync(epoch);
        
        coordinator.SignalReadyForNext("source1", 
            EpochVector.FromSingleSource("source1", 1),
            EpochVector.FromSingleSource("source1", 2));

        source.SignalCompletion();
        await Task.Delay(200);

        // Assert
        Assert.Null(capturedCheckpoint);

        // Cleanup
        await processor.DisposeAsync();
    }

    [Fact]
    public async Task Epoch_BackwardCompatibility_OldSignatureStillWorks()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<TestService>();
        var provider = services.BuildServiceProvider();

        var coordinator = new EpochCoordinator(provider.GetRequiredService<IServiceScopeFactory>());
        var epoch = await coordinator.GetOrCreateEpochAsync("source1", EpochVector.FromSingleSource("source1", 1));

        var operationExecuted = false;

        // Act - Use old signature without context
        await epoch.QueueSerializedOperationAsync<TestService>(async (svc) =>
        {
            operationExecuted = true;
            await Task.CompletedTask;
        });

        // Process the operation
        var source = new EpochSourceNode();
        var processor = new EpochProcessorNode(source);
        
        // Publish the epoch
        await source.PublishEpochAsync(epoch);
        
        coordinator.SignalReadyForNext("source1", 
            EpochVector.FromSingleSource("source1", 1),
            EpochVector.FromSingleSource("source1", 2));

        source.SignalCompletion();
        await Task.Delay(200);

        // Assert
        Assert.True(operationExecuted);

        // Cleanup
        await processor.DisposeAsync();
    }

    private class TestService
    {
    }
}
