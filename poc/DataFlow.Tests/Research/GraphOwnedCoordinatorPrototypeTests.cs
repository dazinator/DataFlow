using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Registry;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataFlow.POC.Tests.Research;

/// <summary>
/// Validation tests for the graph-owned coordinator prototype.
/// These tests demonstrate that the coordinator ownership model works correctly.
/// </summary>
public class GraphOwnedCoordinatorPrototypeTests
{
    [Fact]
    public void EpochSourceNode_CanBeCreated_WithoutCoordinator()
    {
        // Arrange & Act
        var node = new EpochSourceNode();
        
        // Assert
        Assert.NotNull(node);
        Assert.NotNull(node.EpochReader);
    }
    
    [Fact]
    public void Graph_ExposesCoordinator_WhenEpochsConfigured()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var registry = new BlockTypeRegistry();
        
        var builder = new DataFlowGraphBuilder("test-graph", serviceProvider, registry);
        
        // Act
        builder.ConfigureEpochs(config =>
        {
            config.SetPolicy(EpochPolicy.ByCount(100));
            config.AddProcessor("processor1");
        });
        
        var graph = builder.Build();
        
        // Assert
        Assert.NotNull(graph.EpochCoordinator);
        Assert.NotNull(graph.EpochSource);
    }
    
    [Fact]
    public void Graph_HasNullCoordinator_WhenEpochsNotConfigured()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var registry = new BlockTypeRegistry();
        
        var builder = new DataFlowGraphBuilder("test-graph", serviceProvider, registry);
        
        // Act - Don't configure epochs
        var graph = builder.Build();
        
        // Assert
        Assert.Null(graph.EpochCoordinator);
        Assert.Null(graph.EpochSource);
    }
    
    [Fact]
    public void MultipleGraphs_HaveIndependentCoordinators()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var registry = new BlockTypeRegistry();
        
        // Act - Create two graphs with epochs
        var builder1 = new DataFlowGraphBuilder("graph-1", serviceProvider, registry);
        builder1.ConfigureEpochs(config =>
        {
            config.SetPolicy(EpochPolicy.ByCount(100));
            config.AddProcessor("processor1");
        });
        var graph1 = builder1.Build();
        
        var builder2 = new DataFlowGraphBuilder("graph-2", serviceProvider, registry);
        builder2.ConfigureEpochs(config =>
        {
            config.SetPolicy(EpochPolicy.ByCount(200));
            config.AddProcessor("processor2");
        });
        var graph2 = builder2.Build();
        
        // Assert - Different coordinator instances
        Assert.NotNull(graph1.EpochCoordinator);
        Assert.NotNull(graph2.EpochCoordinator);
        Assert.NotSame(graph1.EpochCoordinator, graph2.EpochCoordinator);
    }
    
    [Fact]
    public void ConfigureEpochs_WorksWithoutFactory_WhenServiceProviderAvailable()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var registry = new BlockTypeRegistry();
        
        var builder = new DataFlowGraphBuilder("test-graph", serviceProvider, registry);
        
        // Act - No factory parameter provided
        builder.ConfigureEpochs(config =>
        {
            config.SetPolicy(EpochPolicy.ByCount(100));
            config.AddProcessor("processor1");
        });
        
        var graph = builder.Build();
        
        // Assert
        Assert.NotNull(graph);
        Assert.NotNull(graph.EpochCoordinator);
    }
    
    [Fact]
    public void ConfigureEpochs_StillWorksWithExplicitFactory_BackwardCompatibility()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var registry = new BlockTypeRegistry();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var customCoordinator = new EpochCoordinator(scopeFactory);
        
        var builder = new DataFlowGraphBuilder("test-graph", serviceProvider, registry);
        
        // Act - Explicit factory provided (backward compatibility)
        builder.ConfigureEpochs(
            config =>
            {
                config.SetPolicy(EpochPolicy.ByCount(100));
                config.AddProcessor("processor1");
            },
            _ => customCoordinator  // Custom coordinator
        );
        
        var graph = builder.Build();
        
        // Assert
        Assert.NotNull(graph);
        Assert.NotNull(graph.EpochCoordinator);
        Assert.Same(customCoordinator, graph.EpochCoordinator);
    }
}
