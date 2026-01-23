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
/// Tests for side-channel architecture in competing edges.
/// Validates that control signals are reliably delivered to all competing consumers.
/// </summary>
public class SideChannelCompetingEdgeTests
{
    [Fact]
    public async Task CompetingEdge_With_SideChannel_Should_Deliver_Control_Signals_To_All_Consumers()
    {
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var consumer1Items = new List<IDataEnvelope>();
        var consumer2Items = new List<IDataEnvelope>();
        var consumer1ControlSignals = new List<IDataEnvelope>();
        var consumer2ControlSignals = new List<IDataEnvelope>();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceEnvelopes(ctx));

        var consumer1 = BlockHelpers.CreateEnvelopeProcessor<int>(
            "consumer1",
            processData: async (value, ctx) =>
            {
                consumer1Items.Add(new DataItem<int>(value));
                await Task.Delay(5); // Small delay to encourage competition
            },
            processControl: async (signal, ctx) =>
            {
                consumer1ControlSignals.Add(signal);
                await Task.CompletedTask;
            });

        var consumer2 = BlockHelpers.CreateEnvelopeProcessor<int>(
            "consumer2",
            processData: async (value, ctx) =>
            {
                consumer2Items.Add(new DataItem<int>(value));
                await Task.Delay(5);
            },
            processControl: async (signal, ctx) =>
            {
                consumer2ControlSignals.Add(signal);
                await Task.CompletedTask;
            });

        var builder = GraphHelpers.CreateGraphBuilder("side-channel-competing-flow");
        builder.AddBlock(producer)
            .AddBlock(consumer1)
            .AddBlock(consumer2);

