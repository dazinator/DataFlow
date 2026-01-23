namespace DataFlow.POC.Tests.Documentation;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Xunit.Categories;
using DataFlow.POC.Registry;

/// <summary>
/// Documentation tests for the Source Blocks guide.
/// These tests verify that all code examples in source-blocks.md are correct and functional.
/// </summary>
[Documentation]
public class SourceBlocksDocumentationTests
{
    [Fact]
    public async Task SourceBlocks_InMemorySource_ShouldYieldAllItems()
    {
        // This test verifies the in-memory data source pattern
        
        // Arrange
        var processedItems = new List<int>();
        
        var numbers = BlockHelpers.CreateProducer("numbers", Enumerable.Range(1, 10));
        
        var processor = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "collector",
            new CollectorActor<int>(processedItems));
        
        var graph = GraphHelpers.CreateGraphBuilder("inmemory-source")
            .AddBlock(numbers)
            .AddBlock(processor)
            .Connect(numbers, processor)
            .Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        
        // Act
        await graph.ExecuteAsync(context);
        
        // Assert
        processedItems.Count.ShouldBe(10);
        processedItems.ShouldBe(Enumerable.Range(1, 10));
    }
    
    [Fact]
    public async Task SourceBlocks_AsyncSource_ShouldYieldAsynchronously()
    {
        // This test verifies the async data source pattern
        
        // Arrange
        var processedItems = new List<string>();
        
        var asyncSource = BlockHelpers.CreateProducer("async-source", GetDataAsync());
        
        var processor = BlockHelpers.CreateActor<string, object, CollectorActor<string>>(
            "collector",
            new CollectorActor<string>(processedItems));
        
        var graph = GraphHelpers.CreateGraphBuilder("async-source")
            .AddBlock(asyncSource)
            .AddBlock(processor)
            .Connect(asyncSource, processor)
            .Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        
        // Act
        await graph.ExecuteAsync(context);
        
        // Assert
        processedItems.Count.ShouldBe(5);
        processedItems.ShouldBe(new[] { "Item-0", "Item-1", "Item-2", "Item-3", "Item-4" });
    }
    
    [Fact]
    public async Task SourceBlocks_ListBasedSource_ShouldYieldFromList()
    {
        // This test verifies the list-based source pattern
        
        // Arrange
        var items = new[] { "apple", "banana", "cherry" };
        var processedItems = new List<string>();
        
        var fruits = BlockHelpers.CreateProducer("fruits", items);
        
        var processor = BlockHelpers.CreateActor<string, object, CollectorActor<string>>(
            "collector",
            new CollectorActor<string>(processedItems));
        
        var graph = GraphHelpers.CreateGraphBuilder("list-source")
            .AddBlock(fruits)
            .AddBlock(processor)
            .Connect(fruits, processor)
            .Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        
        // Act
        await graph.ExecuteAsync(context);
        
        // Assert
        processedItems.Count.ShouldBe(3);
        processedItems.ShouldBe(new[] { "apple", "banana", "cherry" });
    }
    
    // Helper method for async source test
    private static async IAsyncEnumerable<string> GetDataAsync()
    {
        for (int i = 0; i < 5; i++)
        {
            await Task.Delay(10);
            yield return $"Item-{i}";
        }
    }
}
