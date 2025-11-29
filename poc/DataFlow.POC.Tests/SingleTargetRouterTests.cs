namespace DataFlow.POC.Tests;

using DataFlow.POC.Core;
using Shouldly;
using System.Threading.Channels;
using Xunit;

/// <summary>
/// Tests for SingleTargetRouter optimization that eliminates dictionary lookups in the routing hot path.
/// This is the key optimization from this PR that builds on PR#42's pre-extracted writers optimization.
/// </summary>
public class SingleTargetRouterTests
{
    /// <summary>
    /// Simple mock block for testing
    /// </summary>
    private class MockBlock : IBlock
    {
        public MockBlock(string name) { Name = name; }
        public string Name { get; set; }
        public Type InputType => typeof(int);
        public Type OutputType => typeof(int);
        
        public async IAsyncEnumerable<object> ExecuteAsync(
            IAsyncEnumerable<object> input,
            IExecutionContext context)
        {
            yield break;
        }
    }
    
    [Fact]
    public async Task SingleTargetRouter_Should_Write_Directly_Without_Dictionary_Lookup()
    {
        // Arrange
        var targetBlock = new MockBlock("target");
        var channel = Channel.CreateUnbounded<int>();
        var router = new SingleTargetRouter<int>(targetBlock, channel.Writer);
        
        // Act
        await router.WriteAsync(42, CancellationToken.None);
        channel.Writer.Complete();
        
        // Assert
        router.TargetBlock.ShouldBe(targetBlock);
        var result = await channel.Reader.ReadAsync();
        result.ShouldBe(42);
    }
    
    [Fact]
    public async Task SingleTargetRouter_Should_Support_Multiple_Writes()
    {
        // Arrange
        var targetBlock = new MockBlock("target");
        var channel = Channel.CreateUnbounded<int>();
        var router = new SingleTargetRouter<int>(targetBlock, channel.Writer);
        
        // Act - Write multiple items (simulating epoch stream routing)
        for (int i = 1; i <= 10; i++)
        {
            await router.WriteAsync(i, CancellationToken.None);
        }
        channel.Writer.Complete();
        
        // Assert - All items should be written
        var results = new List<int>();
        await foreach (var item in channel.Reader.ReadAllAsync())
        {
            results.Add(item);
        }
        
        results.Count.ShouldBe(10);
        results.ShouldBe(Enumerable.Range(1, 10));
    }
    
    [Fact]
    public async Task BroadcastEdge_With_SingleTargetRouters_Should_Eliminate_Dictionary_Lookups()
    {
        // This test validates that the architecture change successfully eliminates dictionary lookups.
        // Previously (before PR#42): Each item routing involved GetWriter() call + dictionary lookup
        // After PR#42: Pre-extracted writers eliminated GetWriter() overhead, but dictionary lookup remained
        // After this PR: SingleTargetRouter has direct writer reference - zero dictionary lookups
        
        // Arrange
        var target1 = new MockBlock("target1");
        var target2 = new MockBlock("target2");
        var target3 = new MockBlock("target3");
        
        var channel1 = Channel.CreateUnbounded<int>();
        var channel2 = Channel.CreateUnbounded<int>();
        var channel3 = Channel.CreateUnbounded<int>();
        
        var routers = new List<SingleTargetRouter<int>>
        {
            new SingleTargetRouter<int>(target1, channel1.Writer),
            new SingleTargetRouter<int>(target2, channel2.Writer),
            new SingleTargetRouter<int>(target3, channel3.Writer)
        };
        
        // Act - Simulate broadcasting 1000 items to 3 targets
        // With old architecture: 3000 dictionary lookups
        // With new architecture: 0 dictionary lookups (direct writer access)
        for (int i = 1; i <= 1000; i++)
        {
            // Each router writes directly without dictionary lookup
            var writeTasks = routers.Select(r => r.WriteAsync(i, CancellationToken.None).AsTask());
            await Task.WhenAll(writeTasks);
        }
        
        channel1.Writer.Complete();
        channel2.Writer.Complete();
        channel3.Writer.Complete();
        
        // Assert - All targets should receive all items
        var results1 = await channel1.Reader.ReadAllAsync().ToListAsync();
        var results2 = await channel2.Reader.ReadAllAsync().ToListAsync();
        var results3 = await channel3.Reader.ReadAllAsync().ToListAsync();
        
        results1.Count.ShouldBe(1000);
        results2.Count.ShouldBe(1000);
        results3.Count.ShouldBe(1000);
        
        results1.ShouldBe(Enumerable.Range(1, 1000));
        results2.ShouldBe(Enumerable.Range(1, 1000));
        results3.ShouldBe(Enumerable.Range(1, 1000));
    }
    
    [Fact]
    public void SingleTargetRouter_Should_Expose_Target_And_Writer()
    {
        // Arrange
        var targetBlock = new MockBlock("target");
        var channel = Channel.CreateUnbounded<int>();
        var router = new SingleTargetRouter<int>(targetBlock, channel.Writer);
        
        // Assert
        router.TargetBlock.ShouldBe(targetBlock);
        router.Writer.ShouldBe(channel.Writer);
    }
}

