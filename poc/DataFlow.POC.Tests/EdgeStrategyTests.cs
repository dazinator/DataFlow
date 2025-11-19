namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using DataFlow.POC.Tests.TestHelpers;

public class EdgeStrategyTests
{
    /// <summary>
    /// Thread-safe collector actor for integers with simulated work delay.
    /// </summary>
    private class WorkSimulatingCollectorActor : IStreamActor<int, object>
    {
        private readonly List<int> _collected;
        private readonly int _delayMs;

        public WorkSimulatingCollectorActor(List<int> collected, int delayMs = 10)
        {
            _collected = collected;
            _delayMs = delayMs;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                lock (_collected)
                {
                    _collected.Add(item);
                }
                await Task.Delay(_delayMs, context.CancellationToken); // Simulate work
            }
            yield break;
        }
    }

    /// <summary>
    /// Collector actor for CloneableItem with simulated work delay.
    /// </summary>
    private class CloneableItemCollectorActor : IStreamActor<CloneableItem, object>
    {
        private readonly List<CloneableItem> _collected;
        private readonly int _delayMs;

        public CloneableItemCollectorActor(List<CloneableItem> collected, int delayMs = 10)
        {
            _collected = collected;
            _delayMs = delayMs;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<CloneableItem> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                lock (_collected)
                {
                    _collected.Add(item);
                }
                await Task.Delay(_delayMs, context.CancellationToken); // Simulate work
            }
            yield break;
        }
    }

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
        
        // Create separate service providers for each processor
        var services1 = new ServiceCollection();
        services1.AddScoped(_ => new WorkSimulatingCollectorActor(processor1Items));
        var serviceProvider1 = services1.BuildServiceProvider();

        var services2 = new ServiceCollection();
        services2.AddScoped(_ => new WorkSimulatingCollectorActor(processor2Items));
        var serviceProvider2 = services2.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<int>("producer", ctx => ProduceIntegers(ctx, 10));

        var processor1 = BlockHelpers.CreateActor<int, object, WorkSimulatingCollectorActor>("processor1", serviceProvider1.GetRequiredService<IServiceScopeFactory>());

        var processor2 = BlockHelpers.CreateActor<int, object, WorkSimulatingCollectorActor>("processor2", serviceProvider2.GetRequiredService<IServiceScopeFactory>());

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
        var context = new ExecutionContext(commonServices, CancellationToken.None);

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
        
        // Create separate service providers for each processor
        var services1 = new ServiceCollection();
        services1.AddScoped(_ => new WorkSimulatingCollectorActor(processor1Items, delayMs: 0));
        var serviceProvider1 = services1.BuildServiceProvider();

        var services2 = new ServiceCollection();
        services2.AddScoped(_ => new WorkSimulatingCollectorActor(processor2Items, delayMs: 0));
        var serviceProvider2 = services2.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<int>("producer", ctx => ProduceIntegers(ctx, 5));

        var processor1 = BlockHelpers.CreateActor<int, object, WorkSimulatingCollectorActor>("processor1", serviceProvider1.GetRequiredService<IServiceScopeFactory>());

        var processor2 = BlockHelpers.CreateActor<int, object, WorkSimulatingCollectorActor>("processor2", serviceProvider2.GetRequiredService<IServiceScopeFactory>());

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
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        // Both processors should get all items
        processor1Items.Count.ShouldBe(5);
        processor2Items.Count.ShouldBe(5);
        processor1Items.ShouldBe(Enumerable.Range(1, 5));
        processor2Items.ShouldBe(Enumerable.Range(1, 5));
    }

    /// <summary>
    /// Collector actor for CloneableItem that modifies the value.
    /// </summary>
    private class ModifyingCloneableItemCollectorActor : IStreamActor<CloneableItem, object>
    {
        private readonly List<CloneableItem> _collected;
        private readonly int _multiplier;

        public ModifyingCloneableItemCollectorActor(List<CloneableItem> collected, int multiplier)
        {
            _collected = collected;
            _multiplier = multiplier;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<CloneableItem> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                item.Value *= _multiplier; // Modify the item
                _collected.Add(item);
            }
            yield break;
        }
    }

    [Fact]
    public async Task BroadcastEdge_With_Cloning_Should_Clone_Items_For_Each_Consumer()
    {
        // Arrange
        var processor1Items = new List<CloneableItem>();
        var processor2Items = new List<CloneableItem>();
        
        // Create separate service providers for each processor
        var services1 = new ServiceCollection();
        services1.AddScoped(_ => new ModifyingCloneableItemCollectorActor(processor1Items, multiplier: 10));
        var serviceProvider1 = services1.BuildServiceProvider();

        var services2 = new ServiceCollection();
        services2.AddScoped(_ => new ModifyingCloneableItemCollectorActor(processor2Items, multiplier: 100));
        var serviceProvider2 = services2.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<CloneableItem>("producer", ProduceCloneableItems);

        var processor1 = BlockHelpers.CreateActor<CloneableItem, object, ModifyingCloneableItemCollectorActor>("processor1", serviceProvider1.GetRequiredService<IServiceScopeFactory>());

        var processor2 = BlockHelpers.CreateActor<CloneableItem, object, ModifyingCloneableItemCollectorActor>("processor2", serviceProvider2.GetRequiredService<IServiceScopeFactory>());

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
        var context = new ExecutionContext(commonServices, CancellationToken.None);

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
        
        // Create separate service providers for each processor
        var broadcastServices1 = new ServiceCollection();
        broadcastServices1.AddScoped(_ => new WorkSimulatingCollectorActor(broadcastProcessor1Items, delayMs: 0));
        var broadcastSP1 = broadcastServices1.BuildServiceProvider();

        var broadcastServices2 = new ServiceCollection();
        broadcastServices2.AddScoped(_ => new WorkSimulatingCollectorActor(broadcastProcessor2Items, delayMs: 0));
        var broadcastSP2 = broadcastServices2.BuildServiceProvider();

        var competingServices1 = new ServiceCollection();
        competingServices1.AddScoped(_ => new WorkSimulatingCollectorActor(competingProcessor1Items, delayMs: 5));
        var competingSP1 = competingServices1.BuildServiceProvider();

        var competingServices2 = new ServiceCollection();
        competingServices2.AddScoped(_ => new WorkSimulatingCollectorActor(competingProcessor2Items, delayMs: 5));
        var competingSP2 = competingServices2.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<int>("producer", ctx => ProduceIntegers(ctx, 10));

        // Broadcast path processors
        var broadcastProc1 = BlockHelpers.CreateActor<int, object, WorkSimulatingCollectorActor>("broadcast-proc1", broadcastSP1.GetRequiredService<IServiceScopeFactory>());

        var broadcastProc2 = BlockHelpers.CreateActor<int, object, WorkSimulatingCollectorActor>("broadcast-proc2", broadcastSP2.GetRequiredService<IServiceScopeFactory>());

        // Competing path processors
        var competingProc1 = BlockHelpers.CreateActor<int, object, WorkSimulatingCollectorActor>("competing-proc1", competingSP1.GetRequiredService<IServiceScopeFactory>());

        var competingProc2 = BlockHelpers.CreateActor<int, object, WorkSimulatingCollectorActor>("competing-proc2", competingSP2.GetRequiredService<IServiceScopeFactory>());

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
        var context = new ExecutionContext(commonServices, CancellationToken.None);

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