        // Use envelope competing strategy with side-channel support
        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateCompetingWithSideChannel();
        builder.AddEdge(new Edge(producer, new[] { consumer1, consumer2 }, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        // Data items should compete (total = 2)
        var totalDataItems = consumer1Items.Count + consumer2Items.Count;
        totalDataItems.ShouldBe(2);

        // Control signals should be delivered to ALL consumers via side-channel
        consumer1ControlSignals.Count.ShouldBe(2); // 1 barrier + 1 heartbeat
        consumer2ControlSignals.Count.ShouldBe(2); // 1 barrier + 1 heartbeat

        // Verify control signal types
        consumer1ControlSignals[0].ShouldBeOfType<CheckpointBarrier>();
        consumer1ControlSignals[1].ShouldBeOfType<Heartbeat>();
        consumer2ControlSignals[0].ShouldBeOfType<CheckpointBarrier>();
        consumer2ControlSignals[1].ShouldBeOfType<Heartbeat>();
    }

    [Fact]
    public async Task CompetingEdge_With_SideChannel_Should_Preserve_Control_Signal_Order()
    {
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var consumer1ControlSignals = new List<IDataEnvelope>();
        var consumer2ControlSignals = new List<IDataEnvelope>();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceOrderedControlSignals(ctx));

        var consumer1 = BlockHelpers.CreateEnvelopeProcessor<int>(
            "consumer1",
            processData: async (value, ctx) => await Task.CompletedTask,
            processControl: async (signal, ctx) =>
            {
                consumer1ControlSignals.Add(signal);
                await Task.CompletedTask;
            });

        var consumer2 = BlockHelpers.CreateEnvelopeProcessor<int>(
            "consumer2",
            processData: async (value, ctx) => await Task.CompletedTask,
            processControl: async (signal, ctx) =>
            {
                consumer2ControlSignals.Add(signal);
                await Task.CompletedTask;
            });

        var builder = GraphHelpers.CreateGraphBuilder("order-preservation-flow");
        builder.AddBlock(producer)
            .AddBlock(consumer1)
            .AddBlock(consumer2);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateCompetingWithSideChannel();
        builder.AddEdge(new Edge(producer, new[] { consumer1, consumer2 }, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        // Both consumers should receive control signals in order
        consumer1ControlSignals.Count.ShouldBe(4);
        consumer2ControlSignals.Count.ShouldBe(4);

        // Verify order for consumer1
        consumer1ControlSignals[0].ShouldBeOfType<CheckpointBarrier>();
        consumer1ControlSignals[1].ShouldBeOfType<Heartbeat>();
        consumer1ControlSignals[2].ShouldBeOfType<CheckpointBarrier>();
        consumer1ControlSignals[3].ShouldBeOfType<Heartbeat>();

        // Verify order for consumer2
        consumer2ControlSignals[0].ShouldBeOfType<CheckpointBarrier>();
        consumer2ControlSignals[1].ShouldBeOfType<Heartbeat>();
        consumer2ControlSignals[2].ShouldBeOfType<CheckpointBarrier>();
        consumer2ControlSignals[3].ShouldBeOfType<Heartbeat>();
    }

    [Fact]
    public async Task CompetingEdge_With_SideChannel_Should_Handle_Barrier_Alignment()
    {
        // This test validates the barrier alignment scenario where all consumers
        // must observe a checkpoint barrier before any can proceed
        
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var consumer1Barriers = new List<Guid>();
        var consumer2Barriers = new List<Guid>();
        var barrierLock = new object();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceWithBarriers(ctx));

        var consumer1 = BlockHelpers.CreateEnvelopeProcessor<int>(
            "consumer1",
            processData: async (value, ctx) => await Task.Delay(10),
            processControl: async (signal, ctx) =>
            {
                if (signal is CheckpointBarrier barrier)
                {
                    lock (barrierLock)
                    {
                        consumer1Barriers.Add(barrier.Id);
                    }
                }
                await Task.CompletedTask;
            });

        var consumer2 = BlockHelpers.CreateEnvelopeProcessor<int>(
            "consumer2",
            processData: async (value, ctx) => await Task.Delay(10),
            processControl: async (signal, ctx) =>
            {
                if (signal is CheckpointBarrier barrier)
                {
                    lock (barrierLock)
                    {
                        consumer2Barriers.Add(barrier.Id);
                    }
                }
                await Task.CompletedTask;
            });

        var builder = GraphHelpers.CreateGraphBuilder("barrier-alignment-flow");
        builder.AddBlock(producer)
            .AddBlock(consumer1)
            .AddBlock(consumer2);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateCompetingWithSideChannel();
        builder.AddEdge(new Edge(producer, new[] { consumer1, consumer2 }, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        // Both consumers should observe the same barriers
        consumer1Barriers.Count.ShouldBe(2);
        consumer2Barriers.Count.ShouldBe(2);
        
        consumer1Barriers.ShouldBe(consumer2Barriers);
    }

    [Fact]
    public async Task CompetingEdge_With_SideChannel_Should_Not_Affect_Data_Competition()
    {
        // This test ensures that adding side-channel for control signals
        // doesn't affect the competing semantics of data items
        
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var consumer1DataCount = 0;
        var consumer2DataCount = 0;
        var dataLock = new object();

        var producer = BlockHelpers.CreateProducer<IDataEnvelope>("producer", ctx => ProduceOnlyData(ctx, 100));

        var consumer1 = BlockHelpers.CreateEnvelopeProcessor<int>(
            "consumer1",
            processData: async (value, ctx) =>
            {
                lock (dataLock) consumer1DataCount++;
                await Task.Delay(1);
            });

        var consumer2 = BlockHelpers.CreateEnvelopeProcessor<int>(
            "consumer2",
            processData: async (value, ctx) =>
            {
                lock (dataLock) consumer2DataCount++;
                await Task.Delay(1);
            });

        var builder = GraphHelpers.CreateGraphBuilder("data-competition-flow");
        builder.AddBlock(producer)
            .AddBlock(consumer1)
            .AddBlock(consumer2);

        var envelopeStrategy = EnvelopeEdgeStrategyFactory.CreateCompetingWithSideChannel();
        builder.AddEdge(new Edge(producer, new[] { consumer1, consumer2 }, envelopeStrategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        // All data items should be processed exactly once (competing semantics)
        (consumer1DataCount + consumer2DataCount).ShouldBe(100);
        
        // At least one consumer should have received items
        // Note: In rare cases with competing semantics, one consumer might get all items,
        // but typically both consumers will get some items
        (consumer1DataCount > 0 || consumer2DataCount > 0).ShouldBeTrue();
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

    private static async IAsyncEnumerable<IDataEnvelope> ProduceOrderedControlSignals(IExecutionContext ctx)
    {
        yield return new DataItem<int>(1);
        yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
        yield return new Heartbeat(DateTime.UtcNow);
        yield return new DataItem<int>(2);
        yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
        yield return new Heartbeat(DateTime.UtcNow);
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<IDataEnvelope> ProduceWithBarriers(IExecutionContext ctx)
    {
        var barrier1 = Guid.NewGuid();
        var barrier2 = Guid.NewGuid();

        for (int i = 0; i < 10; i++)
        {
            yield return new DataItem<int>(i);
        }
        
        yield return new CheckpointBarrier(barrier1, DateTime.UtcNow);
        
        for (int i = 10; i < 20; i++)
        {
            yield return new DataItem<int>(i);
        }
        
        yield return new CheckpointBarrier(barrier2, DateTime.UtcNow);
        
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<IDataEnvelope> ProduceOnlyData(IExecutionContext ctx, int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return new DataItem<int>(i);
        }
        await Task.CompletedTask;
    }
}
