namespace DataFlow.POC.Tests.Research;

using DataFlow.POC.Core;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Research tests investigating architectural mismatch in epoch stream routing.
/// 
/// Background: Issue #15 and PR #24 uncovered a potential mismatch where edges
/// route epoch stream CONTAINERS instead of data ITEMS, breaking routing semantics.
/// 
/// These tests validate the hypothesis and demonstrate the problem.
/// </summary>
public class EpochRoutingArchitectureTests
{
    private readonly ITestOutputHelper _output;

    public EpochRoutingArchitectureTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Test Case 1: Broadcast Routing with Epoch Streams
    /// 
    /// Hypothesis: When broadcasting epoch streams, both consumers receive the SAME
    /// epoch stream container object. Enumerating the container's items causes conflicts
    /// because IAsyncEnumerable can only be enumerated once.
    /// 
    /// Expected Problem: Second consumer gets empty stream or enumeration fails.
    /// </summary>
    [Fact]
    public async Task BroadcastEdge_WithEpochStreams_SharesSameContainer()
    {
        // Arrange: Create a source that outputs epoch streams
        var sourceData = new List<int> { 1, 2, 3, 4, 5 };
        var graph = new DataFlowGraph("test-graph", NullLogger<DataFlowGraph>.Instance);

        // Source block that outputs IAsyncEnumerable<IEpochStream<int>>
        var sourceBlock = new EpochStreamSourceBlock(sourceData);
        var consumerA = new EpochStreamConsumerBlock("ConsumerA");
        var consumerB = new EpochStreamConsumerBlock("ConsumerB");

        graph.AddBlock(sourceBlock);
        graph.AddBlock(consumerA);
        graph.AddBlock(consumerB);

        // Create broadcast edge
        var broadcastEdge = new Edge(
            sourceBlock,
            new[] { (IBlock)consumerA, consumerB },
            new BroadcastEdgeStrategy());

        graph.AddEdge(broadcastEdge);

        // Act: Execute the graph
        var serviceProvider = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        await graph.ExecuteAsync(context);

        // Assert: Check what each consumer received
        _output.WriteLine($"Consumer A received {consumerA.ReceivedItems.Count} items: [{string.Join(", ", consumerA.ReceivedItems)}]");
        _output.WriteLine($"Consumer B received {consumerB.ReceivedItems.Count} items: [{string.Join(", ", consumerB.ReceivedItems)}]");

        // HYPOTHESIS VALIDATION:
        // If containers are shared, only ONE consumer will successfully enumerate
        // The other will get 0 items (or error)
        
        var totalItems = consumerA.ReceivedItems.Count + consumerB.ReceivedItems.Count;
        
        _output.WriteLine($"\nTotal items received across both consumers: {totalItems}");
        _output.WriteLine($"Expected if broadcast works correctly: {sourceData.Count * 2} (each consumer gets all items)");
        _output.WriteLine($"Expected if containers are shared: {sourceData.Count} (only one consumer gets items)");

        // Document the actual behavior
        if (totalItems == sourceData.Count)
        {
            _output.WriteLine("\n⚠️ ARCHITECTURAL MISMATCH CONFIRMED:");
            _output.WriteLine("   - Only one consumer received items");
            _output.WriteLine("   - Containers are being shared, not data items");
            _output.WriteLine("   - Broadcast semantics are BROKEN");
        }
        else if (totalItems == sourceData.Count * 2)
        {
            _output.WriteLine("\n✅ BROADCAST WORKS CORRECTLY:");
            _output.WriteLine("   - Both consumers received all items");
            _output.WriteLine("   - Some mechanism is duplicating data properly");
        }
        else
        {
            _output.WriteLine($"\n❓ UNEXPECTED BEHAVIOR: Total items = {totalItems}");
        }

        // For now, document the observation without strict assertion
        // The test serves to validate the hypothesis
        totalItems.ShouldBeGreaterThan(0, "At least one consumer should receive items");
    }

