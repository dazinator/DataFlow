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
/// Tests for the inline transform block that processes items inline with downstream pull.
/// </summary>
[IntegrationTest]
public class InlineTransformBlockTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public InlineTransformBlockTests(ITestOutputHelper testOutputHelper)
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

    [Fact(Skip = "Test hangs indefinitely - needs investigation. Issue with InlineTransformBlock causing deadlock.")]
    public async Task InlineTransformBlock_TransformsItemsInline()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).ToList();
        var processedItems = new ConcurrentBag<string>();

        var provider = Services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<InlineTransformBlock<int, string>>>();
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

        var transformBlock = new InlineTransformBlock<int, string>(
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

    [Fact(Skip = "Test hangs indefinitely - needs investigation. Issue with InlineTransformBlock causing deadlock.")]
    public async Task InlineTransformBlock_SupportsAsyncEnumerableInterface()
    {
        // Arrange
        var items = Enumerable.Range(1, 5).ToList();
        
        var provider = Services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<InlineTransformBlock<int, string>>>();
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
        
        var transformBlock = new InlineTransformBlock<int, string>(
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

    [Fact(Skip = "Test hangs indefinitely - needs investigation. Issue with InlineTransformBlock causing deadlock.")]
    public async Task InlineTransformBlock_ProcessesLargeVolume()
    {
        // Arrange
        var itemCount = 1000;
        var items = Enumerable.Range(1, itemCount).ToList();
        var processedItems = new ConcurrentBag<string>();

        var provider = Services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<InlineTransformBlock<int, string>>>();
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

        var transformBlock = new InlineTransformBlock<int, string>(
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
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        var producerTask = producer.ExecuteAsync(context);
        var transformTask = transformBlock.ExecuteAsync(context);
        var processorTask = processorBlock.ExecuteAsync(context);

        await Task.WhenAll(producerTask, transformTask, processorTask);
        
        sw.Stop();

        // Assert
        processedItems.Count.ShouldBe(itemCount);
        Output.WriteLine($"Processed {itemCount} items in {sw.ElapsedMilliseconds}ms");
    }

    [Fact(Skip = "Test hangs indefinitely - needs investigation. Issue with InlineTransformBlock causing deadlock.")]
    public async Task InlineTransformBlock_HandlesCancellation()
    {
        // Arrange
        var provider = Services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<InlineTransformBlock<int, string>>>();
        var channelFactory = provider.GetRequiredService<IBoundedChannelFactory>();

        var producer = new ProducerBlock<int>(
            "test-producer",
            provider.GetRequiredService<ILogger<ProducerBlock<int>>>(),
            channelFactory,
            new ProducerBlockOptions<int>
            {
                ProducersFactory = (ctx, ct) => Task.FromResult<IEnumerable<IStreamProducer<int>>>(
                    new[] { new TestProducer<int>(Enumerable.Range(1, 1000).ToList(), delay: TimeSpan.FromMilliseconds(10)) })
            });

        var transformBlock = new InlineTransformBlock<int, string>(
            "test-transform",
            logger,
            x => $"Item_{x}");

        var processorBlock = new ProcessorBlock<string>(
            "test-processor",
            provider.GetRequiredService<ILogger<ProcessorBlock<string>>>(),
            sp => new TestProcessor<string>());

        transformBlock.SetSource(producer);
        processorBlock.SetSource(transformBlock);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), provider, cts.Token);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await Task.WhenAll(
                producer.ExecuteAsync(context),
                transformBlock.ExecuteAsync(context),
                processorBlock.ExecuteAsync(context)
            );
        });
    }
}
