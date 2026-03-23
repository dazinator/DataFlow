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
/// OBSOLETE — these tests cover EnvelopeBlocks (SimpleEnvelopeTransformer, EnvelopeProjector,
/// EnvelopeProcessor) which were designed to propagate the envelope control plane through the
/// topology. That control plane is superseded by the epoch stream model. Code retained for reference.
/// </summary>
public class EnvelopeBlocksTests
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

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task SimpleEnvelopeTransformer_Should_Transform_Data_And_Forward_Control()
    {
        // Arrange
        var outputEnvelopes = new List<IDataEnvelope>();
        
        var services = new ServiceCollection();
        services.AddScoped(_ => new EnvelopeCollectorActor(outputEnvelopes));
        var serviceProvider = services.BuildServiceProvider();
        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceMixedEnvelopes(ctx));
        var transformer = BlockHelpers.CreateSimpleEnvelopeTransformer<int, string>("transformer", i => $"Value-{i}");
        var consumer = BlockHelpers.CreateActor<IDataEnvelope, object, EnvelopeCollectorActor>("consumer", serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("transform-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AddBlock(consumer);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        builder.AddEdge(new Edge(producer, transformer, envelopeStrategy));
        builder.AddEdge(new Edge(transformer, consumer, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        outputEnvelopes.Count.ShouldBe(5); // 3 data + 2 control

        // Data items should be transformed
        outputEnvelopes[0].ShouldBeOfType<DataItem<string>>();
        outputEnvelopes[0].GetValue<string>().ShouldBe("Value-1");

        // Control signals should be forwarded
        outputEnvelopes[1].ShouldBeOfType<CheckpointBarrier>();

        outputEnvelopes[2].ShouldBeOfType<DataItem<string>>();
        outputEnvelopes[2].GetValue<string>().ShouldBe("Value-2");

        outputEnvelopes[3].ShouldBeOfType<Heartbeat>();

        outputEnvelopes[4].ShouldBeOfType<DataItem<string>>();
        outputEnvelopes[4].GetValue<string>().ShouldBe("Value-3");
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task AsyncEnvelopeTransformer_Should_Transform_With_Async_Logic()
    {
        // Arrange
        var outputEnvelopes = new List<IDataEnvelope>();
        
        var services = new ServiceCollection();
        services.AddScoped(_ => new EnvelopeCollectorActor(outputEnvelopes));
        var serviceProvider = services.BuildServiceProvider();
        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceDataOnly(ctx));
        var transformer = BlockHelpers.CreateAsyncEnvelopeTransformer<int, int>("transformer", async (i, ctx) =>
        {
            await Task.Delay(1); // Simulate async work
            return i * 2;
        });
        var consumer = BlockHelpers.CreateActor<IDataEnvelope, object, EnvelopeCollectorActor>("consumer", serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("async-transform-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AddBlock(consumer);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        builder.AddEdge(new Edge(producer, transformer, envelopeStrategy));
        builder.AddEdge(new Edge(transformer, consumer, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        outputEnvelopes.Count.ShouldBe(3);
        outputEnvelopes.All(e => e.IsDataItem()).ShouldBeTrue();
        outputEnvelopes[0].GetValue<int>().ShouldBe(2);  // 1 * 2
        outputEnvelopes[1].GetValue<int>().ShouldBe(4);  // 2 * 2
        outputEnvelopes[2].GetValue<int>().ShouldBe(6);  // 3 * 2
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task EnvelopeProjector_Should_Project_One_To_Many()
    {
        // Arrange
        var outputEnvelopes = new List<IDataEnvelope>();
        
        var services = new ServiceCollection();
        services.AddScoped(_ => new EnvelopeCollectorActor(outputEnvelopes));
        var serviceProvider = services.BuildServiceProvider();
        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceDataWithControl(ctx));
        var projector = BlockHelpers.CreateEnvelopeProjector<int, int>("projector", (i, ctx) =>
        {
            // Each input produces multiple outputs
            return AsyncEnumerable(i, i * 10, i * 100);
        });
        var consumer = BlockHelpers.CreateActor<IDataEnvelope, object, EnvelopeCollectorActor>("consumer", serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("projector-flow");
        builder.AddBlock(producer)
            .AddBlock(projector)
            .AddBlock(consumer);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        builder.AddEdge(new Edge(producer, projector, envelopeStrategy));
        builder.AddEdge(new Edge(projector, consumer, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        // 2 data items * 3 outputs each + 1 control signal = 7 total
        outputEnvelopes.Count.ShouldBe(7);

        var dataItems = outputEnvelopes.Where(e => e.IsDataItem()).ToList();
        dataItems.Count.ShouldBe(6);

        // First input (1) produces 1, 10, 100
        dataItems[0].GetValue<int>().ShouldBe(1);
        dataItems[1].GetValue<int>().ShouldBe(10);
        dataItems[2].GetValue<int>().ShouldBe(100);

        // Control signal in the middle
        outputEnvelopes[3].ShouldBeOfType<CheckpointBarrier>();

        // Second input (2) produces 2, 20, 200
        dataItems[3].GetValue<int>().ShouldBe(2);
        dataItems[4].GetValue<int>().ShouldBe(20);
        dataItems[5].GetValue<int>().ShouldBe(200);
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task EnvelopeProcessor_Should_Process_Data_Items()
    {
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var processedData = new List<int>();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceMixedEnvelopes(ctx));
        var processor = BlockHelpers.CreateEnvelopeProcessor<int>("processor", async (value, ctx) =>
        {
            processedData.Add(value);
            await Task.CompletedTask;
        });

        var builder = GraphHelpers.CreateGraphBuilder("processor-flow");
        builder.AddBlock(producer)
            .AddBlock(processor);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        builder.AddEdge(new Edge(producer, processor, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        processedData.Count.ShouldBe(3); // Only data items, control signals ignored
        processedData.ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task EnvelopeProcessor_Should_Observe_Control_Signals()
    {
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var processedData = new List<int>();
        var observedControlSignals = new List<IDataEnvelope>();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceMixedEnvelopes(ctx));
        var processor = BlockHelpers.CreateEnvelopeProcessor<int>(
            "processor",
            processData: async (value, ctx) =>
            {
                processedData.Add(value);
                await Task.CompletedTask;
            },
            processControl: async (signal, ctx) =>
            {
                observedControlSignals.Add(signal);
                await Task.CompletedTask;
            });

        var builder = GraphHelpers.CreateGraphBuilder("observing-processor-flow");
        builder.AddBlock(producer)
            .AddBlock(processor);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        builder.AddEdge(new Edge(producer, processor, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        processedData.Count.ShouldBe(3);
        processedData.ShouldBe(new[] { 1, 2, 3 });

        observedControlSignals.Count.ShouldBe(2);
        observedControlSignals[0].ShouldBeOfType<CheckpointBarrier>();
        observedControlSignals[1].ShouldBeOfType<Heartbeat>();
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task EnvelopeBlocks_Should_Preserve_Order_In_Pipeline()
    {
        // Arrange
        var outputEnvelopes = new List<IDataEnvelope>();
        
        var services = new ServiceCollection();
        services.AddScoped(_ => new EnvelopeCollectorActor(outputEnvelopes));
        var serviceProvider = services.BuildServiceProvider();
        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceOrderedStream(ctx));
        var transformer1 = BlockHelpers.CreateSimpleEnvelopeTransformer<int, int>("transformer1", i => i + 1);
        var transformer2 = BlockHelpers.CreateSimpleEnvelopeTransformer<int, int>("transformer2", i => i * 10);
        var consumer = BlockHelpers.CreateActor<IDataEnvelope, object, EnvelopeCollectorActor>("consumer", serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("pipeline-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer1)
            .AddBlock(transformer2)
            .AddBlock(consumer);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        builder.AddEdge(new Edge(producer, transformer1, envelopeStrategy));
        builder.AddEdge(new Edge(transformer1, transformer2, envelopeStrategy));
        builder.AddEdge(new Edge(transformer2, consumer, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        outputEnvelopes.Count.ShouldBe(6);

        // Verify order and transformations
        outputEnvelopes[0].GetValue<int>().ShouldBe(20);  // (1 + 1) * 10
        outputEnvelopes[1].ShouldBeOfType<CheckpointBarrier>();
        outputEnvelopes[2].GetValue<int>().ShouldBe(30);  // (2 + 1) * 10
        outputEnvelopes[3].GetValue<int>().ShouldBe(40);  // (3 + 1) * 10
        outputEnvelopes[4].ShouldBeOfType<Heartbeat>();
        outputEnvelopes[5].GetValue<int>().ShouldBe(50);  // (4 + 1) * 10
    }

    // Helper methods

    private static async IAsyncEnumerable<IDataEnvelope> ProduceMixedEnvelopes(IExecutionContext ctx)
    {
        yield return new DataItem<int>(1);
        yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
        yield return new DataItem<int>(2);
        yield return new Heartbeat(DateTime.UtcNow);
        yield return new DataItem<int>(3);
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<IDataEnvelope> ProduceDataOnly(IExecutionContext ctx)
    {
        yield return new DataItem<int>(1);
        yield return new DataItem<int>(2);
        yield return new DataItem<int>(3);
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<IDataEnvelope> ProduceDataWithControl(IExecutionContext ctx)
    {
        yield return new DataItem<int>(1);
        yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
        yield return new DataItem<int>(2);
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<IDataEnvelope> ProduceOrderedStream(IExecutionContext ctx)
    {
        yield return new DataItem<int>(1);
        yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
        yield return new DataItem<int>(2);
        yield return new DataItem<int>(3);
        yield return new Heartbeat(DateTime.UtcNow);
        yield return new DataItem<int>(4);
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<int> AsyncEnumerable(params int[] values)
    {
        foreach (var value in values)
        {
            yield return value;
        }
        await Task.CompletedTask;
    }
}