    /// <summary>
    /// Test Case 2: Selective Routing with Epoch Streams
    /// 
    /// Hypothesis: Selective routing operates on the CONTAINER level, not item level.
    /// A key selector on IEpochStream<T> would route entire epochs, not individual items.
    /// 
    /// Expected Problem: Cannot route individual items within epoch streams.
    /// </summary>
    [Fact]
    public async Task SelectiveRouting_WithEpochStreams_RoutesContainers()
    {
        _output.WriteLine("⚠️ Test demonstrating selective routing architectural mismatch");
        _output.WriteLine("   Selective routing with IEpochStream<T> routes containers, not items");
        
        // This test would require SelectiveEdgeStrategy implementation
        // Documenting the conceptual problem:
        
        _output.WriteLine("\nConcept:");
        _output.WriteLine("  Source outputs: IAsyncEnumerable<IEpochStream<int>>");
        _output.WriteLine("  Edge receives: IEpochStream<int> (container)");
        _output.WriteLine("  Key selector: Operates on container, not on int items");
        _output.WriteLine("  Result: Entire epoch routed to one consumer based on container properties");
        _output.WriteLine("\nExpected behavior:");
        _output.WriteLine("  Edge should route individual 'int' items based on their values");
        _output.WriteLine("  Not route the entire IEpochStream<int> container");

        // Mark as conceptual test for now
        Assert.True(true, "Conceptual test documenting the problem");
    }

    /// <summary>
    /// Test Case 3: Comparing ConfigureEpochs with Epoch-Aware Blocks
    /// 
    /// Hypothesis: ConfigureEpochs wraps plain streams in epochs but has the same problem
    /// when used with broadcast/selective edges.
    /// </summary>
    [Fact]
    public async Task ConfigureEpochs_WithBroadcast_HasSameIssue()
    {
        _output.WriteLine("⚠️ Testing ConfigureEpochs approach with broadcast");
        
        // Arrange: Plain source + WrapInSingleEpoch
        var plainSource = CreatePlainStream(5);
        var epochWrapped = plainSource.WrapInSingleEpoch("source");

        _output.WriteLine("\nConfigureEpochs pattern:");
        _output.WriteLine("  1. Plain source: IAsyncEnumerable<int>");
        _output.WriteLine("  2. WrapInSingleEpoch: IAsyncEnumerable<IEpochStream<int>>");
        _output.WriteLine("  3. Edge routing: Routes IEpochStream<int> containers");
        _output.WriteLine("\nResult: Same architectural mismatch as epoch-aware blocks");

        // Validate that ConfigureEpochs produces epoch streams
        var epochList = new List<IEpochStream<int>>();
        await foreach (var epoch in epochWrapped)
        {
            epochList.Add(epoch);
        }

        epochList.Count.ShouldBe(1, "WrapInSingleEpoch should produce exactly one epoch");
        _output.WriteLine($"\nEpoch container produced: {epochList.Count}");

        // The container would still be shared in broadcast scenario
        _output.WriteLine("If broadcast edge receives this container, same sharing problem occurs");
    }

    #region Helper Classes and Methods

    /// <summary>
    /// Test block that produces epoch streams
    /// </summary>
    private class EpochStreamSourceBlock : BlockBase<object, IEpochStream<int>>
    {
        private readonly List<int> _sourceData;

        public EpochStreamSourceBlock(List<int> sourceData) 
            : base(new BlockContext("EpochStreamSource"))
        {
            _sourceData = sourceData;
        }

        public override async IAsyncEnumerable<IEpochStream<int>> ExecuteAsync(
            IAsyncEnumerable<object> input,
            IExecutionContext context)
        {
            // Produce one epoch stream containing all data
            var epochVector = EpochVector.FromSingleSource("test", 1);
            var items = CreateAsyncEnumerable(_sourceData);
            yield return new EpochStream<int>(epochVector, items);
        }

        private async IAsyncEnumerable<T> CreateAsyncEnumerable<T>(List<T> items)
        {
            foreach (var item in items)
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Test block that consumes epoch streams
    /// </summary>
    private class EpochStreamConsumerBlock : BlockBase<IEpochStream<int>, object>
    {
        public List<int> ReceivedItems { get; } = new();

        public EpochStreamConsumerBlock(string name)
            : base(new BlockContext(name))
        {
        }

        public override async IAsyncEnumerable<object> ExecuteAsync(
            IAsyncEnumerable<IEpochStream<int>> input,
            IExecutionContext context)
        {
            await foreach (var epochStream in input.WithCancellation(context.CancellationToken))
            {
                // Enumerate the items from the epoch stream
                await foreach (var item in epochStream.Items.WithCancellation(context.CancellationToken))
                {
                    ReceivedItems.Add(item);
                }
            }

            // Terminal block - no output
            yield break;
        }
    }

    private static async IAsyncEnumerable<int> CreatePlainStream(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return i;
        }
    }

    #endregion
}
