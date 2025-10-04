namespace Tests.DataFlow;

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Uniun.DataFlow.Blocks.Transform;
using Xunit.Abstractions;

/// <summary>
/// Simple exploratory tests for the minimal buffer transform block
/// </summary>
[Exploratory]
public class MinimalBufferExploratoryTests
{
    private readonly ITestOutputHelper _output;

    public MinimalBufferExploratoryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task DirectTest_MinimalBufferBlock_BasicFunctionality()
    {
        // This test directly exercises the block without the full data flow infrastructure
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_output));
        services.AddDataFlows();
        
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<MinimalBufferTransformBlock<int, string>>>();
        
        // Create a simple source channel
        var sourceChannel = Channel.CreateUnbounded<int>();
        
        // Create a mock source block
        var mockSource = new MockSourceBlock<int>(sourceChannel.Reader);
        
        // Create the transform block
        var transformBlock = new MinimalBufferTransformBlock<int, string>(
            "test",
            logger,
            x => $"Item_{x}");
        
        transformBlock.SetSource(mockSource);
        
        // Write some test data
        await sourceChannel.Writer.WriteAsync(1);
        await sourceChannel.Writer.WriteAsync(2);
        await sourceChannel.Writer.WriteAsync(3);
        sourceChannel.Writer.Complete();
        
        // Create a cancellation token
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), provider, cts.Token);
        
        // Start the transform block execution
        var executeTask = transformBlock.ExecuteAsync(context);
        
        // Read the transformed items
        var results = new List<string>();
        await foreach (var item in transformBlock.GetAsyncEnumerable(null!, cts.Token))
        {
            _output.WriteLine($"Read: {item}");
            results.Add(item);
        }
        
        await executeTask;
        
        // Verify
        results.Count.ShouldBe(3);
        results[0].ShouldBe("Item_1");
        results[1].ShouldBe("Item_2");
        results[2].ShouldBe("Item_3");
    }
    
    private class MockSourceBlock<T> : ISourceBlock<T>
    {
        private readonly ChannelReader<T> _reader;

        public MockSourceBlock(ChannelReader<T> reader)
        {
            _reader = reader;
        }

        public string Name => "mock";
        public BlockOptions Options => new BlockOptions();
        public BlockMetricsTagsContext? MetricsContext { get; set; }

        public ChannelReader<T> GetReader(ITargetBlock<T> target) => _reader;

        public async IAsyncEnumerable<T> GetAsyncEnumerable(
            ITargetBlock<T> target,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in _reader.ReadAllAsync(cancellationToken))
            {
                yield return item;
            }
        }

        public Task ExecuteAsync(IDataFlowContext context) => Task.CompletedTask;
    }
}
