namespace DataFlow.POC.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DataFlow.POC.Blocks;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

/// <summary>
/// Benchmark comparing source-centric (current) vs decoupled epoch segmentation.
/// Validates that the decoupled design has acceptable performance overhead.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class DecoupledEpochBenchmark
{
    private const int TotalItems = 1000;
    private const int ItemsPerEpoch = 100;
    private IServiceProvider _serviceProvider = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddTransient<BenchmarkPlainSource>();
        services.AddTransient<BenchmarkSourceCentricSource>();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> SourceCentric_CountSegmentation()
    {
        // Current approach: Source emits epoch streams directly
        var sourceBlock = new EpochSourceBlock<int, BenchmarkSourceCentricSource>(
            "source",
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var context = new BenchmarkExecutionContext();
        var count = 0;

        await foreach (var epochStream in sourceBlock.ExecuteAsync(EmptyInput(), context))
        {
            await foreach (var item in epochStream.Items)
            {
                count++;
            }
        }

        return count;
    }

    [Benchmark]
    public async Task<int> Decoupled_CountSegmentation()
    {
        // Decoupled approach: Plain source + external segmenter
        var plainSourceBlock = new PlainSourceBlock<int, BenchmarkPlainSource>(
            "plain-source",
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var segmenterBlock = new EpochSegmenterBlock<int>(
            "segmenter",
            EpochSegmentationPolicy.ByCount(ItemsPerEpoch, "source"));

        var context = new BenchmarkExecutionContext();
        var count = 0;

        var plainItems = plainSourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);

        await foreach (var epochStream in epochStreams)
        {
            await foreach (var item in epochStream.Items)
            {
                count++;
            }
        }

        return count;
    }

    [Benchmark]
    public async Task<int> SourceCentric_WithProcessing()
    {
        // Current approach with realistic processing work (30ms per item)
        var sourceBlock = new EpochSourceBlock<int, BenchmarkSourceCentricSource>(
            "source",
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var context = new BenchmarkExecutionContext();
        var count = 0;

        await foreach (var epochStream in sourceBlock.ExecuteAsync(EmptyInput(), context))
        {
            await foreach (var item in epochStream.Items)
            {
                await Task.Delay(30); // Simulate processing
                count++;
            }
        }

        return count;
    }

    [Benchmark]
    public async Task<int> Decoupled_WithProcessing()
    {
        // Decoupled approach with realistic processing work (30ms per item)
        var plainSourceBlock = new PlainSourceBlock<int, BenchmarkPlainSource>(
            "plain-source",
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var segmenterBlock = new EpochSegmenterBlock<int>(
            "segmenter",
            EpochSegmentationPolicy.ByCount(ItemsPerEpoch, "source"));

        var context = new BenchmarkExecutionContext();
        var count = 0;

        var plainItems = plainSourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);

        await foreach (var epochStream in epochStreams)
        {
            await foreach (var item in epochStream.Items)
            {
                await Task.Delay(30); // Simulate processing
                count++;
            }
        }

        return count;
    }

    [Benchmark]
    public async Task<int> PlainSource_NoEpochs()
    {
        // Plain source without any epoch infrastructure (baseline)
        var plainSourceBlock = new PlainSourceBlock<int, BenchmarkPlainSource>(
            "plain-source",
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var context = new BenchmarkExecutionContext();
        var count = 0;

        await foreach (var item in plainSourceBlock.ExecuteAsync(EmptyInput(), context))
        {
            count++;
        }

        return count;
    }

    // Helper methods and classes

    private static async IAsyncEnumerable<object> EmptyInput()
    {
        await Task.CompletedTask;
        yield break;
    }

    // Plain source for decoupled approach
    private class BenchmarkPlainSource : PlainSourceActorBase<int>
    {
        public override async IAsyncEnumerable<int> ProduceAsync(
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            for (int i = 0; i < TotalItems; i++)
            {
                yield return i;
            }
        }
    }

    // Source-centric source for current approach
    private class BenchmarkSourceCentricSource : SourceActorBase<int>
    {
        public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            // Mimic count-based segmentation
            for (int epochIndex = 0; epochIndex * ItemsPerEpoch < TotalItems; epochIndex++)
            {
                var startIdx = epochIndex * ItemsPerEpoch;
                var endIdx = Math.Min(startIdx + ItemsPerEpoch, TotalItems);
                
                yield return CreateEpochStream(
                    CreateEpoch("source", epochIndex + 1),
                    ProduceEpochItems(startIdx, endIdx));
            }
        }

        private static async IAsyncEnumerable<int> ProduceEpochItems(int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                yield return i;
            }
        }
    }

    private class BenchmarkExecutionContext : IExecutionContext
    {
        public CancellationToken CancellationToken { get; } = CancellationToken.None;
        public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
        public Guid InvocationId { get; } = Guid.NewGuid();
    }
}
