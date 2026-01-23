using DataFlow.Blazor.Events;
using DataFlow.Blazor.Services;
using Xunit;

namespace DataFlow.Blazor.Tests;

/// <summary>
/// Tests for the MockEventSource
/// </summary>
public class MockEventSourceTests
{
    [Fact]
    public async Task MockEventSource_GeneratesFlowStartedEvent()
    {
        // Arrange
        var source = new MockEventSource();
        var invocationId = Guid.NewGuid();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var events = new List<object>();
        await foreach (var evt in source.GetEventsAsync(invocationId, cts.Token))
        {
            events.Add(evt);
            if (evt is FlowStartedEvent)
                break; // Got what we need
        }

        // Assert
        Assert.Contains(events, e => e is FlowStartedEvent);
    }

    [Fact]
    public async Task MockEventSource_GeneratesBlockEvents()
    {
        // Arrange
        var source = new MockEventSource();
        var invocationId = Guid.NewGuid();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var events = new List<object>();
        await foreach (var evt in source.GetEventsAsync(invocationId, cts.Token))
        {
            events.Add(evt);
            if (events.Count >= 10) // Collect several events
                break;
        }

        // Assert
        Assert.Contains(events, e => e is BlockStartedEvent);
    }

    [Fact]
    public async Task MockEventSource_ReturnsNullSnapshotInitially()
    {
        // Arrange
        var source = new MockEventSource();
        var invocationId = Guid.NewGuid();

        // Act
        var snapshot = await source.GetSnapshotAsync(invocationId);

        // Assert
        Assert.Null(snapshot); // Mock doesn't support snapshots yet
    }
}
