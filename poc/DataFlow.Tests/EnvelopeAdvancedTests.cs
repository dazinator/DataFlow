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
/// OBSOLETE — these tests cover advanced scenarios (barrier alignment, heartbeat propagation,
/// multi-stage control signal threading) for the envelope-based control plane design.
/// That design is superseded by the epoch stream model: epoch boundaries are barriers natively,
/// and envelope-style message workflows (fan-out/fan-in with barrier alignment, thread safety
/// of shared message instances, fan-in reconciliation) are application-level concerns that
/// should be addressed by a dedicated message-workflow subsystem if ever required.
/// Code retained for reference.
/// </summary>
public class EnvelopeAdvancedTests
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
    /// Collector actor that tracks position of items.
    /// </summary>
    private class PositionTrackingEnvelopeCollectorActor : IStreamActor<IDataEnvelope, object>
    {
        private readonly List<(int position, IDataEnvelope envelope)> _collected;
        private int _position;

        public PositionTrackingEnvelopeCollectorActor(List<(int position, IDataEnvelope envelope)> collected)
        {
            _collected = collected;
            _position = 0;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<IDataEnvelope> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _collected.Add((_position++, item));
            }
            yield break;
        }
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task MultiPath_Broadcast_Should_Deliver_Control_Signals_To_All_Paths()
    {
        // This test demonstrates that control signals are broadcast to all downstream paths
        // even when the data items are also broadcast to all downstream paths.
        // Both data and control signals reach all consumers in broadcast mode.
        
        // Arrange
        var path1Results = new List<IDataEnvelope>();
        var path2Results = new List<IDataEnvelope>();

        // Create separate service providers for each consumer
        var services1 = new ServiceCollection();
        services1.AddScoped(_ => new EnvelopeCollectorActor(path1Results));
        var serviceProvider1 = services1.BuildServiceProvider();

        var services2 = new ServiceCollection();
        services2.AddScoped(_ => new EnvelopeCollectorActor(path2Results));
        var serviceProvider2 = services2.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceWithBarriers(ctx));
        
        var path1Transform = BlockHelpers.CreateSimpleEnvelopeTransformer<int, string>(
            "path1-transform", i => $"Path1-{i}");
        var path1Consumer = BlockHelpers.CreateActor<IDataEnvelope, object, EnvelopeCollectorActor>("path1-consumer", serviceProvider1.GetRequiredService<IServiceScopeFactory>());

        var path2Transform = BlockHelpers.CreateSimpleEnvelopeTransformer<int, string>(
            "path2-transform", i => $"Path2-{i}");
        var path2Consumer = BlockHelpers.CreateActor<IDataEnvelope, object, EnvelopeCollectorActor>("path2-consumer", serviceProvider2.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("multipath-flow");
        builder.AddBlock(producer)
            .AddBlock(path1Transform)
            .AddBlock(path1Consumer)
            .AddBlock(path2Transform)
            .AddBlock(path2Consumer);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        
        // Producer broadcasts to both paths
        builder.AddEdge(new Edge(producer, new[] { path1Transform, path2Transform }, envelopeStrategy));
        builder.AddEdge(new Edge(path1Transform, path1Consumer, envelopeStrategy));
        builder.AddEdge(new Edge(path2Transform, path2Consumer, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        // Both paths should receive all items (data + control)
        path1Results.Count.ShouldBe(5); // 3 data + 2 control
        path2Results.Count.ShouldBe(5);

        // Verify path 1
        path1Results[0].GetValue<string>().ShouldBe("Path1-1");
        path1Results[1].ShouldBeOfType<CheckpointBarrier>();
        path1Results[2].GetValue<string>().ShouldBe("Path1-2");
        path1Results[3].ShouldBeOfType<CheckpointBarrier>();
        path1Results[4].GetValue<string>().ShouldBe("Path1-3");

        // Verify path 2
        path2Results[0].GetValue<string>().ShouldBe("Path2-1");
        path2Results[1].ShouldBeOfType<CheckpointBarrier>();
        path2Results[2].GetValue<string>().ShouldBe("Path2-2");
        path2Results[3].ShouldBeOfType<CheckpointBarrier>();
        path2Results[4].GetValue<string>().ShouldBe("Path2-3");

        // Verify both paths got the same barriers
        var path1Barriers = path1Results.OfType<CheckpointBarrier>().ToList();
        var path2Barriers = path2Results.OfType<CheckpointBarrier>().ToList();
        path1Barriers.Count.ShouldBe(2);
        path2Barriers.Count.ShouldBe(2);
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task Pipeline_With_Multiple_Control_Signal_Types()
    {
        // Demonstrates a pipeline that processes multiple types of control signals
        
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var processedData = new List<int>();
        var checkpoints = new List<CheckpointBarrier>();
        var heartbeats = new List<Heartbeat>();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceAllControlTypes(ctx));
        var processor = BlockHelpers.CreateEnvelopeProcessor<int>(
            "processor",
            processData: async (value, ctx) =>
            {
                processedData.Add(value);
                await Task.CompletedTask;
            },
            processControl: async (signal, ctx) =>
            {
                switch (signal)
                {
                    case CheckpointBarrier barrier:
                        checkpoints.Add(barrier);
                        break;
                    case Heartbeat heartbeat:
                        heartbeats.Add(heartbeat);
                        break;
                }
                await Task.CompletedTask;
            });

        var builder = GraphHelpers.CreateGraphBuilder("control-types-flow");
        builder.AddBlock(producer)
            .AddBlock(processor);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        builder.AddEdge(new Edge(producer, processor, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        processedData.Count.ShouldBe(4);
        processedData.ShouldBe(new[] { 1, 2, 3, 4 });

        checkpoints.Count.ShouldBe(2);
        heartbeats.Count.ShouldBe(1);
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task Complex_Pipeline_Should_Maintain_Signal_Order()
    {
        // Tests a complex pipeline: Producer -> Transform -> Projector -> Processor
        // Verifies that control signals maintain their position relative to data items
        
        // Arrange
        var outputItems = new List<(int position, IDataEnvelope envelope)>();

        var services = new ServiceCollection();
        services.AddScoped(_ => new EnvelopeCollectorActor(outputItems.Select(x => x.envelope).ToList()));
        var serviceProvider = services.BuildServiceProvider();
        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceComplexStream(ctx));
        
        var transformer = BlockHelpers.CreateSimpleEnvelopeTransformer<int, int>(
            "transformer", i => i * 10);
        
        var projector = BlockHelpers.CreateEnvelopeProjector<int, int>(
            "projector", (i, ctx) => DuplicateAsync(i));
        
        // Create a custom actor that tracks position
        var processorActor = new PositionTrackingEnvelopeCollectorActor(outputItems);
        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => processorActor);
        var processorServiceProvider = processorServices.BuildServiceProvider();
        
        var processor = BlockHelpers.CreateActor<IDataEnvelope, object, PositionTrackingEnvelopeCollectorActor>("processor", processorServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("complex-pipeline-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AddBlock(projector)
            .AddBlock(processor);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        builder.AddEdge(new Edge(producer, transformer, envelopeStrategy));
        builder.AddEdge(new Edge(transformer, projector, envelopeStrategy));
        builder.AddEdge(new Edge(projector, processor, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        // Original: 1, Checkpoint, 2
        // After transform (*10): 10, Checkpoint, 20
        // After projector (duplicate): 10, 10, Checkpoint, 20, 20
        outputItems.Count.ShouldBe(5);
        
        outputItems[0].envelope.GetValue<int>().ShouldBe(10);
        outputItems[1].envelope.GetValue<int>().ShouldBe(10);
        outputItems[2].envelope.ShouldBeOfType<CheckpointBarrier>();
        outputItems[3].envelope.GetValue<int>().ShouldBe(20);
        outputItems[4].envelope.GetValue<int>().ShouldBe(20);
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task Heartbeat_Signals_Can_Track_Pipeline_Progress()
    {
        // Demonstrates using heartbeats for progress tracking
        
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var itemsProcessed = 0;
        var heartbeatsSeen = 0;
        var progressSnapshots = new List<int>();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceWithHeartbeats(ctx));
        var processor = BlockHelpers.CreateEnvelopeProcessor<int>(
            "processor",
            processData: async (value, ctx) =>
            {
                itemsProcessed++;
                // Note: In real scenarios, work happens here; test just increments counter
                await Task.CompletedTask;
            },
            processControl: async (signal, ctx) =>
            {
                if (signal is Heartbeat)
                {
                    heartbeatsSeen++;
                    progressSnapshots.Add(itemsProcessed);
                }
                await Task.CompletedTask;
            });

        var builder = GraphHelpers.CreateGraphBuilder("heartbeat-flow");
        builder.AddBlock(producer)
            .AddBlock(processor);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        builder.AddEdge(new Edge(producer, processor, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        itemsProcessed.ShouldBe(6);
        heartbeatsSeen.ShouldBe(2);
        
        // Progress should increase at each heartbeat
        progressSnapshots.Count.ShouldBe(2);
        progressSnapshots[0].ShouldBeGreaterThanOrEqualTo(0);
        progressSnapshots[1].ShouldBeGreaterThanOrEqualTo(progressSnapshots[0]);
    }

    // Helper methods to produce test data

    private static async IAsyncEnumerable<IDataEnvelope> ProduceWithBarriers(IExecutionContext ctx)
    {
        yield return new DataItem<int>(1);
        yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
        yield return new DataItem<int>(2);
        yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
        yield return new DataItem<int>(3);
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<IDataEnvelope> ProduceAllControlTypes(IExecutionContext ctx)
    {
        yield return new DataItem<int>(1);
        yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
        yield return new DataItem<int>(2);
        yield return new DataItem<int>(3);
        yield return new Heartbeat(DateTime.UtcNow);
        yield return new DataItem<int>(4);
        yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<IDataEnvelope> ProduceComplexStream(IExecutionContext ctx)
    {
        yield return new DataItem<int>(1);
        yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
        yield return new DataItem<int>(2);
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<IDataEnvelope> ProduceWithHeartbeats(IExecutionContext ctx)
    {
        yield return new DataItem<int>(1);
        yield return new DataItem<int>(2);
        yield return new Heartbeat(DateTime.UtcNow);
        yield return new DataItem<int>(3);
        yield return new DataItem<int>(4);
        yield return new Heartbeat(DateTime.UtcNow);
        yield return new DataItem<int>(5);
        yield return new DataItem<int>(6);
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<int> DuplicateAsync(int value)
    {
        yield return value;
        yield return value;
        await Task.CompletedTask;
    }
}
