using DataFlow.Blazor.Events;
using DataFlow.Blazor.Services;
using Xunit;

namespace DataFlow.Blazor.Tests;

/// <summary>
/// Tests for the EventProcessor state management
/// </summary>
public class EventProcessorTests
{
    [Fact]
    public void EventProcessor_InitializesWithCorrectInvocationId()
    {
        // Arrange
        var invocationId = Guid.NewGuid();

        // Act
        var processor = new EventProcessor(invocationId);

        // Assert
        Assert.Equal(invocationId, processor.State.InvocationId);
    }

    [Fact]
    public void EventProcessor_ProcessesFlowStartedEvent()
    {
        // Arrange
        var processor = new EventProcessor(Guid.NewGuid());
        var evt = new FlowStartedEvent(processor.State.InvocationId, "Test Flow", DateTime.UtcNow);

        // Act
        processor.ProcessEvent(evt);

        // Assert
        Assert.Equal("Test Flow", processor.State.FlowName);
        Assert.Equal(FlowState.Running, processor.State.State);
        Assert.NotNull(processor.State.StartTime);
    }

    [Fact]
    public void EventProcessor_ProcessesFlowCompletedEvent()
    {
        // Arrange
        var processor = new EventProcessor(Guid.NewGuid());
        var startEvt = new FlowStartedEvent(processor.State.InvocationId, "Test Flow", DateTime.UtcNow);
        var completeEvt = new FlowCompletedEvent(processor.State.InvocationId, true, DateTime.UtcNow, null);

        // Act
        processor.ProcessEvent(startEvt);
        processor.ProcessEvent(completeEvt);

        // Assert
        Assert.Equal(FlowState.Completed, processor.State.State);
        Assert.NotNull(processor.State.EndTime);
        Assert.Null(processor.State.ErrorMessage);
    }

    [Fact]
    public void EventProcessor_ProcessesFlowCompletedEventWithError()
    {
        // Arrange
        var processor = new EventProcessor(Guid.NewGuid());
        var startEvt = new FlowStartedEvent(processor.State.InvocationId, "Test Flow", DateTime.UtcNow);
        var completeEvt = new FlowCompletedEvent(processor.State.InvocationId, false, DateTime.UtcNow, "Test error");

        // Act
        processor.ProcessEvent(startEvt);
        processor.ProcessEvent(completeEvt);

        // Assert
        Assert.Equal(FlowState.Failed, processor.State.State);
        Assert.Equal("Test error", processor.State.ErrorMessage);
    }

    [Fact]
    public void EventProcessor_ProcessesBlockStartedEvent()
    {
        // Arrange
        var processor = new EventProcessor(Guid.NewGuid());
        var evt = new BlockStartedEvent("producer", "ProducerBlock", DateTime.UtcNow);

        // Act
        processor.ProcessEvent(evt);

        // Assert
        Assert.True(processor.State.Blocks.ContainsKey("producer"));
        Assert.Equal("ProducerBlock", processor.State.Blocks["producer"].BlockType);
        Assert.Equal(Events.BlockState.Running, processor.State.Blocks["producer"].State);
    }

    [Fact]
    public void EventProcessor_ProcessesBlockMetricsEvent()
    {
        // Arrange
        var processor = new EventProcessor(Guid.NewGuid());
        var startEvt = new BlockStartedEvent("producer", "ProducerBlock", DateTime.UtcNow);
        var metricsEvt = new BlockMetricsEvent("producer", ItemsConsumed: 0, ItemsProduced: 100, DateTime.UtcNow);

        // Act
        processor.ProcessEvent(startEvt);
        processor.ProcessEvent(metricsEvt);

        // Assert
        Assert.Equal(0,   processor.State.Blocks["producer"].ItemsConsumed);
        Assert.Equal(100, processor.State.Blocks["producer"].ItemsProduced);
    }

    [Fact]
    public void EventProcessor_ProcessesBlockCompletedEvent()
    {
        // Arrange
        var processor = new EventProcessor(Guid.NewGuid());
        var startEvt = new BlockStartedEvent("producer", "ProducerBlock", DateTime.UtcNow);
        var completeEvt = new BlockCompletedEvent("producer", true, DateTime.UtcNow, null);

        // Act
        processor.ProcessEvent(startEvt);
        processor.ProcessEvent(completeEvt);

        // Assert
        Assert.Equal(Events.BlockState.Completed, processor.State.Blocks["producer"].State);
        Assert.NotNull(processor.State.Blocks["producer"].EndTime);
    }

    [Fact]
    public void EventProcessor_ProcessesChannelStatsEvent()
    {
        // Arrange
        var processor = new EventProcessor(Guid.NewGuid());
        var blockStartEvt = new BlockStartedEvent("producer", "ProducerBlock", DateTime.UtcNow);
        var channelEvt = new ChannelStatsEvent("producer", "transform", 100, 50, DateTime.UtcNow);

        // Act
        processor.ProcessEvent(blockStartEvt);
        processor.ProcessEvent(channelEvt);

        // Assert
        var key = ("producer", "transform");
        Assert.True(processor.State.Channels.ContainsKey(key));
        Assert.Equal(100, processor.State.Channels[key].BufferCapacity);
        Assert.Equal(50, processor.State.Channels[key].CurrentCount);
    }

    [Fact]
    public void EventProcessor_CalculatesTotalSourceItemsIngested()
    {
        // Arrange - block1 is a source, block2 is a downstream transform
        var processor = new EventProcessor(Guid.NewGuid());
        processor.ProcessEvent(new BlockStartedEvent("block1", "ProducerBlock", DateTime.UtcNow, IsSource: true));
        processor.ProcessEvent(new BlockStartedEvent("block2", "TransformBlock", DateTime.UtcNow, IsSource: false));
        processor.ProcessEvent(new BlockMetricsEvent("block1", ItemsConsumed: 0, ItemsProduced: 100, DateTime.UtcNow));
        processor.ProcessEvent(new BlockMetricsEvent("block2", ItemsConsumed: 200, ItemsProduced: 200, DateTime.UtcNow));

        // Act — only source blocks count; transform items are double-counted if summed across all blocks.
        // Source blocks have consumed=0 and output=N; TotalSourceItemsIngested sums ItemsProduced for sources.
        var totalItems = processor.State.TotalSourceItemsIngested;

        // Assert
        Assert.Equal(100, totalItems);
    }

    [Fact]
    public void EventProcessor_AppliesSnapshot()
    {
        // Arrange
        var processor = new EventProcessor(Guid.NewGuid());
        var snapshot = new FlowSnapshot(
            InvocationId: processor.State.InvocationId,
            FlowName: "Snapshot Flow",
            StartTime: DateTime.UtcNow,
            State: FlowState.Running,
            CompletedAt: null,
            ErrorMessage: null,
            Blocks: new Dictionary<string, BlockSnapshot>
            {
                ["producer"] = new BlockSnapshot(
                    BlockName: "producer",
                    BlockType: "ProducerBlock",
                    State: Events.BlockState.Running,
                    ItemsConsumed: 0,
                    ItemsProduced: 500,
                    StartTime: DateTime.UtcNow,
                    EndTime: null,
                    ErrorMessage: null
                )
            },
            Channels: new Dictionary<string, ChannelSnapshot>()
        );

        // Act
        processor.ApplySnapshot(snapshot);

        // Assert
        Assert.Equal("Snapshot Flow", processor.State.FlowName);
        Assert.True(processor.State.Blocks.ContainsKey("producer"));
        Assert.Equal(500, processor.State.Blocks["producer"].ItemsProduced);
    }
}
