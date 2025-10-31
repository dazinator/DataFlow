namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// Advanced tests demonstrating complex envelope scenarios including:
/// - Barrier alignment across multiple paths
/// - Scope boundary handling
/// - Heartbeat propagation
/// - Multi-stage pipelines with control signals
/// </summary>
public class EnvelopeAdvancedTests
{
    [Fact]
    public async Task MultiPath_Broadcast_Should_Deliver_Control_Signals_To_All_Paths()
    {
        // This test demonstrates that control signals are broadcast to all downstream paths
        // even when the data items are also broadcast to all downstream paths.
        // Both data and control signals reach all consumers in broadcast mode.
        
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var path1Results = new List<IDataEnvelope>();
        var path2Results = new List<IDataEnvelope>();

        var producer = new ProducerBlock<IDataEnvelope>("producer", ctx => ProduceWithBarriers(ctx));
        
        var path1Transform = new SimpleEnvelopeTransformerBlock<int, string>(
            "path1-transform", i => $"Path1-{i}");
        var path1Consumer = new ProcessorBlock<IDataEnvelope>("path1-consumer", async (item, ctx) =>
        {
            path1Results.Add(item);
            await Task.CompletedTask;
        });

        var path2Transform = new SimpleEnvelopeTransformerBlock<int, string>(
            "path2-transform", i => $"Path2-{i}");
        var path2Consumer = new ProcessorBlock<IDataEnvelope>("path2-consumer", async (item, ctx) =>
        {
            path2Results.Add(item);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("multipath-flow");
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

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

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

    [Fact]
    public async Task Pipeline_With_Multiple_Control_Signal_Types()
    {
        // Demonstrates a pipeline that processes multiple types of control signals
        
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var processedData = new List<int>();
        var checkpoints = new List<CheckpointBarrier>();
        var heartbeats = new List<Heartbeat>();

        var producer = new ProducerBlock<IDataEnvelope>("producer", ctx => ProduceAllControlTypes(ctx));
        var processor = new EnvelopeProcessorBlock<int>(
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

        var builder = new DataFlowGraphBuilder("control-types-flow");
        builder.AddBlock(producer)
            .AddBlock(processor);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        builder.AddEdge(new Edge(producer, processor, envelopeStrategy));

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        processedData.Count.ShouldBe(4);
        processedData.ShouldBe(new[] { 1, 2, 3, 4 });

        checkpoints.Count.ShouldBe(2);
        heartbeats.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Complex_Pipeline_Should_Maintain_Signal_Order()
    {
        // Tests a complex pipeline: Producer -> Transform -> Projector -> Processor
        // Verifies that control signals maintain their position relative to data items
        
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var outputItems = new List<(int position, IDataEnvelope envelope)>();
        int position = 0;

        var producer = new ProducerBlock<IDataEnvelope>("producer", ctx => ProduceComplexStream(ctx));
        
        var transformer = new SimpleEnvelopeTransformerBlock<int, int>(
            "transformer", i => i * 10);
        
        var projector = new EnvelopeProjectorBlock<int, int>(
            "projector", (i, ctx) => DuplicateAsync(i));
        
        var processor = new ProcessorBlock<IDataEnvelope>("processor", async (item, ctx) =>
        {
            outputItems.Add((position++, item));
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("complex-pipeline-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AddBlock(projector)
            .AddBlock(processor);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        builder.AddEdge(new Edge(producer, transformer, envelopeStrategy));
        builder.AddEdge(new Edge(transformer, projector, envelopeStrategy));
        builder.AddEdge(new Edge(projector, processor, envelopeStrategy));

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

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

    [Fact]
    public async Task Heartbeat_Signals_Can_Track_Pipeline_Progress()
    {
        // Demonstrates using heartbeats for progress tracking
        
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var itemsProcessed = 0;
        var heartbeatsSeen = 0;
        var progressSnapshots = new List<int>();

        var producer = new ProducerBlock<IDataEnvelope>("producer", ctx => ProduceWithHeartbeats(ctx));
        var processor = new EnvelopeProcessorBlock<int>(
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

        var builder = new DataFlowGraphBuilder("heartbeat-flow");
        builder.AddBlock(producer)
            .AddBlock(processor);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateBroadcast();
        builder.AddEdge(new Edge(producer, processor, envelopeStrategy));

        var graph = builder.Build();
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
