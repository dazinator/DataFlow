namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Shouldly;
using Xunit;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Tests for validating that multiple producers connecting to a single consumer
/// without a buffer node throws a helpful error message.
/// </summary>
public class MultiProducerValidationTests
{
    /// <summary>
    /// Simple collector actor for validation tests.
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

    [Fact]
    public void Connect_MultipleProducersToSingleConsumer_ThrowsInvalidOperationException()
    {
        // Arrange
        var processedItems = new List<int>();
        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new IntCollectorActor(processedItems));
        var processorSP = processorServices.BuildServiceProvider();

        var producer1 = BlockHelpers.CreateProducer<int>("producer1", ctx => ProduceIntegers(ctx, 1, 5));
        var producer2 = BlockHelpers.CreateProducer<int>("producer2", ctx => ProduceIntegers(ctx, 6, 5));
        var processor = BlockHelpers.CreateActor<int, object, IntCollectorActor>("processor", processorSP.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("multi-producer-test");
        builder.AddBlock(producer1)
            .AddBlock(producer2)
            .AddBlock(processor)
            .Connect(producer1, processor);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
        {
            builder.Connect(producer2, processor);
        });

        exception.Message.ShouldContain("Cannot connect block 'producer2' to 'processor'");
        exception.Message.ShouldContain("already has an incoming connection from 'producer1'");
        exception.Message.ShouldContain("Multiple producers to a single consumer require a buffer node");
        exception.Message.ShouldContain("var buffer = builder.Buffer<T>(capacity: N);");
    }

    [Fact]
    public void Connect_ByName_MultipleProducersToSingleConsumer_ThrowsInvalidOperationException()
    {
        // Arrange
        var processedItems = new List<int>();
        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new IntCollectorActor(processedItems));
        var processorSP = processorServices.BuildServiceProvider();

        var producer1 = BlockHelpers.CreateProducer<int>("producer1", ctx => ProduceIntegers(ctx, 1, 5));
        var producer2 = BlockHelpers.CreateProducer<int>("producer2", ctx => ProduceIntegers(ctx, 6, 5));
        var processor = BlockHelpers.CreateActor<int, object, IntCollectorActor>("processor", processorSP.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("multi-producer-test");
        builder.AddBlock(producer1)
            .AddBlock(producer2)
            .AddBlock(processor)
            .Connect("producer1", "processor");

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
        {
            builder.Connect("producer2", "processor");
        });

        exception.Message.ShouldContain("Cannot connect block 'producer2' to 'processor'");
        exception.Message.ShouldContain("already has an incoming connection from 'producer1'");
    }

    [Fact]
    public void AddEdge_MultipleProducersToSingleConsumer_ThrowsInvalidOperationException()
    {
        // Arrange
        var processedItems = new List<int>();
        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new IntCollectorActor(processedItems));
        var processorSP = processorServices.BuildServiceProvider();

        var producer1 = BlockHelpers.CreateProducer<int>("producer1", ctx => ProduceIntegers(ctx, 1, 5));
        var producer2 = BlockHelpers.CreateProducer<int>("producer2", ctx => ProduceIntegers(ctx, 6, 5));
        var processor = BlockHelpers.CreateActor<int, object, IntCollectorActor>("processor", processorSP.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("multi-producer-test");
        builder.AddBlock(producer1)
            .AddBlock(producer2)
            .AddBlock(processor);

        var edge1 = new Edge(producer1, processor);
        builder.AddEdge(edge1);

        var edge2 = new Edge(producer2, processor);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
        {
            builder.AddEdge(edge2);
        });

        exception.Message.ShouldContain("Cannot connect block 'producer2' to 'processor'");
        exception.Message.ShouldContain("already has an incoming connection from 'producer1'");
    }

    [Fact]
    public void Connect_WithBufferNode_MultipleProducersAllowed()
    {
        // Arrange
        var processedItems = new List<int>();
        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new IntCollectorActor(processedItems));
        var processorSP = processorServices.BuildServiceProvider();

        var producer1 = BlockHelpers.CreateProducer<int>("producer1", ctx => ProduceIntegers(ctx, 1, 5));
        var producer2 = BlockHelpers.CreateProducer<int>("producer2", ctx => ProduceIntegers(ctx, 6, 5));
        var processor = BlockHelpers.CreateActor<int, object, IntCollectorActor>("processor", processorSP.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("multi-producer-with-buffer-test");
        var buffer = builder.Buffer<int>(capacity: 10);
        
        // Act - This should NOT throw because we're using a buffer node
        builder.AddBlock(producer1)
            .AddBlock(producer2)
            .AddBlock(processor)
            .Connect(producer1, buffer)
            .Connect(producer2, buffer)
            .Connect(buffer, processor);

        // Assert - Building the graph should succeed
        var graph = Should.NotThrow(() => builder.Build());
        graph.ShouldNotBeNull();
    }

    [Fact]
    public void Connect_SingleProducerToMultipleConsumers_Allowed()
    {
        // Arrange - this should be allowed (broadcast pattern)
        var processor1Items = new List<int>();
        var processor2Items = new List<int>();
        
        var processor1Services = new ServiceCollection();
        processor1Services.AddScoped(_ => new IntCollectorActor(processor1Items));
        var processor1SP = processor1Services.BuildServiceProvider();

        var processor2Services = new ServiceCollection();
        processor2Services.AddScoped(_ => new IntCollectorActor(processor2Items));
        var processor2SP = processor2Services.BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<int>("producer", ctx => ProduceIntegers(ctx, 1, 10));
        var processor1 = BlockHelpers.CreateActor<int, object, IntCollectorActor>("processor1", processor1SP.GetRequiredService<IServiceScopeFactory>());
        var processor2 = BlockHelpers.CreateActor<int, object, IntCollectorActor>("processor2", processor2SP.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("broadcast-test");
        
        // Act - This should NOT throw - one producer to many consumers is allowed
        builder.AddBlock(producer)
            .AddBlock(processor1)
            .AddBlock(processor2)
            .Connect(producer, processor1)
            .Connect(producer, processor2);

        // Assert
        var graph = Should.NotThrow(() => builder.Build());
        graph.ShouldNotBeNull();
    }

    private static async IAsyncEnumerable<int> ProduceIntegers(IExecutionContext context, int start, int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return start + i;
        }
    }
}
