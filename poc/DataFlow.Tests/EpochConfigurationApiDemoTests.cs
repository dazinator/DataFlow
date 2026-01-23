namespace DataFlow.POC.Tests;

using DataFlow.POC.Builder;
using DataFlow.POC.Checkpointing;
using DataFlow.POC.Core;
using DataFlow.POC.Registry;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// Demonstrates the improved ConfigureEpochs API that no longer requires
/// manual factory functions when using DI.
/// </summary>
public class EpochConfigurationApiDemoTests
{
    [Fact]
    public void ImprovedApi_ConfigureEpochs_WithoutFactory()
    {
        // Arrange - Setup DI with IServiceScopeFactory
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        
        var registry = new BlockTypeRegistry();
        var builder = new DataFlowGraphBuilder("demo");
        
        // Act - CLEAN API: No factory needed!
        builder.ConfigureEpochs(config =>
        {
            config.SetPolicy(EpochPolicy.ByCount(100));
            config.AddProcessor("processor1");
            config.OnBeginEpoch(async (epoch, ct) => 
            {
                // Begin transaction logic
                await Task.CompletedTask;
            });
            config.OnCommitEpoch(async (epoch, ct) => 
            {
                // Commit transaction logic
                await Task.CompletedTask;
            });
        });
        // ^^^ Notice: No awkward factory function required!
        
        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        
        // Assert
        graph.ShouldNotBeNull();
        graph.Name.ShouldBe("demo");
    }
    
    [Fact]
    public void BackwardCompatibility_ConfigureEpochs_WithExplicitFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var registry = new BlockTypeRegistry();
        var builder = new DataFlowGraphBuilder("demo");
        
        // Act - OLD API: Explicit factory still works for advanced scenarios
        builder.ConfigureEpochs(
            config =>
            {
                config.SetPolicy(EpochPolicy.ByCount(100));
                config.AddProcessor("processor1");
            },
            checkpointStrategy =>
            {
                // Custom coordinator implementation or configuration
                var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
                return new EpochCoordinator(
                    scopeFactory,
                    operationsQueueCapacity: 200, // Custom capacity
                    checkpointStrategy: checkpointStrategy);
            });
        
        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        
        // Assert
        graph.ShouldNotBeNull();
    }
    
    [Fact]
    public void Comparison_BeforeAndAfter()
    {
        // This test demonstrates the difference between old and new API
        
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var registry = new BlockTypeRegistry();
        
        // === BEFORE (Awkward) ===
        var builderBefore = new DataFlowGraphBuilder("before");
        builderBefore.ConfigureEpochs(
            config =>
            {
                config.SetPolicy(EpochPolicy.ByCount(1000));
                config.AddProcessor("order-processor");
            },
            // ❌ Developer must manually create coordinator with IServiceScopeFactory
            checkpointStrategy =>
            {
                var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
                return new EpochCoordinator(scopeFactory, checkpointStrategy: checkpointStrategy);
            });
        
        // === AFTER (Clean) ===
        var builderAfter = new DataFlowGraphBuilder("after");
        builderAfter.ConfigureEpochs(config =>
        {
            config.SetPolicy(EpochPolicy.ByCount(1000));
            config.AddProcessor("order-processor");
        });
        // ✅ Factory is optional - DI handles it automatically!
        
        // Both should work
        var graphBefore = builderBefore.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var graphAfter = builderAfter.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        
        graphBefore.ShouldNotBeNull();
        graphAfter.ShouldNotBeNull();
    }
}
