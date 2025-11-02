using BenchmarkDotNet.Attributes;
using DataFlow.POC.Core;

namespace DataFlow.POC.Benchmarks;

/// <summary>
/// Benchmarks epoch infrastructure scaling with varying granularity (1-10,000 epochs).
/// Tests the same data stream (100,000 items) to identify sweet spot (100-1,000 items/epoch).
/// Shows overhead dominated by epoch count, not size, with graceful degradation.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class EpochGranularityScalingBenchmark
{
    private const int TotalItems = 100_000;
    private IAsyncEnumerable<int> _data = null!;

    [Params(1, 10, 100, 1000, 10000)]
    public int EpochCount { get; set; }

    private int ItemsPerEpoch => TotalItems / EpochCount;

    [GlobalSetup]
    public void Setup()
    {
        _data = GenerateDataAsync();
    }

    private async IAsyncEnumerable<int> GenerateDataAsync()
    {
        for (int i = 0; i < TotalItems; i++)
        {
            yield return i;
            // Add realistic async work simulation (50% of items)
            if ((i & 1) == 1)
                await Task.Delay(0);
        }
    }

    [Benchmark(Baseline = true)]
    public async Task Baseline_NoEpochs()
    {
        await foreach (var item in _data)
        {
            await SimulateWork(item);
        }
    }

    [Benchmark]
    public async Task WithEpochs_Sequential()
    {
        var config = new EpochSegmenterConfig();
        var epochs = EpochSegmenter.SegmentByKey(
            _data,
            item => item / ItemsPerEpoch,  // Group items into epochs
            "epoch-granularity-test");

        await foreach (var epoch in epochs)
        {
            await foreach (var item in epoch.Items)
            {
                await SimulateWork(item);
            }
        }
    }

    [Benchmark]
    public async Task WithEpochs_Overlapped()
    {
        var config = new EpochSegmenterConfig { ExecutionPolicy = EpochExecutionPolicy.Overlapped };
        var epochs = EpochSegmenter.SegmentByKey(
            _data,
            item => item / ItemsPerEpoch,  // Group items into epochs
            "epoch-granularity-test",
            config);

        await foreach (var epoch in epochs.ConfigureAwait(false))
        {
            await foreach (var item in epoch.Items.ConfigureAwait(false))
            {
                await SimulateWork(item);
            }
        }
    }

    private static ValueTask SimulateWork(int i)
    {
        // Simulate realistic async work pattern
        return (i & 1) == 0 ? ValueTask.CompletedTask : new ValueTask(Task.CompletedTask);
    }
}
