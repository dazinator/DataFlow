namespace DataFlow.POC.Tests.Documentation;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Xunit.Categories;

/// <summary>
/// Documentation tests for the Working with Blocks guide.
/// These tests verify that all code examples in working-with-blocks.md are correct and functional.
/// </summary>
[Documentation]
public class WorkingWithBlocksDocumentationTests
{
    [Fact]
    public async Task WorkingWithBlocks_ProducerBlock_ShouldGenerateData()
    {
        // This test verifies the producer block pattern from the guide
        
        // Arrange
        var processedItems = new List<int>();
        
        var producer = BlockHelpers.CreateProducer("numbers", new[] { 1, 2, 3, 4, 5 });
        
        var processor = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "collector",
            new CollectorActor<int>(processedItems));
        
        var graph = GraphHelpers.CreateGraphBuilder("producer-test")
            .AddBlock(producer)
            .AddBlock(processor)
            .Connect(producer, processor)
            .Build();
        
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        
        // Act
        await graph.ExecuteAsync(context);
        
        // Assert
        processedItems.Count.ShouldBe(5);
        processedItems.ShouldBe(new[] { 1, 2, 3, 4, 5 });
    }
    
    [Fact]
    public async Task WorkingWithBlocks_TransformActor_ShouldTransformData()
    {
        // This test verifies the transform actor pattern
        
        // Arrange
        var processedItems = new List<string>();
        
        var scopeFactory = TestServiceBuilder.Create()
            .WithScoped(new TransformActor<int, string>(i => $"Item-{i}"))
            .WithScoped(new CollectorActor<string>(processedItems))
            .BuildScopeFactory();
        
        var producer = BlockHelpers.CreateProducer("source", new[] { 1, 2, 3 });
        
        var transformer = BlockHelpers.CreateActor<int, string, TransformActor<int, string>>(
            "transform",
            scopeFactory);
        
        var processor = BlockHelpers.CreateActor<string, object, CollectorActor<string>>(
            "collector",
            scopeFactory);
        
        var graph = GraphHelpers.CreateGraphBuilder("transform-test")
            .AddBlock(producer)
            .AddBlock(transformer)
            .AddBlock(processor)
            .Connect(producer, transformer)
            .Connect(transformer, processor)
            .Build();
        
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        
        // Act
        await graph.ExecuteAsync(context);
        
        // Assert
        processedItems.Count.ShouldBe(3);
        processedItems.ShouldBe(new[] { "Item-1", "Item-2", "Item-3" });
    }
    
    [Fact]
    public async Task WorkingWithBlocks_GenericTransformActor_ShouldWork()
    {
        // This test verifies the generic transform actor pattern from the guide
        
        // Arrange
        var processedItems = new List<int>();
        
        var scopeFactory = TestServiceBuilder.Create()
            .WithScoped(new TransformActor<int, int>(x => x * 2))
            .WithScoped(new CollectorActor<int>(processedItems))
            .BuildScopeFactory();
        
        var producer = BlockHelpers.CreateProducer("source", new[] { 1, 2, 3 });
        
        var multiplier = BlockHelpers.CreateActor<int, int, TransformActor<int, int>>(
            "multiplier",
            scopeFactory);
        
        var processor = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "collector",
            scopeFactory);
        
        var graph = GraphHelpers.CreateGraphBuilder("multiplier-test")
            .AddBlock(producer)
            .AddBlock(multiplier)
            .AddBlock(processor)
            .Connect(producer, multiplier)
            .Connect(multiplier, processor)
            .Build();
        
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        
        // Act
        await graph.ExecuteAsync(context);
        
        // Assert
        processedItems.Count.ShouldBe(3);
        processedItems.ShouldBe(new[] { 2, 4, 6 });
    }
    
    [Fact]
    public async Task WorkingWithBlocks_FilterPattern_ShouldFilterItems()
    {
        // This test verifies the filter pattern from the guide
        
        // Arrange
        var processedItems = new List<int>();
        
        var scopeFactory = TestServiceBuilder.Create()
            .WithScoped(new FilterActor<int>(x => x > 5))
            .WithScoped(new CollectorActor<int>(processedItems))
            .BuildScopeFactory();
        
        var producer = BlockHelpers.CreateProducer("source", Enumerable.Range(1, 10));
        
        var filter = BlockHelpers.CreateActor<int, int, FilterActor<int>>(
            "filter",
            scopeFactory);
        
        var processor = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "collector",
            scopeFactory);
        
        var graph = GraphHelpers.CreateGraphBuilder("filter-test")
            .AddBlock(producer)
            .AddBlock(filter)
            .AddBlock(processor)
            .Connect(producer, filter)
            .Connect(filter, processor)
            .Build();
        
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        
        // Act
        await graph.ExecuteAsync(context);
        
        // Assert
        processedItems.Count.ShouldBe(5);
        processedItems.ShouldBe(new[] { 6, 7, 8, 9, 10 });
    }
}

// Helper actor for filter pattern
public class FilterActor<T> : IStreamActor<T, T>
{
    private readonly Func<T, bool> _predicate;
    
    public FilterActor(Func<T, bool> predicate)
    {
        _predicate = predicate;
    }
    
    public async IAsyncEnumerable<T> RunAsync(
        IAsyncEnumerable<T> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            if (_predicate(item))
                yield return item;
        }
    }
}
