namespace Benchmarks;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks.Processor;
using Uniun.DataFlow.Blocks.Producer;
using Uniun.DataFlow.Blocks.Transform;

/// <summary>
/// Detailed memory analysis comparing execution models.
/// Uses a larger item count and tracks memory metrics throughout execution.
/// </summary>
[SimpleJob(RunStrategy.Monitoring, warmupCount: 1, iterationCount: 1)]
[MemoryDiagnoser]
public class TransformBlockMemoryBenchmarks
{
    private ServiceProvider _sp;
    private ILogger<TransformBlockMemoryBenchmarks> _logger;

    [Params(100000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddDataFlows();
        services.AddDataFlowMetrics();

        _sp = services.BuildServiceProvider();
        _logger = _sp.GetRequiredService<ILogger<TransformBlockMemoryBenchmarks>>();

        // Force GC before benchmarks
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true, true);
    }

    [Benchmark(Baseline = true)]
    public async Task TransformBlock_DefaultBuffer()
    {
        var processedItems = new ConcurrentBag<string>();
        var channelFactory = _sp.GetRequiredService<IBoundedChannelFactory>();

        var producer = new ProducerBlock<int>(
            "producer",
            _sp.GetRequiredService<ILogger<ProducerBlock<int>>>(),
            channelFactory,
            new ProducerBlockOptions<int>
            {
                ProducersFactory = (ctx, ct) => Task.FromResult<IEnumerable<IStreamProducer<int>>>(
                    new[] { new RangeProducer(1, ItemCount) })
            });

        var transform = new TransformBlock<int, string>(
            "transform",
            _sp.GetRequiredService<ILogger<TransformBlock<int, string>>>(),
            channelFactory,
            new TransformBlockOptions<int, string>
            {
                TransformerFactory = sp => new SimpleTransformer()
            });

        var processor = new ProcessorBlock<string>(
            "processor",
            _sp.GetRequiredService<ILogger<ProcessorBlock<string>>>(),
            sp => new CountingProcessor());

        transform.SetSource(producer);
        processor.SetSource(transform);

        var context = new DataFlowContext(Guid.NewGuid())
        {
            ServiceProvider = _sp,
            Name = "default-buffer"
        };

        var startMemory = GC.GetTotalMemory(false);
        var sw = Stopwatch.StartNew();

        await Task.WhenAll(
            producer.ExecuteAsync(context),
            transform.ExecuteAsync(context),
            processor.ExecuteAsync(context)
        );

        sw.Stop();
        var endMemory = GC.GetTotalMemory(false);

        Console.WriteLine($"[DefaultBuffer] Time: {sw.ElapsedMilliseconds}ms, Memory Delta: {(endMemory - startMemory) / 1024}KB");
    }

    [Benchmark]
    public async Task InlineTransformBlock_NoBuffer()
    {
        var processedItems = new ConcurrentBag<string>();
        var channelFactory = _sp.GetRequiredService<IBoundedChannelFactory>();

        var producer = new ProducerBlock<int>(
            "producer",
            _sp.GetRequiredService<ILogger<ProducerBlock<int>>>(),
            channelFactory,
            new ProducerBlockOptions<int>
            {
                ProducersFactory = (ctx, ct) => Task.FromResult<IEnumerable<IStreamProducer<int>>>(
                    new[] { new RangeProducer(1, ItemCount) })
            });

        var transform = new InlineTransformBlock<int, string>(
            "transform",
            _sp.GetRequiredService<ILogger<InlineTransformBlock<int, string>>>(),
            x => $"Item_{x}");

        var processor = new ProcessorBlock<string>(
            "processor",
            _sp.GetRequiredService<ILogger<ProcessorBlock<string>>>(),
            sp => new CountingProcessor());

        transform.SetSource(producer);
        processor.SetSource(transform);

        var context = new DataFlowContext(Guid.NewGuid())
        {
            ServiceProvider = _sp,
            Name = "inline-transform"
        };

        var startMemory = GC.GetTotalMemory(false);
        var sw = Stopwatch.StartNew();

        await Task.WhenAll(
            producer.ExecuteAsync(context),
            transform.ExecuteAsync(context),
            processor.ExecuteAsync(context)
        );

        sw.Stop();
        var endMemory = GC.GetTotalMemory(false);

        Console.WriteLine($"[InlineTransform] Time: {sw.ElapsedMilliseconds}ms, Memory Delta: {(endMemory - startMemory) / 1024}KB");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _sp?.Dispose();
    }

    // Helper classes
    private class RangeProducer : IStreamProducer<int>
    {
        private readonly int _start;
        private readonly int _count;

        public RangeProducer(int start, int count)
        {
            _start = start;
            _count = count;
        }

        public async IAsyncEnumerable<int> ProduceAsync(
            IDataFlowContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (var i = 0; i < _count; i++)
            {
                yield return _start + i;
                await Task.Yield();
            }
        }
    }

    private class SimpleTransformer : IStreamTransformer<int, string>
    {
        public async IAsyncEnumerable<string> TransformAsync(
            IDataFlowContext context,
            IAsyncEnumerable<int> input,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in input.WithCancellation(cancellationToken))
            {
                yield return $"Item_{item}";
            }
        }
    }

    private class CountingProcessor : IStreamProcessor<string>
    {
        public async Task ProcessAsync(
            IDataFlowContext context,
            IAsyncEnumerable<string> input,
            CancellationToken cancellationToken)
        {
            var count = 0;
            await foreach (var item in input.WithCancellation(cancellationToken))
            {
                count++;
            }
        }
    }
}
