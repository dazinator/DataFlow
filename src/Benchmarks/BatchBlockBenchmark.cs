namespace Benchmarks;

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks.BatchBlock;
using Uniun.DataFlow.Blocks.InputChannel;
using Uniun.DataFlow.Builder;

/// <summary>
/// Benchmark comparing the old BatchBlock implementation (with continuous timer)
/// vs the new implementation (with on-demand timer).
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[Config(typeof(Config))]
public class BatchBlockBenchmark
{
    private class Config : ManualConfig
    {
        public Config()
        {
            AddColumn(StatisticColumn.Mean);
            AddColumn(StatisticColumn.StdDev);
            AddColumn(StatisticColumn.Median);
            AddColumn(StatisticColumn.Min);
            AddColumn(StatisticColumn.Max);
        }
    }

    private ServiceProvider? _serviceProvider;

    [Params(1000, 5000)] // Number of items to process
    public int ItemCount { get; set; }

    [Params(10, 50)] // Batch size
    public int BatchSize { get; set; }

    [Params(100)] // Window period in milliseconds
    public int WindowPeriodMs { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddDataFlowMetrics();
        services.AddDataFlows();
        _serviceProvider = services.BuildServiceProvider();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _serviceProvider?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task OldBatchBlock_ContinuousTimer()
    {
        var processedBatches = 0;
        var tcs = new TaskCompletionSource<bool>();

        var builder = new DataFlowBuilder(_serviceProvider);

        // Use InputChannel to control item flow
        builder.AddInputChannel<int>("source", _ => { });

        var inputBlock = builder.GetSourceBlock<int>("source") as InputChannelBlock<int>;

        // Create OLD batch block manually since we need to use OldBatchBlock
        var oldBatchBlock = ActivatorUtilities.CreateInstance<OldBatchBlock<int>>(
            _serviceProvider,
            "batcher",
            new BatchBlockOptions
            {
                MaxBatchSize = BatchSize,
                WindowPeriod = TimeSpan.FromMilliseconds(WindowPeriodMs)
            });

        var sourceBlock = builder.GetSourceBlock<int>("source");
        oldBatchBlock.SetSource(sourceBlock);

        builder.AddSourceBlock("batcher", oldBatchBlock);

        builder.AddProcessor<int[], SimpleProcessor<int[]>>("processor", sp => new SimpleProcessor<int[]>(
            onProcess: _ =>
            {
                var count = Interlocked.Increment(ref processedBatches);
                var expectedBatches = (int)Math.Ceiling((double)ItemCount / BatchSize);
                if (count >= expectedBatches)
                {
                    tcs.TrySetResult(true);
                }
            }))
            .ReceiveFrom("batcher");

        var flow = builder.Build();
        var context = new DataFlowContext
        {
            InvocationId = Guid.NewGuid(),
            ServiceProvider = _serviceProvider,
            CancellationToken = CancellationToken.None,
            Name = "OldBatchBlockBenchmark"
        };

        var flowTask = flow.ExecuteAsync(context);

        // Push items quickly to trigger size-based batching
        for (var i = 0; i < ItemCount; i++)
        {
            await inputBlock.WriteAsync(i);
        }

        inputBlock.Complete();
        await tcs.Task;
        await flowTask;
    }

    [Benchmark]
    public async Task NewBatchBlock_OnDemandTimer()
    {
        var processedBatches = 0;
        var tcs = new TaskCompletionSource<bool>();

        var builder = new DataFlowBuilder(_serviceProvider);

        builder.AddInputChannel<int>("source", _ => { })
            .AddBatch<int>("batcher",
                maxBatchSize: BatchSize,
                windowPeriod: TimeSpan.FromMilliseconds(WindowPeriodMs))
            .ReceiveFrom("source")
            .AddProcessor<int[], SimpleProcessor<int[]>>("processor", sp => new SimpleProcessor<int[]>(
                onProcess: _ =>
                {
                    var count = Interlocked.Increment(ref processedBatches);
                    var expectedBatches = (int)Math.Ceiling((double)ItemCount / BatchSize);
                    if (count >= expectedBatches)
                    {
                        tcs.TrySetResult(true);
                    }
                }))
            .ReceiveFrom("batcher");

        var inputBlock = builder.GetSourceBlock<int>("source") as InputChannelBlock<int>;

        var flow = builder.Build();
        var context = new DataFlowContext
        {
            InvocationId = Guid.NewGuid(),
            ServiceProvider = _serviceProvider,
            CancellationToken = CancellationToken.None,
            Name = "NewBatchBlockBenchmark"
        };

        var flowTask = flow.ExecuteAsync(context);

        // Push items quickly to trigger size-based batching
        for (var i = 0; i < ItemCount; i++)
        {
            await inputBlock.WriteAsync(i);
        }

        inputBlock.Complete();
        await tcs.Task;
        await flowTask;
    }
}
