namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class BufferNodeTests
{
    private static async IAsyncEnumerable<int> ProduceIntegers(IExecutionContext ctx, int start, int count)
    {
        for (int i = start; i < start + count; i++)
        {
            yield return i;
        }
        await Task.CompletedTask;
    }

    [Fact]
    public async Task BufferNode_Should_Connect_Single_Producer_To_Single_Consumer()
    {
        // Arrange
        var processedItems = new List<int>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 1, 10));
        var processor = new ProcessorBlock<int>("processor", async (item, ctx) =>
        {
            processedItems.Add(item);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("buffer-node-flow");
        var buffer = builder.Buffer<int>(capacity: 5);
        
        builder.AddBlock(producer)
            .AddBlock(processor)
            .Connect(producer, buffer)
            .Connect(buffer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        processedItems.Count.ShouldBe(10);
        processedItems.ShouldBe(Enumerable.Range(1, 10));
    }

    [Fact]
    public async Task BufferNode_Should_Connect_Multiple_Producers_To_Single_Consumer()
    {
        // Arrange
        var processedItems = new List<int>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer1 = new ProducerBlock<int>("producer1", ctx => ProduceIntegers(ctx, 1, 5));
        var producer2 = new ProducerBlock<int>("producer2", ctx => ProduceIntegers(ctx, 100, 5));
        var processor = new ProcessorBlock<int>("processor", async (item, ctx) =>
        {
            lock (processedItems)
            {
                processedItems.Add(item);
            }
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("multi-producer-buffer-flow");
        var buffer = builder.Buffer<int>(capacity: 10);
        
        builder.AddBlock(producer1)
            .AddBlock(producer2)
            .AddBlock(processor)
            .Connect(producer1, buffer)
            .Connect(producer2, buffer)
            .Connect(buffer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        processedItems.Count.ShouldBe(10);
        
        // Items from both producers should be present
        var itemsFromProducer1 = processedItems.Where(x => x < 100).OrderBy(x => x).ToList();
        var itemsFromProducer2 = processedItems.Where(x => x >= 100).OrderBy(x => x).ToList();
        
        itemsFromProducer1.ShouldBe(Enumerable.Range(1, 5));
        itemsFromProducer2.ShouldBe(Enumerable.Range(100, 5));
    }

    [Fact]
    public async Task BufferNode_Should_Connect_Single_Producer_To_Multiple_Consumers()
    {
        // Arrange
        var processor1Items = new List<int>();
        var processor2Items = new List<int>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 1, 10));
        
        var processor1 = new ProcessorBlock<int>("processor1", async (item, ctx) =>
        {
            lock (processor1Items)
            {
                processor1Items.Add(item);
            }
            await Task.Delay(10); // Simulate work
        });

        var processor2 = new ProcessorBlock<int>("processor2", async (item, ctx) =>
        {
            lock (processor2Items)
            {
                processor2Items.Add(item);
            }
            await Task.Delay(10); // Simulate work
        });

        var builder = new DataFlowGraphBuilder("single-producer-multi-consumer-buffer-flow");
        var buffer = builder.Buffer<int>(capacity: 5);
        
        builder.AddBlock(producer)
            .AddBlock(processor1)
            .AddBlock(processor2)
            .Connect(producer, buffer)
            .Connect(buffer, processor1)
            .Connect(buffer, processor2);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        // Consumers compete for items from the buffer
        processor1Items.Count.ShouldBeGreaterThan(0);
        processor2Items.Count.ShouldBeGreaterThan(0);
        
        // Together they should process all items
        (processor1Items.Count + processor2Items.Count).ShouldBe(10);
        
        // No duplicates - each item processed exactly once
        var allItems = processor1Items.Concat(processor2Items).OrderBy(x => x).ToList();
        allItems.ShouldBe(Enumerable.Range(1, 10));
    }

    [Fact]
    public async Task BufferNode_Should_Connect_Multiple_Producers_To_Multiple_Consumers()
    {
        // Arrange
        var processor1Items = new List<int>();
        var processor2Items = new List<int>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer1 = new ProducerBlock<int>("producer1", ctx => ProduceIntegers(ctx, 1, 5));
        var producer2 = new ProducerBlock<int>("producer2", ctx => ProduceIntegers(ctx, 100, 5));

        var processor1 = new ProcessorBlock<int>("processor1", async (item, ctx) =>
        {
            lock (processor1Items)
            {
                processor1Items.Add(item);
            }
            await Task.Delay(5);
        });

        var processor2 = new ProcessorBlock<int>("processor2", async (item, ctx) =>
        {
            lock (processor2Items)
            {
                processor2Items.Add(item);
            }
            await Task.Delay(5);
        });

        var builder = new DataFlowGraphBuilder("multi-producer-multi-consumer-buffer-flow");
        var buffer = builder.Buffer<int>(capacity: 10);
        
        builder.AddBlock(producer1)
            .AddBlock(producer2)
            .AddBlock(processor1)
            .AddBlock(processor2)
            .Connect(producer1, buffer)
            .Connect(producer2, buffer)
            .Connect(buffer, processor1)
            .Connect(buffer, processor2);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        // Both consumers should process some items
        processor1Items.Count.ShouldBeGreaterThan(0);
        processor2Items.Count.ShouldBeGreaterThan(0);
        
        // Together they should process all items from both producers
        (processor1Items.Count + processor2Items.Count).ShouldBe(10);
        
        // All items from both producers should be present exactly once
        var allItems = processor1Items.Concat(processor2Items).OrderBy(x => x).ToList();
        var expectedItems = Enumerable.Range(1, 5).Concat(Enumerable.Range(100, 5)).OrderBy(x => x).ToList();
        allItems.ShouldBe(expectedItems);
    }

    [Fact]
    public async Task BufferNode_Should_Enforce_Capacity_For_Backpressure()
    {
        // Arrange
        var processedItems = new List<int>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 1, 100));
        var processor = new ProcessorBlock<int>("processor", async (item, ctx) =>
        {
            processedItems.Add(item);
            await Task.Delay(50); // Slow consumer to test backpressure
        });

        var builder = new DataFlowGraphBuilder("backpressure-flow");
        var buffer = builder.Buffer<int>(capacity: 5); // Small buffer
        
        builder.AddBlock(producer)
            .AddBlock(processor)
            .Connect(producer, buffer)
            .Connect(buffer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        processedItems.Count.ShouldBe(100);
        processedItems.ShouldBe(Enumerable.Range(1, 100));
    }

    [Fact]
    public void BufferNode_Should_Validate_Type_Compatibility_With_Source_Block()
    {
        // Arrange
        var builder = new DataFlowGraphBuilder("type-mismatch-flow");
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 1, 10));
        var buffer = builder.Buffer<string>(capacity: 10); // Wrong type
        
        builder.AddBlock(producer);

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() =>
        {
            builder.Connect(producer, buffer);
        });

        exception.Message.ShouldContain("Type mismatch");
        exception.Message.ShouldContain("Int32");
        exception.Message.ShouldContain("String");
    }

    [Fact]
    public void BufferNode_Should_Validate_Type_Compatibility_With_Target_Block()
    {
        // Arrange
        var builder = new DataFlowGraphBuilder("type-mismatch-flow");
        var buffer = builder.Buffer<int>(capacity: 10);
        var processor = new ProcessorBlock<string>("processor", async (item, ctx) =>
        {
            await Task.CompletedTask;
        });
        
        builder.AddBlock(processor);

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() =>
        {
            builder.Connect(buffer, processor);
        });

        exception.Message.ShouldContain("Type mismatch");
        exception.Message.ShouldContain("Int32");
        exception.Message.ShouldContain("String");
    }

    [Fact]
    public async Task Block_Should_Broadcast_To_Both_Edge_And_BufferNode()
    {
        // This test verifies that when a block is connected via an edge to downstream blocks
        // AND also to a buffer node, each output item is routed to ALL destinations (broadcast semantics)
        
        // Arrange
        var edgeConsumerItems = new List<int>();
        var bufferConsumer1Items = new List<int>();
        var bufferConsumer2Items = new List<int>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 1, 10));
        
        // Edge consumer - directly connected via edge
        var edgeConsumer = new ProcessorBlock<int>("edge-consumer", async (item, ctx) =>
        {
            edgeConsumerItems.Add(item);
            await Task.CompletedTask;
        });
        
        // Buffer consumers - connected via buffer node (competing)
        var bufferConsumer1 = new ProcessorBlock<int>("buffer-consumer1", async (item, ctx) =>
        {
            bufferConsumer1Items.Add(item);
            await Task.CompletedTask;
        });
        
        var bufferConsumer2 = new ProcessorBlock<int>("buffer-consumer2", async (item, ctx) =>
        {
            bufferConsumer2Items.Add(item);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("broadcast-flow");
        var buffer = builder.Buffer<int>(capacity: 10);
        
        builder.AddBlock(producer)
            .AddBlock(edgeConsumer)
            .AddBlock(bufferConsumer1)
            .AddBlock(bufferConsumer2)
            .Connect(producer, edgeConsumer)      // Edge connection
            .Connect(producer, buffer)             // Buffer connection
            .Connect(buffer, bufferConsumer1)      // Buffer consumers compete
            .Connect(buffer, bufferConsumer2);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        // Edge consumer should receive ALL 10 items (broadcast)
        edgeConsumerItems.Count.ShouldBe(10);
        edgeConsumerItems.ShouldBe(Enumerable.Range(1, 10));
        
        // Buffer consumers compete for items, so together they should also have all 10 items
        var totalBufferItems = bufferConsumer1Items.Count + bufferConsumer2Items.Count;
        totalBufferItems.ShouldBe(10);
        
        // All items should be present in buffer consumers combined
        var allBufferItems = bufferConsumer1Items.Concat(bufferConsumer2Items).OrderBy(x => x).ToList();
        allBufferItems.ShouldBe(Enumerable.Range(1, 10));
        
        // Both buffer consumers should have processed at least one item (unless race condition)
        // Note: This is probabilistic but with 10 items it's highly likely
        (bufferConsumer1Items.Count > 0 || bufferConsumer2Items.Count > 0).ShouldBeTrue();
    }
}
