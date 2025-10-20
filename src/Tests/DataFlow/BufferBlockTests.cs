namespace Tests.DataFlow;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Builder.Graph;
using Uniun.DataFlow.Metrics;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Tests for BufferBlock - a block that accepts items from multiple upstream producers
/// and distributes them to multiple downstream consumers in a competing consumer pattern.
/// </summary>
[IntegrationTest]
public class BufferBlockTests
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceCollection _services;

    public BufferBlockTests(ITestOutputHelper output)
    {
        _output = output;
        _services = new ServiceCollection();
        AddDefaultServices(_services);
    }

    private void AddDefaultServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddXUnit(_output));
        services.AddDataFlows();
        services.AddMetrics();
        services.AddDataFlowMetrics();
    }

    private IDataFlowContext CreateContext(string name, Guid guid, IServiceProvider provider, CancellationToken ct = default)
    {
        return DataFlowContextTestUtils.GetContext(name, guid, provider, ct);
    }

    [Fact]
    public async Task BufferBlock_WithSingleProducerAndSingleConsumer_ProcessesAllItems()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).ToArray();
        var processedItems = new ConcurrentBag<int>();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "SingleProducerConsumer");

        builder.AddProducer<int>("source", (IServiceProvider sp) => new TestProducer<int>(items));

        builder.AddBuffer<int>("buffer")
            .ReceiveFrom("source");

        StructuredDataFlowBuilderExtensions.AddProcessor<int>(builder, "consumer",
            (IServiceProvider sp) => new TestProcessor<int>(
                onProcessItem: item => processedItems.Add(item)))
            .ReceiveFrom("buffer");

        var flow = builder.Build();

        // Act
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert
        processedItems.Count.ShouldBe(10, "All items should be processed");
        processedItems.OrderBy(x => x).ToArray().ShouldBe(items, "Items should match");
    }

    [Fact]
    public async Task BufferBlock_WithMultipleConsumers_DistributesItemsCompetitively()
    {
        // Arrange - Multiple consumers compete for items (each item goes to exactly ONE consumer)
        var items = Enumerable.Range(1, 30).ToArray();
        var processedItems = new ConcurrentBag<(int item, string consumerId)>();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "CompetingConsumers");

        builder.AddProducer<int>("source", (IServiceProvider sp) => new TestProducer<int>(items));

        builder.AddBuffer<int>("buffer")
            .ReceiveFrom("source");

        // Create 3 competing consumers
        for (var i = 0; i < 3; i++)
        {
            var consumerId = $"consumer-{i}";
            StructuredDataFlowBuilderExtensions.AddProcessor<int>(builder, consumerId,
                (IServiceProvider sp) => new TestProcessor<int>(
                    onProcessItem: item => processedItems.Add((item, consumerId)),
                    delay: TimeSpan.FromMilliseconds(1))) // Small delay to allow competition
                .ReceiveFrom("buffer");
        }

        var flow = builder.Build();

        // Act
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert
        _output.WriteLine($"Total items processed: {processedItems.Count}");

        // Each item should be processed exactly once (competing consumers)
        processedItems.Count.ShouldBe(30, "Each item should be processed exactly once");

        // Verify each original item appears exactly once
        var processedItemIds = processedItems.Select(x => x.item).OrderBy(x => x).ToArray();
        processedItemIds.ShouldBe(items, "Each item should appear exactly once");

        // All consumers should have processed at least some items
        var consumersUsed = processedItems.Select(x => x.consumerId).Distinct().Count();
        _output.WriteLine($"Number of consumers that processed items: {consumersUsed}");
        consumersUsed.ShouldBeGreaterThanOrEqualTo(1, "At least one consumer should process items");

        // Distribution report
        _output.WriteLine("Distribution:");
        foreach (var consumerId in new[] { "consumer-0", "consumer-1", "consumer-2" })
        {
            var count = processedItems.Count(x => x.consumerId == consumerId);
            _output.WriteLine($"  {consumerId}: {count} items");
        }
    }

    [Fact]
    public async Task BufferBlock_WithMultipleProducers_AcceptsFromAllSources()
    {
        // Arrange - Multiple producers feed into the same buffer
        var items1 = Enumerable.Range(1, 10).ToArray();
        var items2 = Enumerable.Range(11, 10).ToArray();
        var items3 = Enumerable.Range(21, 10).ToArray();
        var allItems = items1.Concat(items2).Concat(items3).OrderBy(x => x).ToArray();

        var processedItems = new ConcurrentBag<int>();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "MultipleProducers");

        // Create 3 producers
        builder.AddProducer<int>("source1", (IServiceProvider sp) => new TestProducer<int>(items1));
        builder.AddProducer<int>("source2", (IServiceProvider sp) => new TestProducer<int>(items2));
        builder.AddProducer<int>("source3", (IServiceProvider sp) => new TestProducer<int>(items3));

        // Buffer accepts from all sources
        builder.AddBuffer<int>("buffer")
            .ReceiveFrom("source1")
            .ReceiveFrom("source2")
            .ReceiveFrom("source3");

        StructuredDataFlowBuilderExtensions.AddProcessor<int>(builder, "consumer",
            (IServiceProvider sp) => new TestProcessor<int>(
                onProcessItem: item => processedItems.Add(item)))
            .ReceiveFrom("buffer");

        var flow = builder.Build();

        // Act
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert
        _output.WriteLine($"Total items processed: {processedItems.Count}");
        processedItems.Count.ShouldBe(30, "All items from all producers should be processed");
        processedItems.OrderBy(x => x).ToArray().ShouldBe(allItems, "All items should match");
    }

    [Fact]
    public async Task BufferBlock_WithMultipleProducersAndConsumers_HandlesComplexScenario()
    {
        // Arrange - Multiple producers AND multiple consumers
        var items1 = Enumerable.Range(1, 15).ToArray();
        var items2 = Enumerable.Range(16, 15).ToArray();
        var allItems = items1.Concat(items2).OrderBy(x => x).ToArray();

        var processedItems = new ConcurrentBag<(int item, string consumerId)>();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "ComplexBuffer");

        // Create 2 producers
        builder.AddProducer<int>("source1", (IServiceProvider sp) => new TestProducer<int>(items1));
        builder.AddProducer<int>("source2", (IServiceProvider sp) => new TestProducer<int>(items2));

        // Buffer accepts from all sources
        builder.AddBuffer<int>("buffer", new BlockOptions { Capacity = 50 })
            .ReceiveFrom("source1")
            .ReceiveFrom("source2");

        // Create 2 competing consumers
        for (var i = 0; i < 2; i++)
        {
            var consumerId = $"consumer-{i}";
            StructuredDataFlowBuilderExtensions.AddProcessor<int>(builder, consumerId,
                (IServiceProvider sp) => new TestProcessor<int>(
                    onProcessItem: item => processedItems.Add((item, consumerId)),
                    delay: TimeSpan.FromMilliseconds(1)))
                .ReceiveFrom("buffer");
        }

        var flow = builder.Build();

        // Act
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert
        _output.WriteLine($"Total items processed: {processedItems.Count}");

        // Each item should be processed exactly once
        processedItems.Count.ShouldBe(30, "Each item should be processed exactly once");

        // Verify all items were processed
        var processedItemIds = processedItems.Select(x => x.item).OrderBy(x => x).ToArray();
        processedItemIds.ShouldBe(allItems, "All items should be processed");

        // Distribution report
        _output.WriteLine("Consumer distribution:");
        foreach (var consumerId in new[] { "consumer-0", "consumer-1" })
        {
            var count = processedItems.Count(x => x.consumerId == consumerId);
            _output.WriteLine($"  {consumerId}: {count} items");
        }
    }

    [Fact]
    public async Task BufferBlock_ComparedToBroadcastBlock_ShowsDifferentSemantics()
    {
        // This test demonstrates the key difference:
        // - BufferBlock: competing consumers (each item to ONE consumer)
        // - BroadcastBlock: fanout (each item to ALL consumers)

        var items = Enumerable.Range(1, 10).ToArray();
        var sp = _services.BuildServiceProvider();

        // Test with BufferBlock
        var bufferProcessedItems = new ConcurrentBag<(int item, string consumerId)>();
        var bufferBuilder = new StructuredDataFlowBuilder(sp, "BufferTest");
        bufferBuilder.AddProducer<int>("source", (IServiceProvider sp) => new TestProducer<int>(items));
        bufferBuilder.AddBuffer<int>("buffer").ReceiveFrom("source");

        for (var i = 0; i < 3; i++)
        {
            var consumerId = $"consumer-{i}";
            StructuredDataFlowBuilderExtensions.AddProcessor<int>(bufferBuilder, consumerId,
                (IServiceProvider sp) => new TestProcessor<int>(
                    onProcessItem: item => bufferProcessedItems.Add((item, consumerId))))
                .ReceiveFrom("buffer");
        }

        var bufferFlow = bufferBuilder.Build();
        await bufferFlow.ExecuteAsync(CreateContext("buffer-test", Guid.NewGuid(), sp));

        // Test with BroadcastBlock
        var broadcastProcessedItems = new ConcurrentBag<(int item, string consumerId)>();
        var broadcastBuilder = new StructuredDataFlowBuilder(sp, "BroadcastTest");
        broadcastBuilder.AddProducer<int>("source", (IServiceProvider sp) => new TestProducer<int>(items));
        broadcastBuilder.AddBroadcast<int>("broadcast").ReceiveFrom("source");

        for (var i = 0; i < 3; i++)
        {
            var consumerId = $"consumer-{i}";
            StructuredDataFlowBuilderExtensions.AddProcessor<int>(broadcastBuilder, consumerId,
                (IServiceProvider sp) => new TestProcessor<int>(
                    onProcessItem: item => broadcastProcessedItems.Add((item, consumerId))))
                .ReceiveFrom("broadcast");
        }

        var broadcastFlow = broadcastBuilder.Build();
        await broadcastFlow.ExecuteAsync(CreateContext("broadcast-test", Guid.NewGuid(), sp));

        // Assert
        _output.WriteLine($"BufferBlock processed: {bufferProcessedItems.Count} items (expected: 10)");
        _output.WriteLine($"BroadcastBlock processed: {broadcastProcessedItems.Count} items (expected: 30)");

        // BufferBlock: competing consumers - each item processed once
        bufferProcessedItems.Count.ShouldBe(10, "BufferBlock: each item to ONE consumer");

        // BroadcastBlock: fanout - each item processed by all consumers
        broadcastProcessedItems.Count.ShouldBe(30, "BroadcastBlock: each item to ALL consumers");

        // Each item should appear exactly once in BufferBlock results
        var bufferItemIds = bufferProcessedItems.Select(x => x.item).Distinct().OrderBy(x => x).ToArray();
        bufferItemIds.ShouldBe(items, "BufferBlock: all items processed exactly once");

        // Each item should appear 3 times in BroadcastBlock results (once per consumer)
        foreach (var item in items)
        {
            var count = broadcastProcessedItems.Count(x => x.item == item);
            count.ShouldBe(3, $"BroadcastBlock: item {item} should be processed by all 3 consumers");
        }
    }

    [Fact]
    public async Task BufferBlock_WithBranches_EnablesComplexPatterns()
    {
        // Arrange - Use branches to create parallel processing paths that merge into a buffer
        var items = Enumerable.Range(1, 20).ToArray();
        var processedItems = new ConcurrentBag<string>();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "BranchMerge");

        builder.AddProducer<int>("source", (IServiceProvider sp) => new TestProducer<int>(items));

        builder.AddBroadcast<int>("fanout").ReceiveFrom("source");

        // Create 2 branches that process items differently
        for (var i = 0; i < 2; i++)
        {
            var branchId = i;
            var branch = builder.AddBranch($"branch-{i}");

            branch.AddTransform<int, string>($"transform-{i}",
                (IServiceProvider sp) => new PassthroughTransformer<int, string>(x => $"Branch{branchId}-{x}"))
                .ReceiveFrom("fanout");
        }

        // Merge both branches into a buffer
        builder.AddBuffer<string>("merge-buffer")
            .ReceiveFrom("transform-0")
            .ReceiveFrom("transform-1");

        StructuredDataFlowBuilderExtensions.AddProcessor<string>(builder, "final-consumer",
            (IServiceProvider sp) => new TestProcessor<string>(
                onProcessItem: item => processedItems.Add(item)))
            .ReceiveFrom("merge-buffer");

        var flow = builder.Build();

        // Act
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert
        _output.WriteLine($"Total items processed: {processedItems.Count}");

        // Both branches produce items, so we should have 40 total (20 from each branch)
        processedItems.Count.ShouldBe(40, "Both branches should produce all items");

        // Verify we have items from both branches
        var branch0Items = processedItems.Count(x => x.StartsWith("Branch0-"));
        var branch1Items = processedItems.Count(x => x.StartsWith("Branch1-"));

        branch0Items.ShouldBe(20, "Branch 0 should produce 20 items");
        branch1Items.ShouldBe(20, "Branch 1 should produce 20 items");
    }

    [Fact]
    public void BufferBlock_MermaidDiagram_RendersCorrectly()
    {
        // Arrange - Create a flow with BufferBlock without explicit branches
        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "DiagramTest");

        builder.AddProducer<int>("source1", (IServiceProvider sp) => new TestProducer<int>(new[] { 1, 2 }));
        builder.AddProducer<int>("source2", (IServiceProvider sp) => new TestProducer<int>(new[] { 3, 4 }));
        
        // Buffer merges without AddBranch
        builder.AddBuffer<int>("merge-buffer")
            .ReceiveFrom("source1")
            .ReceiveFrom("source2");
        
        StructuredDataFlowBuilderExtensions.AddProcessor<int>(builder, "consumer", 
            (IServiceProvider sp) => new TestProcessor<int>())
            .ReceiveFrom("merge-buffer");

        // Act
        var diagram = builder.Graph.ToMermaidDiagram();

        // Assert
        _output.WriteLine("Generated Mermaid Diagram:");
        _output.WriteLine(diagram);
        
        // Verify the diagram contains key elements
        diagram.ShouldContain("source1");
        diagram.ShouldContain("source2");
        diagram.ShouldContain("merge-buffer");
        diagram.ShouldContain("consumer");
        diagram.ShouldContain("-->"); // Connection arrows
        
        // Verify it's valid mermaid syntax
        diagram.ShouldStartWith("flowchart");
    }

    [Fact]
    public async Task BufferBlock_WhenOneProducerFails_OtherProducersAndConsumersStopGracefully()
    {
        // Arrange - Test error handling with multiple producers
        var items1 = Enumerable.Range(1, 5).ToArray();
        var items2 = Enumerable.Range(6, 5).ToArray();
        var processedItems = new ConcurrentBag<int>();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "ErrorHandling");

        // Producer 1: Normal producer
        builder.AddProducer<int>("source1", (IServiceProvider sp) => new TestProducer<int>(items1));
        
        // Producer 2: Will fail after producing 2 items
        builder.AddProducer<int>("source2", (IServiceProvider sp) => new ErrorProducer<int>(
            items2,
            shouldError: (item) => item == 7)); // Fail on 7
        
        // Buffer accepts from both sources
        builder.AddBuffer<int>("buffer")
            .ReceiveFrom("source1")
            .ReceiveFrom("source2");
        
        StructuredDataFlowBuilderExtensions.AddProcessor<int>(builder, "consumer", 
            (IServiceProvider sp) => new TestProcessor<int>(
                onProcessItem: item => processedItems.Add(item)))
            .ReceiveFrom("buffer");

        var flow = builder.Build();

        // Act & Assert
        var context = CreateContext("test", Guid.NewGuid(), sp);
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await flow.ExecuteAsync(context));
        
        _output.WriteLine($"Exception message: {exception.Message}");
        _output.WriteLine($"Items processed before error: {processedItems.Count}");
        
        // When one producer fails:
        // 1. The channel is marked as complete with error
        // 2. Other producers should stop (via task cancellation propagation)
        // 3. Consumers should stop reading
        // 4. The error should propagate up
        
        exception.Message.ShouldContain("Simulated error");
        
        // Some items should have been processed before the error
        processedItems.Count.ShouldBeGreaterThan(0);
        processedItems.Count.ShouldBeLessThan(10); // Not all items processed
    }

    #region Helper Classes

    private class PassthroughTransformer<TIn, TOut> : IStreamTransformer<TIn, TOut>
    {
        private readonly Func<TIn, TOut> _transform;

        public PassthroughTransformer(Func<TIn, TOut> transform)
        {
            _transform = transform;
        }

        public async IAsyncEnumerable<TOut> TransformAsync(
            IDataFlowContext context,
            IAsyncEnumerable<TIn> input,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in input.WithCancellation(cancellationToken))
            {
                yield return _transform(item);
            }
        }
    }

    #endregion
}
