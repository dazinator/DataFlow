namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using DataFlow.POC.Tests.TestHelpers;
using DataFlow.POC.Registry;

/// <summary>
/// OBSOLETE — these tests cover the envelope-based control plane approach where
/// CheckpointBarrier and Heartbeat signals were multiplexed through the same channel as data.
/// This design is superseded by the epoch stream model: epoch boundaries serve as barriers
/// natively, and envelope-style message workflows are an application-level concern that
/// requires no special library infrastructure. The production code is retained for reference.
/// </summary>
public class EnvelopeEdgeStrategyTests
{
    /// <summary>
    /// Collector actor for IDataEnvelope items.
    /// </summary>
    private class EnvelopeCollectorActor : IStreamActor<IDataEnvelope, object>
    {
        private readonly List<IDataEnvelope> _collected;

        public EnvelopeCollectorActor(List<IDataEnvelope> collected)
        {
            _collected = collected;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<IDataEnvelope> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _collected.Add(item);
            }
            yield break;
        }
    }

    /// <summary>
    /// Collector actor with simulated work delay.
    /// </summary>
    private class DelayedEnvelopeCollectorActor : IStreamActor<IDataEnvelope, object>
    {
        private readonly List<IDataEnvelope> _collected;
        private readonly int _delayMs;

        public DelayedEnvelopeCollectorActor(List<IDataEnvelope> collected, int delayMs = 5)
        {
            _collected = collected;
            _delayMs = delayMs;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<IDataEnvelope> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _collected.Add(item);
                await Task.Delay(_delayMs, context.CancellationToken);
            }
            yield break;
        }
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task EnvelopeBroadcast_Should_Broadcast_Data_And_Control_Signals()
    {
        // Arrange
        var consumer1Items = new List<IDataEnvelope>();
        var consumer2Items = new List<IDataEnvelope>();

        // Create separate service providers for each consumer
        var services1 = new ServiceCollection();
        services1.AddScoped(_ => new EnvelopeCollectorActor(consumer1Items));
        var serviceProvider1 = services1.BuildServiceProvider();

        var services2 = new ServiceCollection();
        services2.AddScoped(_ => new EnvelopeCollectorActor(consumer2Items));
        var serviceProvider2 = services2.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceEnvelopes(ctx));

        var consumer1 = BlockHelpers.CreateActor<IDataEnvelope, object, EnvelopeCollectorActor>("consumer1", serviceProvider1.GetRequiredService<IServiceScopeFactory>());

        var consumer2 = BlockHelpers.CreateActor<IDataEnvelope, object, EnvelopeCollectorActor>("consumer2", serviceProvider2.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("broadcast-envelope-flow");
        builder.AddBlock(producer)
            .AddBlock(consumer1)
            .AddBlock(consumer2);

        // Use envelope broadcast strategy
        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        builder.AddEdge(new Edge(producer, new[] { consumer1, consumer2 }, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        consumer1Items.Count.ShouldBe(4); // 2 data + 2 control signals
        consumer2Items.Count.ShouldBe(4); // 2 data + 2 control signals

        // Both consumers should receive all items in order
        consumer1Items[0].ShouldBeOfType<DataItem<int>>();
        consumer1Items[0].GetValue<int>().ShouldBe(1);
        consumer1Items[1].ShouldBeOfType<CheckpointBarrier>();
        consumer1Items[2].ShouldBeOfType<DataItem<int>>();
        consumer1Items[2].GetValue<int>().ShouldBe(2);
        consumer1Items[3].ShouldBeOfType<Heartbeat>();

        consumer2Items[0].ShouldBeOfType<DataItem<int>>();
        consumer2Items[1].ShouldBeOfType<CheckpointBarrier>();
        consumer2Items[2].ShouldBeOfType<DataItem<int>>();
        consumer2Items[3].ShouldBeOfType<Heartbeat>();
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task EnvelopeCompeting_Should_Compete_Data_And_Control_Goes_To_First_Consumer()
    {
        // Note: This test demonstrates the known limitation of competing edges with envelopes.
        // Control signals in competing edges follow competing semantics (first consumer gets it).
        // For guaranteed control signal delivery to all consumers, use broadcast edges.
        
        // Arrange
        var consumer1Items = new List<IDataEnvelope>();
        var consumer2Items = new List<IDataEnvelope>();

        // Create separate service providers for each consumer
        var services1 = new ServiceCollection();
        services1.AddScoped(_ => new DelayedEnvelopeCollectorActor(consumer1Items));
        var serviceProvider1 = services1.BuildServiceProvider();

        var services2 = new ServiceCollection();
        services2.AddScoped(_ => new DelayedEnvelopeCollectorActor(consumer2Items));
        var serviceProvider2 = services2.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceEnvelopes(ctx));

        var consumer1 = BlockHelpers.CreateActor<IDataEnvelope, object, DelayedEnvelopeCollectorActor>("consumer1", serviceProvider1.GetRequiredService<IServiceScopeFactory>());

        var consumer2 = BlockHelpers.CreateActor<IDataEnvelope, object, DelayedEnvelopeCollectorActor>("consumer2", serviceProvider2.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("competing-envelope-flow");
        builder.AddBlock(producer)
            .AddBlock(consumer1)
            .AddBlock(consumer2);

        // Use envelope competing strategy
        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateCompeting();
        builder.AddEdge(new Edge(producer, new[] { consumer1, consumer2 }, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        // All items (data + control) compete between consumers
        var totalItems = consumer1Items.Count + consumer2Items.Count;
        totalItems.ShouldBe(4); // 2 data + 2 control signals
        
        // Control signals and data items compete (each goes to one consumer)
        // We just verify the total count is correct
        var allItems = consumer1Items.Concat(consumer2Items).ToList();
        var dataItemCount = allItems.Count(e => e.IsDataItem());
        var controlSignalCount = allItems.Count(e => e.IsControlSignal());
        
        dataItemCount.ShouldBe(2);
        controlSignalCount.ShouldBe(2);
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task EnvelopeEdge_Should_Preserve_Control_Signal_Order()
    {
        // Arrange
        var receivedItems = new List<IDataEnvelope>();
        
        var services = new ServiceCollection();
        services.AddScoped(_ => new EnvelopeCollectorActor(receivedItems));
        var serviceProvider = services.BuildServiceProvider();
        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceOrderedEnvelopes(ctx));

        var consumer = BlockHelpers.CreateActor<IDataEnvelope, object, EnvelopeCollectorActor>("consumer", serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("ordered-envelope-flow");
        builder.AddBlock(producer)
            .AddBlock(consumer);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        builder.AddEdge(new Edge(producer, consumer, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        receivedItems.Count.ShouldBe(6);
        
        // Verify order is preserved
        receivedItems[0].ShouldBeOfType<DataItem<int>>();
        receivedItems[0].GetValue<int>().ShouldBe(1);
        
        receivedItems[1].ShouldBeOfType<CheckpointBarrier>();
        
        receivedItems[2].ShouldBeOfType<DataItem<int>>();
        receivedItems[2].GetValue<int>().ShouldBe(2);
        
        receivedItems[3].ShouldBeOfType<DataItem<int>>();
        receivedItems[3].GetValue<int>().ShouldBe(3);
        
        receivedItems[4].ShouldBeOfType<Heartbeat>();
        
        receivedItems[5].ShouldBeOfType<DataItem<int>>();
        receivedItems[5].GetValue<int>().ShouldBe(4);
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task EnvelopeEdge_Should_Handle_Only_Control_Signals()
    {
        // Arrange
        var receivedItems = new List<IDataEnvelope>();
        
        var services = new ServiceCollection();
        services.AddScoped(_ => new EnvelopeCollectorActor(receivedItems));
        var serviceProvider = services.BuildServiceProvider();
        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceOnlyControlSignals(ctx));

        var consumer = BlockHelpers.CreateActor<IDataEnvelope, object, EnvelopeCollectorActor>("consumer", serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("control-only-flow");
        builder.AddBlock(producer)
            .AddBlock(consumer);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        builder.AddEdge(new Edge(producer, consumer, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        receivedItems.Count.ShouldBe(2);
        receivedItems.All(e => e.IsControlSignal()).ShouldBeTrue();
        receivedItems[0].ShouldBeOfType<CheckpointBarrier>();
        receivedItems[1].ShouldBeOfType<Heartbeat>();
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task EnvelopeEdge_Should_Handle_Only_Data_Items()
    {
        // Arrange
        var receivedItems = new List<IDataEnvelope>();
        
        var services = new ServiceCollection();
        services.AddScoped(_ => new EnvelopeCollectorActor(receivedItems));
        var serviceProvider = services.BuildServiceProvider();
        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceOnlyData(ctx));

        var consumer = BlockHelpers.CreateActor<IDataEnvelope, object, EnvelopeCollectorActor>("consumer", serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("data-only-flow");
        builder.AddBlock(producer)
            .AddBlock(consumer);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        builder.AddEdge(new Edge(producer, consumer, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        receivedItems.Count.ShouldBe(5);
        receivedItems.All(e => e.IsDataItem()).ShouldBeTrue();
        for (int i = 0; i < 5; i++)
        {
            receivedItems[i].GetValue<int>().ShouldBe(i + 1);
        }
    }

    // Helper methods to produce test data

    private static async IAsyncEnumerable<IDataEnvelope> ProduceEnvelopes(IExecutionContext ctx)
    {
        yield return new DataItem<int>(1);
        yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
        yield return new DataItem<int>(2);
        yield return new Heartbeat(DateTime.UtcNow);
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<IDataEnvelope> ProduceOrderedEnvelopes(IExecutionContext ctx)
    {
        yield return new DataItem<int>(1);
        yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
        yield return new DataItem<int>(2);
        yield return new DataItem<int>(3);
        yield return new Heartbeat(DateTime.UtcNow);
        yield return new DataItem<int>(4);
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<IDataEnvelope> ProduceOnlyControlSignals(IExecutionContext ctx)
    {
        yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
        yield return new Heartbeat(DateTime.UtcNow);
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<IDataEnvelope> ProduceOnlyData(IExecutionContext ctx)
    {
        for (int i = 1; i <= 5; i++)
        {
            yield return new DataItem<int>(i);
        }
        await Task.CompletedTask;
    }
}
