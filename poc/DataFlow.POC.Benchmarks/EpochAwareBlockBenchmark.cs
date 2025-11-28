namespace DataFlow.POC.Benchmarks;

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DataFlow.POC.Benchmarks.DeprecatedBlocks;
using DataFlow.POC.Blocks;
using DataFlow.POC.Checkpointing;
using DataFlow.POC.Core;
using DataFlow.POC.Observability;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

/// <summary>
/// Benchmarks for epoch-aware blocks (EpochActorBlock and EpochBatchBlock).
/// Validates that epoch-aware blocks have acceptable overhead compared to plain blocks.
/// Target: &lt;10% overhead vs plain blocks in micro-benchmarks.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class EpochAwareBlockBenchmark
{
    private const int TotalItems = 10000;
    private const int ItemsPerEpoch = 100;
    private IServiceProvider _serviceProvider = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddTransient<BenchmarkPlainSource>();
        services.AddTransient<SimpleTransformActor>();
        services.AddTransient<EpochSimpleTransformActor>();
        _serviceProvider = services.BuildServiceProvider();
    }

    // ========================================
    // Baseline: Plain ActorBlock
    // ========================================

    [Benchmark(Baseline = true, Description = "ActorBlock (plain stream)")]
    public async Task<int> PlainActorBlock_Transform()
    {
        var sourceBlock = new PlainSourceBlock<int, BenchmarkPlainSource>(
            "source",
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var actorBlock = new ActorBlock<int, int, SimpleTransformActor>(
            "actor",
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var context = new BenchmarkExecutionContext();
        var count = 0;

        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var transformed = actorBlock.ExecuteAsync(plainItems, context);

        await foreach (var item in transformed)
        {
            count++;
        }

        return count;
    }

    // ========================================
    // EpochActorBlock: Transformation
    // ========================================

    [Benchmark(Description = "EpochActorBlock (1-to-1 transform)")]
    public async Task<int> EpochActorBlock_Transform()
    {
        var sourceBlock = new PlainSourceBlock<int, BenchmarkPlainSource>(
            "source",
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var segmenter = new EpochSegmenterBlock<int>(new BlockContext("segmenter"),
            EpochSegmentationPolicy.ByCount(ItemsPerEpoch, "benchmark"));

        var epochActor = new EpochActorBlock<int, int, EpochSimpleTransformActor>(new BlockContext("epoch-actor"),
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var context = new BenchmarkExecutionContext();
        var count = 0;

        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenter.ExecuteAsync(plainItems, context);
        var transformed = epochActor.ExecuteAsync(epochStreams, context);

        await foreach (var epochStream in transformed)
        {
            await foreach (var item in epochStream.Items)
            {
                count++;
            }
        }

        return count;
    }

    // ========================================
    // EpochActorBlock: 1-to-Many
    // ========================================

    [Benchmark(Description = "EpochActorBlock (1-to-many)")]
    public async Task<int> EpochActorBlock_OneToMany()
    {
        var sourceBlock = new PlainSourceBlock<int, BenchmarkPlainSource>(
            "source",
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var segmenter = new EpochSegmenterBlock<int>(new BlockContext("segmenter"),
            EpochSegmentationPolicy.ByCount(ItemsPerEpoch, "benchmark"));

        var epochActor = new EpochActorBlock<int, int, OneToManyActor>(new BlockContext("epoch-actor"),
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var context = new BenchmarkExecutionContext();
        var count = 0;

        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenter.ExecuteAsync(plainItems, context);
        var transformed = epochActor.ExecuteAsync(epochStreams, context);

        await foreach (var epochStream in transformed)
        {
            await foreach (var item in epochStream.Items)
            {
                count++;
            }
        }

        return count;
    }

    // ========================================
    // EpochActorBlock: Filtering
    // ========================================

    [Benchmark(Description = "EpochActorBlock (filtering)")]
    public async Task<int> EpochActorBlock_Filter()
    {
        var sourceBlock = new PlainSourceBlock<int, BenchmarkPlainSource>(
            "source",
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var segmenter = new EpochSegmenterBlock<int>(new BlockContext("segmenter"),
            EpochSegmentationPolicy.ByCount(ItemsPerEpoch, "benchmark"));

        var epochActor = new EpochActorBlock<int, int, FilterEvenActor>(new BlockContext("epoch-actor"),
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var context = new BenchmarkExecutionContext();
        var count = 0;

        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenter.ExecuteAsync(plainItems, context);
        var filtered = epochActor.ExecuteAsync(epochStreams, context);

        await foreach (var epochStream in filtered)
        {
            await foreach (var item in epochStream.Items)
            {
                count++;
            }
        }

        return count;
    }

    // ========================================
    // EpochBatchBlock
    // ========================================

    [Benchmark(Description = "EpochBatchBlock (batching)")]
    public async Task<int> EpochBatchBlock_Batching()
    {
        var sourceBlock = new PlainSourceBlock<int, BenchmarkPlainSource>(
            "source",
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var segmenter = new EpochSegmenterBlock<int>(new BlockContext("segmenter"),
            EpochSegmentationPolicy.ByCount(ItemsPerEpoch, "benchmark"));

        var batcher = new EpochBatchBlock<int>(new BlockContext("batcher"),
            maxBatchSize: 10);

        var context = new BenchmarkExecutionContext();
        var batchCount = 0;
        var itemCount = 0;

        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenter.ExecuteAsync(plainItems, context);
        var batched = batcher.ExecuteAsync(epochStreams, context);

        await foreach (var epochStream in batched)
        {
            await foreach (var batch in epochStream.Items)
            {
                batchCount++;
                itemCount += batch.Length;
            }
        }

        return itemCount;
    }

    // ========================================
    // Composed Pipeline: Transform → Batch
    // ========================================

    [Benchmark(Description = "EpochActorBlock + EpochBatchBlock (composed)")]
    public async Task<int> ComposedPipeline_TransformAndBatch()
    {
        var sourceBlock = new PlainSourceBlock<int, BenchmarkPlainSource>(
            "source",
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var segmenter = new EpochSegmenterBlock<int>(new BlockContext("segmenter"),
            EpochSegmentationPolicy.ByCount(ItemsPerEpoch, "benchmark"));

        var transformer = new EpochActorBlock<int, int, EpochSimpleTransformActor>(new BlockContext("transformer"),
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var batcher = new EpochBatchBlock<int>(new BlockContext("batcher"),
            maxBatchSize: 10);

        var context = new BenchmarkExecutionContext();
        var itemCount = 0;

        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenter.ExecuteAsync(plainItems, context);
        var transformed = transformer.ExecuteAsync(epochStreams, context);
        var batched = batcher.ExecuteAsync(transformed, context);

        await foreach (var epochStream in batched)
        {
            await foreach (var batch in epochStream.Items)
            {
                itemCount += batch.Length;
            }
        }

        return itemCount;
    }

    // ========================================
    // Helper classes and actors
    // ========================================

    private static async IAsyncEnumerable<object> EmptyInput()
    {
        yield break;
    }

    private class BenchmarkPlainSource : PlainSourceActorBase<int>
    {
        public override async IAsyncEnumerable<int> ProduceAsync(
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            for (int i = 0; i < TotalItems; i++)
            {
                yield return i;
            }
            await Task.CompletedTask;
        }
    }

    private class SimpleTransformActor : IStreamActor<int, int>
    {
        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                yield return item * 2;
            }
        }
    }

    private class EpochSimpleTransformActor : IStreamActor<int, int>
    {
        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                yield return item * 2;
            }
        }
    }

    private class OneToManyActor : IStreamActor<int, int>
    {
        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                yield return item;
                yield return item * 2;
            }
        }
    }

    private class FilterEvenActor : IStreamActor<int, int>
    {
        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                if (item % 2 == 0)
                {
                    yield return item;
                }
            }
        }
    }

    private class BenchmarkExecutionContext : IExecutionContext
    {
        public CancellationToken CancellationToken { get; } = CancellationToken.None;
        public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
        public Guid InvocationId { get; } = Guid.NewGuid();
        public ICheckpoint? RecoveryCheckpoint { get; } = null;
        public IDataFlowMetrics? Metrics { get; } = null;
    }
}

/// <summary>
/// Performance benchmarks for epoch-aware blocks with realistic processing overhead.
/// Tests with 30ms processing time per item to simulate real-world scenarios.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 1, iterationCount: 3)]
public class EpochAwareBlockRealisticBenchmark
{
    private const int TotalItems = 100;
    private const int ItemsPerEpoch = 10;
    private const int ProcessingDelayMs = 30;
    private IServiceProvider _serviceProvider = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddTransient<RealisticPlainSource>();
        services.AddTransient<RealisticTransformActor>();
        services.AddTransient<EpochRealisticTransformActor>();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Benchmark(Baseline = true, Description = "ActorBlock with realistic processing")]
    public async Task<int> PlainActorBlock_RealisticProcessing()
    {
        var sourceBlock = new PlainSourceBlock<int, RealisticPlainSource>(
            "source",
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var actorBlock = new ActorBlock<int, int, RealisticTransformActor>(
            "actor",
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var context = new BenchmarkExecutionContext();
        var count = 0;

        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var transformed = actorBlock.ExecuteAsync(plainItems, context);

        await foreach (var item in transformed)
        {
            count++;
        }

        return count;
    }

    [Benchmark(Description = "EpochActorBlock with realistic processing")]
    public async Task<int> EpochActorBlock_RealisticProcessing()
    {
        var sourceBlock = new PlainSourceBlock<int, RealisticPlainSource>(
            "source",
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var segmenter = new EpochSegmenterBlock<int>(new BlockContext("segmenter"),
            EpochSegmentationPolicy.ByCount(ItemsPerEpoch, "benchmark"));

        var epochActor = new EpochActorBlock<int, int, EpochRealisticTransformActor>(new BlockContext("epoch-actor"),
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var context = new BenchmarkExecutionContext();
        var count = 0;

        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenter.ExecuteAsync(plainItems, context);
        var transformed = epochActor.ExecuteAsync(epochStreams, context);

        await foreach (var epochStream in transformed)
        {
            await foreach (var item in epochStream.Items)
            {
                count++;
            }
        }

        return count;
    }

    private static async IAsyncEnumerable<object> EmptyInput()
    {
        yield break;
    }

    private class RealisticPlainSource : PlainSourceActorBase<int>
    {
        public override async IAsyncEnumerable<int> ProduceAsync(
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            for (int i = 0; i < TotalItems; i++)
            {
                yield return i;
            }
            await Task.CompletedTask;
        }
    }

    private class RealisticTransformActor : IStreamActor<int, int>
    {
        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                // Simulate realistic processing (database query, API call, etc.)
                await Task.Delay(ProcessingDelayMs, context.CancellationToken);
                yield return item * 2;
            }
        }
    }

    private class EpochRealisticTransformActor : IStreamActor<int, int>
    {
        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                // Simulate realistic processing
                await Task.Delay(ProcessingDelayMs, context.CancellationToken);
                yield return item * 2;
            }
        }
    }

    private class BenchmarkExecutionContext : IExecutionContext
    {
        public CancellationToken CancellationToken { get; } = CancellationToken.None;
        public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
        public Guid InvocationId { get; } = Guid.NewGuid();
        public ICheckpoint? RecoveryCheckpoint { get; } = null;
        public IDataFlowMetrics? Metrics { get; } = null;
    }
}
