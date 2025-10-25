namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class EdgeStrategyTests
{
    private static async IAsyncEnumerable<int> ProduceIntegers(IExecutionContext ctx, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return i;
        }
    }

    [Fact]
    public async Task CompetingEdge_Should_Distribute_Items_To_Competing_Consumers()
    {
        // Arrange
        var processor1Items = new List<int>();
        var processor2Items = new List<int>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 10));

        var processor1 = new ProcessorBlock<int>("processor1", async (item, ctx) =>
        {
            lock (processor1Items)
            {
                processor1Items.Add(item);
            }
            await Task.Delay(10); // Simulate work
        });

        var processor2 = new ProcessorBlock<int>("processor2", async (item, ctx) =>
        {
            lock (processor2Items)
            {
                processor2Items.Add(item);
            }
            await Task.Delay(10); // Simulate work
        });

        var builder = new DataFlowGraphBuilder("competing-flow");
        builder.AddBlock(producer)
            .AddBlock(processor1)
            .AddBlock(processor2);

        // Create competing edge - both processors compete for items from producer
        var competingEdge = new Edge(
            producer,
            new[] { processor1, processor2 },
            new CompetingEdgeStrategy(BufferMode.Bounded, 5));
        
        builder.AddEdge(competingEdge);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        // Each processor should get some items, but not all
        processor1Items.Count.ShouldBeGreaterThan(0);
        processor2Items.Count.ShouldBeGreaterThan(0);
        
        // Together they should process all items
        (processor1Items.Count + processor2Items.Count).ShouldBe(10);
        
        // No duplicates - each item processed exactly once
        var allItems = processor1Items.Concat(processor2Items).OrderBy(x => x).ToList();
        allItems.ShouldBe(Enumerable.Range(1, 10));
    }

    [Fact]
    public async Task BroadcastEdge_Should_Send_All_Items_To_All_Consumers()
    {
        // Arrange
        var processor1Items = new List<int>();
        var processor2Items = new List<int>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 5));

        var processor1 = new ProcessorBlock<int>("processor1", async (item, ctx) =>
        {
            processor1Items.Add(item);
            await Task.CompletedTask;
        });

        var processor2 = new ProcessorBlock<int>("processor2", async (item, ctx) =>
        {
            processor2Items.Add(item);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("broadcast-flow");
        builder.AddBlock(producer)
            .AddBlock(processor1)
            .AddBlock(processor2);

        // Create broadcast edge - both processors get all items
        var broadcastEdge = new Edge(
            producer,
            new[] { processor1, processor2 },
            new BroadcastEdgeStrategy(BufferMode.Bounded, 10));
        
        builder.AddEdge(broadcastEdge);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        // Both processors should get all items
        processor1Items.Count.ShouldBe(5);
        processor2Items.Count.ShouldBe(5);
        processor1Items.ShouldBe(Enumerable.Range(1, 5));
        processor2Items.ShouldBe(Enumerable.Range(1, 5));
    }

    [Fact]
    public async Task BroadcastEdge_With_Cloning_Should_Clone_Items_For_Each_Consumer()
    {
        // Arrange
        var processor1Items = new List<CloneableItem>();
        var processor2Items = new List<CloneableItem>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<CloneableItem>("producer", ProduceCloneableItems);

        var processor1 = new ProcessorBlock<CloneableItem>("processor1", async (item, ctx) =>
        {
            item.Value *= 10; // Modify the item
            processor1Items.Add(item);
            await Task.CompletedTask;
        });

        var processor2 = new ProcessorBlock<CloneableItem>("processor2", async (item, ctx) =>
        {
            item.Value *= 100; // Modify the item differently
            processor2Items.Add(item);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("cloning-flow");
        builder.AddBlock(producer)
            .AddBlock(processor1)
            .AddBlock(processor2);

        // Create broadcast edge with cloning - both processors get independent clones
        var cloningEdge = new Edge(
            producer,
            new[] { processor1, processor2 },
            new BroadcastEdgeStrategy(
                cloneFunc: item => ((CloneableItem)item).Clone(),
                BufferMode.Bounded, 
                10));
        
        builder.AddEdge(cloningEdge);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        // Both processors should get all items
        processor1Items.Count.ShouldBe(3);
        processor2Items.Count.ShouldBe(3);
        
        // Processor1 modified items by *10
        processor1Items.Select(x => x.Value).ShouldBe(new[] { 10, 20, 30 });
        
        // Processor2 modified items by *100 (independent clones)
        processor2Items.Select(x => x.Value).ShouldBe(new[] { 100, 200, 300 });
    }

    private static async IAsyncEnumerable<CloneableItem> ProduceCloneableItems(IExecutionContext ctx)
    {
        yield return new CloneableItem { Value = 1 };
        yield return new CloneableItem { Value = 2 };
        yield return new CloneableItem { Value = 3 };
    }

    [Fact]
    public async Task Mixed_Edge_Strategies_Should_Work_Together()
    {
        // Arrange
        var broadcastProcessor1Items = new List<int>();
        var broadcastProcessor2Items = new List<int>();
        var competingProcessor1Items = new List<int>();
        var competingProcessor2Items = new List<int>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 10));

        // Broadcast path processors
        var broadcastProc1 = new ProcessorBlock<int>("broadcast-proc1", async (item, ctx) =>
        {
            broadcastProcessor1Items.Add(item);
            await Task.CompletedTask;
        });

        var broadcastProc2 = new ProcessorBlock<int>("broadcast-proc2", async (item, ctx) =>
        {
            broadcastProcessor2Items.Add(item);
            await Task.CompletedTask;
        });

        // Competing path processors
        var competingProc1 = new ProcessorBlock<int>("competing-proc1", async (item, ctx) =>
        {
            lock (competingProcessor1Items)
            {
                competingProcessor1Items.Add(item);
            }
            await Task.Delay(5);
        });

        var competingProc2 = new ProcessorBlock<int>("competing-proc2", async (item, ctx) =>
        {
            lock (competingProcessor2Items)
            {
                competingProcessor2Items.Add(item);
            }
            await Task.Delay(5);
        });

        var builder = new DataFlowGraphBuilder("mixed-strategy-flow");
        builder.AddBlock(producer)
            .AddBlock(broadcastProc1)
            .AddBlock(broadcastProc2)
            .AddBlock(competingProc1)
            .AddBlock(competingProc2);

        // Broadcast edge to first two processors
        var broadcastEdge = new Edge(
            producer,
            new[] { broadcastProc1, broadcastProc2 },
            new BroadcastEdgeStrategy(BufferMode.Bounded, 10));
        
        // Competing edge to second two processors
        var competingEdge = new Edge(
            producer,
            new[] { competingProc1, competingProc2 },
            new CompetingEdgeStrategy(BufferMode.Bounded, 10));
        
        builder.AddEdge(broadcastEdge);
        builder.AddEdge(competingEdge);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        // Broadcast processors should both get all items
        broadcastProcessor1Items.Count.ShouldBe(10);
        broadcastProcessor2Items.Count.ShouldBe(10);
        broadcastProcessor1Items.ShouldBe(Enumerable.Range(1, 10));
        broadcastProcessor2Items.ShouldBe(Enumerable.Range(1, 10));
        
        // Competing processors should share items
        competingProcessor1Items.Count.ShouldBeGreaterThan(0);
        competingProcessor2Items.Count.ShouldBeGreaterThan(0);
        (competingProcessor1Items.Count + competingProcessor2Items.Count).ShouldBe(10);
        
        var allCompetingItems = competingProcessor1Items.Concat(competingProcessor2Items).OrderBy(x => x).ToList();
        allCompetingItems.ShouldBe(Enumerable.Range(1, 10));
    }

    private class CloneableItem : ICloneable
    {
        public int Value { get; set; }

        public object Clone()
        {
            return new CloneableItem { Value = Value };
        }
    }
}
