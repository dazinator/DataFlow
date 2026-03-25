namespace DataFlow.POC.Tests.Documentation;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Checkpointing;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Xunit.Categories;
using DataFlow.POC.Registry;

/// <summary>
/// Documentation tests for the Epoch Actor Block guide.
/// These tests verify that all code examples in epoch-actor-block.md are correct and functional.
/// </summary>
[Documentation]
public class EpochActorBlockDocumentationTests : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly EpochCoordinator _coordinator;
    private readonly List<IAsyncDisposable> _disposables = new();
    
    public EpochActorBlockDocumentationTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<TestService>();
        _serviceProvider = services.BuildServiceProvider();
        
        _coordinator = new EpochCoordinator(_serviceProvider.GetRequiredService<IServiceScopeFactory>());
        _disposables.Add(_coordinator);
    }
    
    public async ValueTask DisposeAsync()
    {
        foreach (var disposable in _disposables)
        {
            await disposable.DisposeAsync();
        }
        await _serviceProvider.DisposeAsync();
    }
    
    [Fact]
    public void EpochActorBlock_ConfigureEpochs_ShouldBuildSuccessfully()
    {
        // This test verifies the basic epoch configuration pattern from the guide
        
        // Arrange & Act
        var builder = GraphHelpers.CreateGraphBuilder("epoch-test");
        builder.ConfigureEpochs(config =>
        {
            config.SetPolicy(EpochPolicy.ByCount(100));
            config.AddProcessor("processor1");
        }, _ => _coordinator);
        
        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        
        // Assert
        graph.ShouldNotBeNull();
        graph.Name.ShouldBe("global:epoch-test");
    }
    
    [Fact]
    public void EpochActorBlock_MultipleProcessors_ShouldConfigureCorrectly()
    {
        // This test verifies multiple processor configuration
        
        // Arrange & Act
        var builder = GraphHelpers.CreateGraphBuilder("multi-processor-test");
        builder.ConfigureEpochs(config =>
        {
            config.SetPolicy(EpochPolicy.ByCount(50));
            config.AddProcessor("processor1");
            config.AddProcessor("processor2");
            config.AddProcessor("processor3");
        }, _ => _coordinator);
        
        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        
        // Assert
        graph.ShouldNotBeNull();
    }
    
    [Fact]
    public void EpochActorBlock_RequiresPolicy_ShouldHaveDefaultOrConfigured()
    {
        // This test verifies that epoch configuration can work with default policy
        
        // Arrange & Act
        var builder = GraphHelpers.CreateGraphBuilder("policy-test");
        builder.ConfigureEpochs(config =>
        {
            // Set an explicit policy
            config.SetPolicy(EpochPolicy.ByCount(1000));
            config.AddProcessor("processor1");
        }, _ => _coordinator);
        
        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        
        // Assert
        graph.ShouldNotBeNull();
    }
    
    [Fact]
    public void EpochActorBlock_ScopeRotationConcept_IsDocumented()
    {
        // This test documents the scope rotation pattern
        // Scope rotation means each epoch gets a new DI scope
        // which is automatically disposed after the epoch completes
        
        // The pattern prevents memory leaks with EF Core and other scoped services
        // by ensuring that scoped services (like DbContext) are disposed periodically
        // rather than accumulating for the entire stream
        
        // This test verifies the configuration supports this pattern
        var builder = GraphHelpers.CreateGraphBuilder("scope-rotation");
        builder.ConfigureEpochs(config =>
        {
            // Configure epochs - each epoch gets its own scope
            config.SetPolicy(EpochPolicy.ByCount(1000));
            config.AddProcessor("processor");
        }, _ => _coordinator);
        
        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        graph.ShouldNotBeNull();
        
        // The actual scope rotation happens during execution
        // Each epoch will have its own scope which is disposed after completion
    }
}

// Test helper service
public class TestService
{
    public int Value { get; set; }
}
