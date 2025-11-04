namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class EventChannelNodeTests
{
    [Fact]
    public void EventChannelNode_Should_Create_With_Valid_Parameters()
    {
        // Act
        var node = new EventChannelNode(typeof(EpochCreatedEvent), capacity: 100, name: "test-events");

        // Assert
        node.EventType.ShouldBe(typeof(EpochCreatedEvent));
        node.Capacity.ShouldBe(100);
        node.Name.ShouldBe("test-events");
        node.GetName().ShouldBe("test-events");
    }

    [Fact]
    public void EventChannelNode_Should_Have_Default_Name_When_Not_Specified()
    {
        // Act
        var node = new EventChannelNode(typeof(EpochCreatedEvent), capacity: 50);

        // Assert
        node.Name.ShouldBeNull();
        node.GetName().ShouldBe("<unnamed-event-channel>");
    }

    [Fact]
    public void EventChannelNode_Should_Throw_On_Null_EventType()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new EventChannelNode(null!, capacity: 100));
    }

    [Fact]
    public void EventChannelNode_Should_Throw_On_Invalid_Capacity()
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => 
            new EventChannelNode(typeof(EpochCreatedEvent), capacity: 0));
        
        Should.Throw<ArgumentOutOfRangeException>(() => 
            new EventChannelNode(typeof(EpochCreatedEvent), capacity: -1));
    }

    [Fact]
    public void EventChannelNode_Generic_Should_Create_With_Type_Safety()
    {
        // Act
        var node = new EventChannelNode<EpochCreatedEvent>(capacity: 100, name: "epoch-created");

        // Assert
        node.EventType.ShouldBe(typeof(EpochCreatedEvent));
        node.Capacity.ShouldBe(100);
        node.Name.ShouldBe("epoch-created");
    }

    [Fact]
    public void EventChannelNode_ToString_Should_Include_Details()
    {
        // Arrange
        var node = new EventChannelNode<GlobalAlignmentEvent>(capacity: 50, name: "alignment-events");

        // Act
        var result = node.ToString();

        // Assert
        result.ShouldContain("EventChannelNode");
        result.ShouldContain("alignment-events");
        result.ShouldContain("GlobalAlignmentEvent");
        result.ShouldContain("Capacity=50");
    }

    [Fact]
    public async Task EventChannelNode_Should_Deliver_Events_Sequentially_To_Single_Consumer()
    {
        // Arrange
        var receivedEvents = new List<EpochCreatedEvent>();
        var services = new ServiceCollection().BuildServiceProvider();

        // Create an event producer
        var producer = new ProducerBlock<EpochCreatedEvent>("event-producer", ctx => ProduceEpochCreatedEvents(5));

        // Create an event consumer
        var consumer = new ProcessorBlock<EpochCreatedEvent>("event-consumer", async (evt, ctx) =>
        {
            lock (receivedEvents)
            {
                receivedEvents.Add(evt);
            }
            await Task.CompletedTask;
        });

        // Build graph with event channel
        var builder = new DataFlowGraphBuilder("event-channel-test");
        var eventChannel = builder.EventChannel<EpochCreatedEvent>(capacity: 10, name: "epoch-created");
        
        builder.AddBlock(producer)
            .AddBlock(consumer)
            .Connect(producer, eventChannel)
            .Connect(eventChannel, consumer);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        receivedEvents.Count.ShouldBe(5);
        for (int i = 0; i < 5; i++)
        {
            receivedEvents[i].Epoch.ToString().ShouldContain($"source1={i + 1}");
        }
    }

    [Fact]
    public async Task EventChannelNode_Should_Broadcast_Events_To_Multiple_Consumers()
    {
        // Arrange
        var consumer1Events = new List<GlobalAlignmentEvent>();
        var consumer2Events = new List<GlobalAlignmentEvent>();
        var services = new ServiceCollection().BuildServiceProvider();

        // Create an event producer
        var producer = new ProducerBlock<GlobalAlignmentEvent>("event-producer", 
            ctx => ProduceGlobalAlignmentEvents(3));

        // Create two event consumers
        var consumer1 = new ProcessorBlock<GlobalAlignmentEvent>("consumer1", async (evt, ctx) =>
        {
            lock (consumer1Events)
            {
                consumer1Events.Add(evt);
            }
            await Task.Delay(10); // Simulate processing time
        });

        var consumer2 = new ProcessorBlock<GlobalAlignmentEvent>("consumer2", async (evt, ctx) =>
        {
            lock (consumer2Events)
            {
                consumer2Events.Add(evt);
            }
            await Task.Delay(10); // Simulate processing time
        });

        // Build graph with event channel
        var builder = new DataFlowGraphBuilder("broadcast-event-test");
        var eventChannel = builder.EventChannel<GlobalAlignmentEvent>(capacity: 10, name: "alignment-events");
        
        builder.AddBlock(producer)
            .AddBlock(consumer1)
            .AddBlock(consumer2)
            .Connect(producer, eventChannel)
            .Connect(eventChannel, consumer1)
            .Connect(eventChannel, consumer2);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        consumer1Events.Count.ShouldBe(3);
        consumer2Events.Count.ShouldBe(3);
        
        // Both consumers should receive the same events
        for (int i = 0; i < 3; i++)
        {
            consumer1Events[i].Watermark.ShouldBe(consumer2Events[i].Watermark);
        }
    }

    [Fact]
    public async Task EventChannelNode_Should_Handle_Different_Event_Types_In_Separate_Channels()
    {
        // Arrange
        var createdEvents = new List<EpochCreatedEvent>();
        var alignedEvents = new List<GlobalAlignmentEvent>();
        var services = new ServiceCollection().BuildServiceProvider();

        // Create producers for different event types
        var createdProducer = new ProducerBlock<EpochCreatedEvent>("created-producer", 
            ctx => ProduceEpochCreatedEvents(3));
        var alignedProducer = new ProducerBlock<GlobalAlignmentEvent>("aligned-producer", 
            ctx => ProduceGlobalAlignmentEvents(2));

        // Create consumers for different event types
        var createdConsumer = new ProcessorBlock<EpochCreatedEvent>("created-consumer", async (evt, ctx) =>
        {
            lock (createdEvents) { createdEvents.Add(evt); }
            await Task.CompletedTask;
        });
        var alignedConsumer = new ProcessorBlock<GlobalAlignmentEvent>("aligned-consumer", async (evt, ctx) =>
        {
            lock (alignedEvents) { alignedEvents.Add(evt); }
            await Task.CompletedTask;
        });

        // Build graph with separate event channels
        var builder = new DataFlowGraphBuilder("multi-event-type-test");
        var createdChannel = builder.EventChannel<EpochCreatedEvent>(capacity: 10, name: "created-events");
        var alignedChannel = builder.EventChannel<GlobalAlignmentEvent>(capacity: 10, name: "aligned-events");
        
        builder.AddBlock(createdProducer)
            .AddBlock(alignedProducer)
            .AddBlock(createdConsumer)
            .AddBlock(alignedConsumer)
            .Connect(createdProducer, createdChannel)
            .Connect(createdChannel, createdConsumer)
            .Connect(alignedProducer, alignedChannel)
            .Connect(alignedChannel, alignedConsumer);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        createdEvents.Count.ShouldBe(3);
        alignedEvents.Count.ShouldBe(2);
    }

    [Fact]
    public async Task EventChannelNode_Should_Maintain_Event_Order_With_Sequential_Delivery()
    {
        // Arrange
        var receivedEvents = new List<EpochCreatedEvent>();
        var receivedOrder = new List<int>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<EpochCreatedEvent>("event-producer", 
            ctx => ProduceEpochCreatedEvents(10));

        var consumer = new ProcessorBlock<EpochCreatedEvent>("event-consumer", async (evt, ctx) =>
        {
            var sequence = int.Parse(evt.Epoch.ToString().Split('=')[1].TrimEnd('}'));
            lock (receivedOrder)
            {
                receivedOrder.Add(sequence);
                receivedEvents.Add(evt);
            }
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("order-test");
        var eventChannel = builder.EventChannel<EpochCreatedEvent>(capacity: 5, name: "ordered-events");
        
        builder.AddBlock(producer)
            .AddBlock(consumer)
            .Connect(producer, eventChannel)
            .Connect(eventChannel, consumer);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        receivedOrder.Count.ShouldBe(10);
        receivedOrder.ShouldBe(Enumerable.Range(1, 10));
    }

    // Helper methods

    private static async IAsyncEnumerable<EpochCreatedEvent> ProduceEpochCreatedEvents(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            var epoch = EpochVector.FromSingleSource("source1", i);
            var blockContext = new BlockContext($"block-{i}");
            yield return new EpochCreatedEvent(epoch, blockContext);
            await Task.Delay(1); // Simulate async work
        }
    }

    private static async IAsyncEnumerable<GlobalAlignmentEvent> ProduceGlobalAlignmentEvents(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            var watermark = EpochVector.FromSingleSource("source1", i);
            yield return new GlobalAlignmentEvent(watermark);
            await Task.Delay(1); // Simulate async work
        }
    }
}
