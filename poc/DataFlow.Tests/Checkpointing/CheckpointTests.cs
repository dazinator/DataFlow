namespace DataFlow.POC.Tests.Checkpointing;

using System.Text.Json;
using DataFlow.POC.Checkpointing;
using DataFlow.POC.Core;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Registry;

public class CheckpointTests
{
    [Fact]
    public void Checkpoint_Constructor_SetsProperties()
    {
        // Arrange
        var checkpointId = "test-checkpoint-1";
        var vector = EpochVector.FromSingleSource("source1", 1);
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var checkpoint = new Checkpoint(checkpointId, vector, timestamp);

        // Assert
        Assert.Equal(checkpointId, checkpoint.CheckpointId);
        Assert.Equal(vector, checkpoint.EpochVector);
        Assert.Equal(timestamp, checkpoint.Timestamp);
        Assert.Empty(checkpoint.BlockStates);
    }

    [Fact]
    public void Checkpoint_Constructor_ThrowsOnNullCheckpointId()
    {
        // Arrange
        var vector = EpochVector.FromSingleSource("source1", 1);
        var timestamp = DateTimeOffset.UtcNow;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Checkpoint(null!, vector, timestamp));
    }

    [Fact]
    public void Checkpoint_Constructor_ThrowsOnNullEpochVector()
    {
        // Arrange
        var checkpointId = "test-checkpoint-1";
        var timestamp = DateTimeOffset.UtcNow;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Checkpoint(checkpointId, null!, timestamp));
    }

    [Fact]
    public void AddBlockState_AddsStateSuccessfully()
    {
        // Arrange
        var checkpoint = new Checkpoint("test", EpochVector.FromSingleSource("s1", 1), DateTimeOffset.UtcNow);
        var state = JsonSerializer.SerializeToElement(new { offset = 123 });

        // Act
        checkpoint.AddBlockState("block1", state);

        // Assert
        Assert.Single(checkpoint.BlockStates);
        Assert.True(checkpoint.BlockStates.ContainsKey("block1"));
        Assert.Equal(state.GetRawText(), checkpoint.BlockStates["block1"].GetRawText());
    }

    [Fact]
    public void AddBlockState_SupportsMultipleBlocks()
    {
        // Arrange
        var checkpoint = new Checkpoint("test", EpochVector.FromSingleSource("s1", 1), DateTimeOffset.UtcNow);
        var state1 = JsonSerializer.SerializeToElement(new { offset = 1 });
        var state2 = JsonSerializer.SerializeToElement(new { offset = 2 });
        var state3 = JsonSerializer.SerializeToElement(new { offset = 3 });

        // Act
        checkpoint.AddBlockState("block1", state1);
        checkpoint.AddBlockState("block2", state2);
        checkpoint.AddBlockState("block3", state3);

        // Assert
        Assert.Equal(3, checkpoint.BlockStates.Count);
        Assert.Equal(state1.GetRawText(), checkpoint.BlockStates["block1"].GetRawText());
        Assert.Equal(state2.GetRawText(), checkpoint.BlockStates["block2"].GetRawText());
        Assert.Equal(state3.GetRawText(), checkpoint.BlockStates["block3"].GetRawText());
    }

    [Fact]
    public void AddBlockState_ThrowsOnDuplicateBlockId()
    {
        // Arrange
        var checkpoint = new Checkpoint("test", EpochVector.FromSingleSource("s1", 1), DateTimeOffset.UtcNow);
        var state1 = JsonSerializer.SerializeToElement(new { offset = 1 });
        var state2 = JsonSerializer.SerializeToElement(new { offset = 2 });

        // Act
        checkpoint.AddBlockState("block1", state1);

        // Assert
        var ex = Assert.Throws<InvalidOperationException>(() => checkpoint.AddBlockState("block1", state2));
        Assert.Contains("block1", ex.Message);
        Assert.Contains("already been added", ex.Message);
    }

    [Fact]
    public void AddBlockState_ThrowsOnNullBlockId()
    {
        // Arrange
        var checkpoint = new Checkpoint("test", EpochVector.FromSingleSource("s1", 1), DateTimeOffset.UtcNow);
        var state = JsonSerializer.SerializeToElement(new { offset = 123 });

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => checkpoint.AddBlockState(null!, state));
    }


    [Fact]
    public void BlockStates_IsReadOnly()
    {
        // Arrange
        var checkpoint = new Checkpoint("test", EpochVector.FromSingleSource("s1", 1), DateTimeOffset.UtcNow);
        checkpoint.AddBlockState("block1", JsonSerializer.SerializeToElement(new { offset = 1 }));

        // Act & Assert
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, JsonElement>>(checkpoint.BlockStates);
    }

    [Fact]
    public void TryGetBlockState_ReturnsTrue_WhenBlockExists()
    {
        // Arrange
        var checkpoint = new Checkpoint("test", EpochVector.FromSingleSource("s1", 1), DateTimeOffset.UtcNow);
        var state = JsonSerializer.SerializeToElement(new { offset = 123 });
        checkpoint.AddBlockState("block1", state);

        // Act
        var found = checkpoint.TryGetBlockState("block1", out var retrievedState);

        // Assert
        Assert.True(found);
        Assert.Equal(state.GetRawText(), retrievedState.GetRawText());
    }

    [Fact]
    public void TryGetBlockState_ReturnsFalse_WhenBlockDoesNotExist()
    {
        // Arrange
        var checkpoint = new Checkpoint("test", EpochVector.FromSingleSource("s1", 1), DateTimeOffset.UtcNow);

        // Act
        var found = checkpoint.TryGetBlockState("nonexistent", out var state);

        // Assert
        Assert.False(found);
        Assert.Equal(default(JsonElement), state);
    }
}
