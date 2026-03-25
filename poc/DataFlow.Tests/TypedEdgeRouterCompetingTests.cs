namespace DataFlow.POC.Tests;

using System.Threading.Channels;
using DataFlow.POC.Core;
using Shouldly;
using Xunit;

/// <summary>
/// Unit tests for <see cref="TypedEdgeRouter{T}"/> competing-edge counting.
/// Verifies that <see cref="ITypedEdgeRouter.GetItemsWrittenToTarget"/> reports the
/// same shared write-count for every competing target, regardless of which target's
/// channel reader actually dequeues the item.
/// </summary>
public class TypedEdgeRouterCompetingTests
{
    private static (IBlock block, Channel<int> channel) MakeTarget(string name)
    {
        var block   = new MockBlock(name);
        var channel = Channel.CreateUnbounded<int>();
        return (block, channel);
    }

    [Fact]
    public async Task GetItemsWrittenToTarget_CompetingEdge_ReturnsSharedCountForAllTargets()
    {
        // Arrange — three competing targets, each backed by its own channel reader
        // but sharing one CountingChannelWriter on the first writer.
        var (block1, ch1) = MakeTarget("worker-1");
        var (block2, ch2) = MakeTarget("worker-2");
        var (block3, ch3) = MakeTarget("worker-3");

        var writers = new Dictionary<IBlock, object>
        {
            [block1] = ch1.Writer,
            [block2] = ch2.Writer,
            [block3] = ch3.Writer,
        };

        var edge = new Edge(
            new MockBlock("producer"),
            new[] { block1, block2, block3 },
            new CompetingEdgeStrategy(BufferMode.Bounded, 10));

        var router = new TypedEdgeRouter<int>(edge, writers);

        // Act — write 5 items through the router
        for (int i = 1; i <= 5; i++)
            await router.RouteItemAsync(i, CancellationToken.None);

        // Assert — all three targets must report the same total write count
        // because competing targets share a single CountingChannelWriter.
        var count1 = router.GetItemsWrittenToTarget(block1);
        var count2 = router.GetItemsWrittenToTarget(block2);
        var count3 = router.GetItemsWrittenToTarget(block3);

        count1.ShouldBe(5, "worker-1 should reflect total writes through the shared channel");
        count2.ShouldBe(5, "worker-2 should reflect total writes through the shared channel");
        count3.ShouldBe(5, "worker-3 should reflect total writes through the shared channel");
    }

    [Fact]
    public async Task GetItemsWrittenToTarget_BroadcastEdge_ReturnsIndividualCountPerTarget()
    {
        // Sanity-check that the broadcast path is NOT affected: each target keeps its own count.
        var (block1, ch1) = MakeTarget("sink-1");
        var (block2, ch2) = MakeTarget("sink-2");

        var writers = new Dictionary<IBlock, object>
        {
            [block1] = ch1.Writer,
            [block2] = ch2.Writer,
        };

        var edge = new Edge(
            new MockBlock("producer"),
            new[] { block1, block2 },
            new BroadcastEdgeStrategy(BufferMode.Bounded, 10));

        var router = new TypedEdgeRouter<int>(edge, writers);

        for (int i = 1; i <= 3; i++)
            await router.RouteItemAsync(i, CancellationToken.None);

        router.GetItemsWrittenToTarget(block1).ShouldBe(3);
        router.GetItemsWrittenToTarget(block2).ShouldBe(3);
    }

    // ---------------------------------------------------------------------------
    // Minimal IBlock stub — mirrors the pattern used in SingleTargetRouterTests.
    // ---------------------------------------------------------------------------
    private sealed class MockBlock : IBlock
    {
        public MockBlock(string name) => Name = name;
        public string Name { get; }
        public Type InputType  => typeof(int);
        public Type OutputType => typeof(int);

        public async IAsyncEnumerable<object> ExecuteAsync(
            IAsyncEnumerable<object> input,
            IExecutionContext context)
        {
            yield break;
        }
    }
}
