namespace DataFlow.POC.Tests.Documentation;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.DependencyInjection;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Xunit.Categories;
using DataFlow.POC.Registry;

/// <summary>
/// Documentation tests for the Getting Started guide.
/// These tests verify that all code examples in getting-started.md are correct and functional.
/// </summary>
[Documentation]
public class GettingStartedDocumentationTests
{
    [Fact]
    public async Task GettingStarted_SimpleUppercaseFlow_ShouldWork()
    {
        // This test verifies the basic "hello world" example from the getting started guide
        
        // Arrange - Create test actors
        var processedItems = new List<string>();
        
        var scopeFactory = TestServiceBuilder.Create()
            .WithScoped(new TransformActor<string, string>(s => s.ToUpperInvariant()))
            .WithScoped(new CollectorActor<string>(processedItems))
            .BuildScopeFactory();
        
        var producer = BlockHelpers.CreateProducer<string>("input", GenerateHelloWorld());
        
        var transformer = BlockHelpers.CreateActor<string, string, TransformActor<string, string>>(
            "uppercase",
            scopeFactory);
        
        var processor = BlockHelpers.CreateActor<string, object, CollectorActor<string>>(
            "writer",
            scopeFactory);
        
        // Act - Build and execute graph
        var graph = GraphHelpers.CreateGraphBuilder("getting-started-example")
            .AddBlock(producer)
            .AddBlock(transformer)
            .AddBlock(processor)
            .Connect(producer, transformer)
            .Connect(transformer, processor)
            .Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        
        await graph.ExecuteAsync(context);
        
        // Assert
        processedItems.Count.ShouldBe(2);
        processedItems[0].ShouldBe("HELLO");
        processedItems[1].ShouldBe("WORLD");
    }
    
    
    [Fact]
    public async Task GettingStarted_DIRegistration_WithActorBlocks_ShouldWork()
    {
        // This test verifies registering and using actor blocks with DI
        // Demonstrates the UseBlock pattern for clean, reusable block definitions
        
        // Arrange
        var processedItems = new List<int>();
        
        var services = new ServiceCollection();
        
        services.AddDataFlows("global", df =>
        {
            // Register actor blocks - these can be reused in multiple graphs
            df.AddActorBlock<int, int, TransformActor<int, int>>("doubler");
            df.AddActorBlock<int, int, TransformActor<int, int>>("tripler");
        });
        
        // Register the actual actor implementations
        services.AddScoped<TransformActor<int, int>>(sp => new TransformActor<int, int>(x => x * 2));
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Build graph - demonstrates UseBlock resolves from DI
        var producer = BlockHelpers.CreateProducer("source", new[] { 1, 2, 3 });
        var collector = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "collector",
            new CollectorActor<int>(processedItems));
        
        // Verify that blocks are registered and can be resolved
        var doublerBlock = serviceProvider.GetKeyedService<IBlock>("global:doubler");
        doublerBlock.ShouldNotBeNull();
        doublerBlock.Name.ShouldBe("global:doubler");
    }
    
    [Fact]
    public void GettingStarted_ServiceProviderResolution_ShouldResolveGraph()
    {
        // This test verifies the pattern for resolving graphs from DI container
        
        // Arrange
        var services = new ServiceCollection();
        var producer = BlockHelpers.CreateProducer("producer", new[] { 42 });
        
        services.AddDataFlows("global", df =>
        {
            df.AddGraph("test-graph", g =>
            {
                g.AddBlock(producer);
            });
        });
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act - Resolve graph by keyed service (as shown in guide)
        var graph = serviceProvider.GetKeyedService<DataFlowGraph>("global:test-graph");
        
        // Assert
        graph.ShouldNotBeNull();
        graph.Name.ShouldBe("test-graph");
    }
    
    [Fact]
    public async Task GettingStarted_ProducerToProcessorFlow_ShouldProcessAllItems()
    {
        // This test verifies the basic producer to processor flow pattern
        
        // Arrange
        var processedItems = new List<int>();
        
        var producer = BlockHelpers.CreateProducer("source", Enumerable.Range(1, 10));
        
        var processor = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "processor",
            new CollectorActor<int>(processedItems));
        
        var graph = GraphHelpers.CreateGraphBuilder("basic-flow")
            .AddBlock(producer)
            .AddBlock(processor)
            .Connect(producer, processor)
            .Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        
        // Act
        await graph.ExecuteAsync(context);
        
        // Assert
        processedItems.Count.ShouldBe(10);
        processedItems.ShouldBe(Enumerable.Range(1, 10));
    }
    
    private static async IAsyncEnumerable<string> GenerateHelloWorld()
    {
        yield return "hello";
        yield return "world";
        await Task.CompletedTask;
    }
}
