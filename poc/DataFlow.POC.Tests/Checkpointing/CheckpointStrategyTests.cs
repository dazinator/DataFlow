namespace DataFlow.POC.Tests.Checkpointing;

using DataFlow.POC.Checkpointing;
using DataFlow.POC.Checkpointing.Strategies;
using DataFlow.POC.Core;
using Xunit;

public class CheckpointStrategyTests
{
    [Fact]
    public void EveryNEpochsStrategy_Constructor_ThrowsOnZero()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new EveryNEpochsStrategy(0));
    }

    [Fact]
    public void EveryNEpochsStrategy_Constructor_ThrowsOnNegative()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new EveryNEpochsStrategy(-1));
    }

    [Fact]
    public void EveryNEpochsStrategy_CheckpointsEveryNEpochs()
    {
        // Arrange
        var strategy = new EveryNEpochsStrategy(3);
        var vectors = new[]
        {
            EpochVector.FromSingleSource("s1", 1),
            EpochVector.FromSingleSource("s1", 2),
            EpochVector.FromSingleSource("s1", 3),
            EpochVector.FromSingleSource("s1", 4),
            EpochVector.FromSingleSource("s1", 5),
            EpochVector.FromSingleSource("s1", 6),
        };

        // Act & Assert
        Assert.False(strategy.ShouldCreateCheckpoint(vectors[0])); // 1st epoch - no checkpoint
        Assert.False(strategy.ShouldCreateCheckpoint(vectors[1])); // 2nd epoch - no checkpoint
        Assert.True(strategy.ShouldCreateCheckpoint(vectors[2]));  // 3rd epoch - checkpoint!
        Assert.False(strategy.ShouldCreateCheckpoint(vectors[3])); // 4th epoch - no checkpoint
        Assert.False(strategy.ShouldCreateCheckpoint(vectors[4])); // 5th epoch - no checkpoint
        Assert.True(strategy.ShouldCreateCheckpoint(vectors[5]));  // 6th epoch - checkpoint!
    }

    [Fact]
    public void EveryNEpochsStrategy_CheckpointsEveryEpochWhenNIsOne()
    {
        // Arrange
        var strategy = new EveryNEpochsStrategy(1);
        var vectors = new[]
        {
            EpochVector.FromSingleSource("s1", 1),
            EpochVector.FromSingleSource("s1", 2),
            EpochVector.FromSingleSource("s1", 3),
        };

        // Act & Assert
        Assert.True(strategy.ShouldCreateCheckpoint(vectors[0]));
        Assert.True(strategy.ShouldCreateCheckpoint(vectors[1]));
        Assert.True(strategy.ShouldCreateCheckpoint(vectors[2]));
    }

    [Fact]
    public void TimeBasedStrategy_Constructor_ThrowsOnZeroInterval()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeBasedStrategy(TimeSpan.Zero));
    }

    [Fact]
    public void TimeBasedStrategy_Constructor_ThrowsOnNegativeInterval()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeBasedStrategy(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void TimeBasedStrategy_CheckpointsAfterInterval()
    {
        // Arrange
        var strategy = new TimeBasedStrategy(TimeSpan.FromMilliseconds(100));
        var vector = EpochVector.FromSingleSource("s1", 1);

        // Act & Assert
        // First call should checkpoint (initial checkpoint)
        Assert.True(strategy.ShouldCreateCheckpoint(vector));

        // Immediate second call should not checkpoint (interval not elapsed)
        Assert.False(strategy.ShouldCreateCheckpoint(vector));

        // Wait for interval to elapse
        Thread.Sleep(150);

        // Now it should checkpoint again
        Assert.True(strategy.ShouldCreateCheckpoint(vector));
    }

    [Fact]
    public void TimeBasedStrategy_ThreadSafe()
    {
        // Arrange
        var strategy = new TimeBasedStrategy(TimeSpan.FromMilliseconds(50));
        var vector = EpochVector.FromSingleSource("s1", 1);
        var checkpointCount = 0;
        var tasks = new List<Task>();

        // Act - Call from multiple threads
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                if (strategy.ShouldCreateCheckpoint(vector))
                {
                    Interlocked.Increment(ref checkpointCount);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - Only one thread should have gotten true
        Assert.Equal(1, checkpointCount);
    }
}
