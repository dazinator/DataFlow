using Bunit;
using DataFlow.Blazor.Components;
using DataFlow.Blazor.Events;
using DataFlow.Blazor.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataFlow.Blazor.Tests;

/// <summary>
/// Tests for the FlowVisualization component
/// </summary>
public class FlowVisualizationComponentTests : TestContext
{
    private readonly Guid _testInvocationId = Guid.NewGuid();

    public FlowVisualizationComponentTests()
    {
        // Register mock event source for all tests
        Services.AddSingleton<IEventSource, MockEventSource>();
    }

    [Fact]
    public void FlowVisualization_RendersWithoutErrors()
    {
        // Act
        var cut = RenderComponent<FlowVisualization>(parameters => parameters
            .Add(p => p.InvocationId, _testInvocationId));

        // Assert
        Assert.NotNull(cut);
        Assert.Contains("flow-visualization", cut.Markup);
    }

    [Fact]
    public void FlowVisualization_EventuallyShowsContent()
    {
        // Act
        var cut = RenderComponent<FlowVisualization>(parameters => parameters
            .Add(p => p.InvocationId, _testInvocationId));

        // Give it time to process events and render
        cut.WaitForState(() => !cut.Markup.Contains("Loading"), timeout: TimeSpan.FromSeconds(3));

        // Assert - should have div element
        Assert.Contains("flow-visualization", cut.Markup);
    }

    [Fact]
    public void FlowVisualization_CanAcceptEventSourceOverride()
    {
        // Arrange
        var customEventSource = new MockEventSource();

        // Act
        var cut = RenderComponent<FlowVisualization>(parameters => parameters
            .Add(p => p.InvocationId, _testInvocationId)
            .Add(p => p.EventSourceOverride, customEventSource));

        // Assert
        Assert.NotNull(cut);
    }
}
