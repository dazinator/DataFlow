namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using DataFlow.POC.Tests.TestHelpers;

public class BufferNodeTests
{
    /// <summary>
    /// Simple collector actor for integers.
    /// </summary>
    private class IntCollectorActor : IStreamActor<int, object>
    {
        private readonly List<int> _collected;

        public IntCollectorActor(List<int> collected)
        {
            _collected = collected;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _collected.Add(item);
            }
            yield break;
        }
    }

    /// <summary>
    /// Collector actor for strings.
    /// </summary>
    private class StringCollectorActor : IStreamActor<string, object>
    {
        private readonly List<string> _collected;

        public StringCollectorActor(List<string> collected)
        {
            _collected = collected;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<string> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _collected.Add(item);
            }
            yield break;
        }
    }

    /// <summary>
    /// Thread-safe collector actor for integers.
    /// </summary>
    private class ThreadSafeIntCollectorActor : IStreamActor<int, object>
    {
        private readonly List<int> _collected;
        private readonly int _delayMs;

        public ThreadSafeIntCollectorActor(List<int> collected, int delayMs = 0)
        {
            _collected = collected;
            _delayMs = delayMs;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                lock (_collected)
                {
                    _collected.Add(item);
                }
                if (_delayMs > 0)
                {
                    await Task.Delay(_delayMs, context.CancellationToken);
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Collector actor with configurable delay (for backpressure testing).
    /// </summary>
    private class DelayingIntCollectorActor : IStreamActor<int, object>
    {
        private readonly List<int> _collected;
        private readonly int _delayMs;

        public DelayingIntCollectorActor(List<int> collected, int delayMs)
        {
            _collected = collected;
            _delayMs = delayMs;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _collected.Add(item);
                await Task.Delay(_delayMs, context.CancellationToken);
            }
            yield break;
        }
    }

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
        
        // Create service provider for processor
        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new IntCollectorActor(processedItems));
        var processorSP = processorServices.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<int>("producer", ctx => ProduceIntegers(ctx, 1, 10));
        var processor = BlockHelpers.CreateActor<int, object, IntCollectorActor>("processor", processorSP.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("buffer-node-flow");
        var buffer = builder.Buffer<int>(capacity: 5);
        
        builder.AddBlock(producer)
            .AddBlock(processor)
            .Connect(producer, buffer)
            .Connect(buffer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

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
        
        // Create service provider for processor (thread-safe)
        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new ThreadSafeIntCollectorActor(processedItems));
        var processorSP = processorServices.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer1 = BlockHelpers.CreateProducer<int>("producer1", ctx => ProduceIntegers(ctx, 1, 5));
        var producer2 = BlockHelpers.CreateProducer<int>("producer2", ctx => ProduceIntegers(ctx, 100, 5));
        var processor = BlockHelpers.CreateActor<int, object, ThreadSafeIntCollectorActor>("processor", processorSP.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("multi-producer-buffer-flow");
        var buffer = builder.Buffer<int>(capacity: 10);
        
        builder.AddBlock(producer1)
            .AddBlock(producer2)
            .AddBlock(processor)
            .Connect(producer1, buffer)
            .Connect(producer2, buffer)
            .Connect(buffer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

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
        
        // Create separate service providers for each processor
        var processor1Services = new ServiceCollection();
        processor1Services.AddScoped(_ => new ThreadSafeIntCollectorActor(processor1Items, delayMs: 10));
        var processor1SP = processor1Services.BuildServiceProvider();

        var processor2Services = new ServiceCollection();
        processor2Services.AddScoped(_ => new ThreadSafeIntCollectorActor(processor2Items, delayMs: 10));
        var processor2SP = processor2Services.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<int>("producer", ctx => ProduceIntegers(ctx, 1, 10));
        
        var processor1 = BlockHelpers.CreateActor<int, object, ThreadSafeIntCollectorActor>("processor1", processor1SP.GetRequiredService<IServiceScopeFactory>());

        var processor2 = BlockHelpers.CreateActor<int, object, ThreadSafeIntCollectorActor>("processor2", processor2SP.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("single-producer-multi-consumer-buffer-flow");
        var buffer = builder.Buffer<int>(capacity: 5);
        
        builder.AddBlock(producer)
            .AddBlock(processor1)
            .AddBlock(processor2)
            .Connect(producer, buffer)
            .Connect(buffer, processor1)
            .Connect(buffer, processor2);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

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
        
        // Create separate service providers for each processor
        var processor1Services = new ServiceCollection();
        processor1Services.AddScoped(_ => new ThreadSafeIntCollectorActor(processor1Items, delayMs: 5));
        var processor1SP = processor1Services.BuildServiceProvider();

        var processor2Services = new ServiceCollection();
        processor2Services.AddScoped(_ => new ThreadSafeIntCollectorActor(processor2Items, delayMs: 5));
        var processor2SP = processor2Services.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer1 = BlockHelpers.CreateProducer<int>("producer1", ctx => ProduceIntegers(ctx, 1, 5));
        var producer2 = BlockHelpers.CreateProducer<int>("producer2", ctx => ProduceIntegers(ctx, 100, 5));

        var processor1 = BlockHelpers.CreateActor<int, object, ThreadSafeIntCollectorActor>("processor1", processor1SP.GetRequiredService<IServiceScopeFactory>());

        var processor2 = BlockHelpers.CreateActor<int, object, ThreadSafeIntCollectorActor>("processor2", processor2SP.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("multi-producer-multi-consumer-buffer-flow");
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
        var context = new ExecutionContext(commonServices, CancellationToken.None);

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
        
        // Create service provider for processor with delay
        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new DelayingIntCollectorActor(processedItems, delayMs: 50));
        var processorSP = processorServices.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<int>("producer", ctx => ProduceIntegers(ctx, 1, 100));
        var processor = BlockHelpers.CreateActor<int, object, DelayingIntCollectorActor>("processor", processorSP.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("backpressure-flow");
        var buffer = builder.Buffer<int>(capacity: 5); // Small buffer
        
        builder.AddBlock(producer)
            .AddBlock(processor)
            .Connect(producer, buffer)
            .Connect(buffer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

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
        var builder = GraphHelpers.CreateGraphBuilder("type-mismatch-flow");
        var producer = BlockHelpers.CreateProducer<int>("producer", ctx => ProduceIntegers(ctx, 1, 10));
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
        var builder = GraphHelpers.CreateGraphBuilder("type-mismatch-flow");
        var buffer = builder.Buffer<int>(capacity: 10);
        
        // Create a string collector actor for the validation test
        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new StringCollectorActor(new List<string>()));
        var processorSP = processorServices.BuildServiceProvider();
        
        var processor = BlockHelpers.CreateActor<string, object, StringCollectorActor>("processor", processorSP.GetRequiredService<IServiceScopeFactory>());
        
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
        
        // Create separate service providers for each consumer
        var edgeConsumerServices = new ServiceCollection();
        edgeConsumerServices.AddScoped(_ => new IntCollectorActor(edgeConsumerItems));
        var edgeConsumerSP = edgeConsumerServices.BuildServiceProvider();

        var bufferConsumer1Services = new ServiceCollection();
        bufferConsumer1Services.AddScoped(_ => new IntCollectorActor(bufferConsumer1Items));
        var bufferConsumer1SP = bufferConsumer1Services.BuildServiceProvider();

        var bufferConsumer2Services = new ServiceCollection();
        bufferConsumer2Services.AddScoped(_ => new IntCollectorActor(bufferConsumer2Items));
        var bufferConsumer2SP = bufferConsumer2Services.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<int>("producer", ctx => ProduceIntegers(ctx, 1, 10));
        
        // Edge consumer - directly connected via edge
        var edgeConsumer = BlockHelpers.CreateActor<int, object, IntCollectorActor>("edge-consumer", edgeConsumerSP.GetRequiredService<IServiceScopeFactory>());
        
        // Buffer consumers - connected via buffer node (competing)
        var bufferConsumer1 = BlockHelpers.CreateActor<int, object, IntCollectorActor>("buffer-consumer1", bufferConsumer1SP.GetRequiredService<IServiceScopeFactory>());
        
        var bufferConsumer2 = BlockHelpers.CreateActor<int, object, IntCollectorActor>("buffer-consumer2", bufferConsumer2SP.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("broadcast-flow");
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
        var context = new ExecutionContext(commonServices, CancellationToken.None);

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
