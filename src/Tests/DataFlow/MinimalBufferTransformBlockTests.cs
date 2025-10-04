namespace Tests.DataFlow;

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Uniun.DataFlow.Blocks.Producer;
using Uniun.DataFlow.Blocks.Processor;
using Uniun.DataFlow.Blocks.Transform;
using Xunit.Abstractions;

/// <summary>
/// Tests demonstrating the minimal-buffer transform block pattern.
/// This shows how blocks can be connected with minimal buffering (capacity of 1)
/// to reduce memory overhead while maintaining backpressure.
/// </summary>
[IntegrationTest]
public class MinimalBufferTransformBlockTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public MinimalBufferTransformBlockTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        Services = new ServiceCollection();
        AddDefaultServices();
    }

    private void AddDefaultServices()
    {
        Services.AddLogging(builder => builder.AddXUnit(_testOutputHelper));
        Services.AddDataFlows();
        Services.AddDataFlowMetrics();
    }

    public IServiceCollection Services { get; }
    public ITestOutputHelper Output => _testOutputHelper;

    [Fact]
    public async Task MinimalBufferTransformBlock_TransformsItemsWithMinimalBuffering()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).ToList();
        var processedItems = new ConcurrentBag<string>();

        var provider = Services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<MinimalBufferTransformBlock<int, string>>>();
        var channelFactory = provider.GetRequiredService<IBoundedChannelFactory>();
        
        var producer = new ProducerBlock<int>(
            "test-producer",
            provider.GetRequiredService<ILogger<ProducerBlock<int>>>(),
            channelFactory,
            new ProducerBlockOptions<int>
            {
                ProducersFactory = (ctx, ct) => Task.FromResult<IEnumerable<IStreamProducer<int>>>(
                    new[] { new TestProducer<int>(items) })
            });

        var transformBlock = new MinimalBufferTransformBlock<int, string>(
            "test-transform",
            logger,
            x => $"Item_{x}");

        var processorBlock = new ProcessorBlock<string>(
            "test-processor",
            provider.GetRequiredService<ILogger<ProcessorBlock<string>>>(),
            sp => new TestProcessor<string>(onProcessItem: item => processedItems.Add(item)));

        transformBlock.SetSource(producer);
        processorBlock.SetSource(transformBlock);

        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), provider);

        // Act
        var producerTask = producer.ExecuteAsync(context);
        var transformTask = transformBlock.ExecuteAsync(context);
        var processorTask = processorBlock.ExecuteAsync(context);

        await Task.WhenAll(producerTask, transformTask, processorTask);

        // Assert
        processedItems.Count.ShouldBe(10);
        processedItems.ShouldContain("Item_1");
        processedItems.ShouldContain("Item_10");
    }

    [Fact]
    public async Task MinimalBufferTransformBlock_MaintainsBackpressure()
    {
        // Arrange
        var items = Enumerable.Range(1, 100).ToList();
        var processedItems = new ConcurrentBag<string>();
        var startTime = DateTime.UtcNow;

        var provider = Services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<MinimalBufferTransformBlock<int, string>>>();
        var channelFactory = provider.GetRequiredService<IBoundedChannelFactory>();

        var producer = new ProducerBlock<int>(
            "test-producer",
            provider.GetRequiredService<ILogger<ProducerBlock<int>>>(),
            channelFactory,
            new ProducerBlockOptions<int>
            {
                ProducersFactory = (ctx, ct) => Task.FromResult<IEnumerable<IStreamProducer<int>>>(
                    new[] { new TestProducer<int>(items) })
            });

        var transformBlock = new MinimalBufferTransformBlock<int, string>(
            "test-transform",
            logger,
            x => $"Item_{x}");

        // Slow processor to test backpressure
        var processorBlock = new ProcessorBlock<string>(
            "test-processor",
            provider.GetRequiredService<ILogger<ProcessorBlock<string>>>(),
            sp => new TestProcessor<string>(
                onProcessItem: item =>
                {
                    Thread.Sleep(10);
                    processedItems.Add(item);
                }));

        transformBlock.SetSource(producer);
        processorBlock.SetSource(transformBlock);

        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), provider);

        // Act
        var producerTask = producer.ExecuteAsync(context);
        var transformTask = transformBlock.ExecuteAsync(context);
        var processorTask = processorBlock.ExecuteAsync(context);

        await Task.WhenAll(producerTask, transformTask, processorTask);

        // Assert
        processedItems.Count.ShouldBe(100);
        
        // With backpressure and minimal buffering, the total time should reflect processing
        var totalTime = DateTime.UtcNow - startTime;
        Output.WriteLine($"Total processing time: {totalTime.TotalMilliseconds}ms");
        
        // We should have processed items (100 items * 10ms = at least 1000ms)
        totalTime.TotalMilliseconds.ShouldBeGreaterThan(900);
    }

    [Fact]
    public async Task MinimalBufferTransformBlock_SupportsAsyncEnumerableInterface()
    {
        // Arrange
        var items = Enumerable.Range(1, 5).ToList();
        
        var provider = Services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<MinimalBufferTransformBlock<int, string>>>();
        var channelFactory = provider.GetRequiredService<IBoundedChannelFactory>();

        var producer = new ProducerBlock<int>(
            "test-producer",
            provider.GetRequiredService<ILogger<ProducerBlock<int>>>(),
            channelFactory,
            new ProducerBlockOptions<int>
            {
                ProducersFactory = (ctx, ct) => Task.FromResult<IEnumerable<IStreamProducer<int>>>(
                    new[] { new TestProducer<int>(items) })
            });
        
        var transformBlock = new MinimalBufferTransformBlock<int, string>(
            "test-transform",
            logger,
            x => $"Item_{x}");

        transformBlock.SetSource(producer);

        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), provider);

        // Start the producer
        var producerTask = producer.ExecuteAsync(context);

        // Start the transform block
        var transformTask = transformBlock.ExecuteAsync(context);

        // Act - consume via IAsyncEnumerable interface
        var results = new List<string>();
        await foreach (var item in transformBlock.GetAsyncEnumerable(null!, context.CancellationToken))
        {
            results.Add(item);
        }

        // Wait for tasks to complete
        await Task.WhenAll(producerTask, transformTask);

        // Assert
        results.Count.ShouldBe(5);
        results[0].ShouldBe("Item_1");
        results[4].ShouldBe("Item_5");
    }
}
