namespace Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks.Producer;
using Uniun.DataFlow.Blocks.Processor;
using Uniun.DataFlow.Blocks.Transform;

/// <summary>
/// Benchmarks comparing different transform block execution models:
/// 1. InlineTransformBlock - Inline execution during downstream enumeration
/// 2. TransformBlock - Default buffered execution (capacity=100, concurrency=1)
/// 3. TransformBlock variants:
///    - Reduced buffer capacity (1, 50)
///    - Increased concurrency (2, 4 concurrent actors)
/// 
/// Tests multiple scenarios:
/// - Different item counts (100, 1K, 10K)
/// - Memory usage and throughput
/// - CPU usage
/// - Throughput (items/sec)
/// </summary>
[SimpleJob(RunStrategy.Throughput, warmupCount: 1, iterationCount: 3)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class TransformBlockExecutionModelBenchmarks
{
    private ServiceProvider _sp;
    private ILogger<TransformBlockExecutionModelBenchmarks> _logger;

    [Params(100, 1000, 10000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddDataFlows();
        services.AddDataFlowMetrics();

        _sp = services.BuildServiceProvider();
        _logger = _sp.GetRequiredService<ILogger<TransformBlockExecutionModelBenchmarks>>();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> TransformBlock_DefaultBuffer()
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
            sp => new CollectingProcessor(processedItems));

        transform.SetSource(producer);
        processor.SetSource(transform);

        var context = new DataFlowContext(Guid.NewGuid()) 
        { 
            ServiceProvider = _sp, 
            Name = "default-buffer" 
        };

        await Task.WhenAll(
            producer.ExecuteAsync(context),
            transform.ExecuteAsync(context),
            processor.ExecuteAsync(context)
        );

        return processedItems.Count;
    }

    [Benchmark]
    public async Task<int> TransformBlock_ReducedBuffer_1()
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
                TransformerFactory = sp => new SimpleTransformer(),
                Capacity = 1  // Reduced buffer capacity
            });

        var processor = new ProcessorBlock<string>(
            "processor",
            _sp.GetRequiredService<ILogger<ProcessorBlock<string>>>(),
            sp => new CollectingProcessor(processedItems));

        transform.SetSource(producer);
        processor.SetSource(transform);

        var context = new DataFlowContext(Guid.NewGuid()) 
        { 
            ServiceProvider = _sp, 
            Name = "reduced-buffer-1" 
        };

        await Task.WhenAll(
            producer.ExecuteAsync(context),
            transform.ExecuteAsync(context),
            processor.ExecuteAsync(context)
        );

        return processedItems.Count;
    }

    [Benchmark]
    public async Task<int> TransformBlock_ReducedBuffer_50()
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
                TransformerFactory = sp => new SimpleTransformer(),
                Capacity = 50  // Reduced buffer capacity
            });

        var processor = new ProcessorBlock<string>(
            "processor",
            _sp.GetRequiredService<ILogger<ProcessorBlock<string>>>(),
            sp => new CollectingProcessor(processedItems));

        transform.SetSource(producer);
        processor.SetSource(transform);

        var context = new DataFlowContext(Guid.NewGuid()) 
        { 
            ServiceProvider = _sp, 
            Name = "reduced-buffer-50" 
        };

        await Task.WhenAll(
            producer.ExecuteAsync(context),
            transform.ExecuteAsync(context),
            processor.ExecuteAsync(context)
        );

        return processedItems.Count;
    }

    [Benchmark]
    public async Task<int> TransformBlock_Concurrency_2()
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
                TransformerFactory = sp => new SimpleTransformer(),
                MaxConcurrency = 2  // 2 concurrent actors
            });

        var processor = new ProcessorBlock<string>(
            "processor",
            _sp.GetRequiredService<ILogger<ProcessorBlock<string>>>(),
            sp => new CollectingProcessor(processedItems));

        transform.SetSource(producer);
        processor.SetSource(transform);

        var context = new DataFlowContext(Guid.NewGuid()) 
        { 
            ServiceProvider = _sp, 
            Name = "concurrency-2" 
        };

        await Task.WhenAll(
            producer.ExecuteAsync(context),
            transform.ExecuteAsync(context),
            processor.ExecuteAsync(context)
        );

        return processedItems.Count;
    }

    [Benchmark]
    public async Task<int> TransformBlock_Concurrency_4()
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
                TransformerFactory = sp => new SimpleTransformer(),
                MaxConcurrency = 4  // 4 concurrent actors
            });

        var processor = new ProcessorBlock<string>(
            "processor",
            _sp.GetRequiredService<ILogger<ProcessorBlock<string>>>(),
            sp => new CollectingProcessor(processedItems));

        transform.SetSource(producer);
        processor.SetSource(transform);

        var context = new DataFlowContext(Guid.NewGuid()) 
        { 
            ServiceProvider = _sp, 
            Name = "concurrency-4" 
        };

        await Task.WhenAll(
            producer.ExecuteAsync(context),
            transform.ExecuteAsync(context),
            processor.ExecuteAsync(context)
        );

        return processedItems.Count;
    }

    [Benchmark]
    public async Task<int> InlineTransformBlock_NoBuffer()
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
            sp => new CollectingProcessor(processedItems));

        transform.SetSource(producer);
        processor.SetSource(transform);

        var context = new DataFlowContext(Guid.NewGuid()) 
        { 
            ServiceProvider = _sp, 
            Name = "inline-transform" 
        };

        await Task.WhenAll(
            producer.ExecuteAsync(context),
            transform.ExecuteAsync(context),
            processor.ExecuteAsync(context)
        );

        return processedItems.Count;
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
            for (int i = 0; i < _count; i++)
            {
                yield return _start + i;
                await Task.Yield(); // Allow cooperative multitasking
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

    private class CollectingProcessor : IStreamProcessor<string>
    {
        private readonly ConcurrentBag<string> _items;

        public CollectingProcessor(ConcurrentBag<string> items)
        {
            _items = items;
        }

        public async Task ProcessAsync(
            IDataFlowContext context,
            IAsyncEnumerable<string> input,
            CancellationToken cancellationToken)
        {
            await foreach (var item in input.WithCancellation(cancellationToken))
            {
                _items.Add(item);
            }
        }
    }
}
