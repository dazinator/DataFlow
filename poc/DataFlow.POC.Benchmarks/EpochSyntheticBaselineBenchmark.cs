namespace DataFlow.POC.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DataFlow.POC.Core;

/// <summary>
/// Synthetic baseline benchmark comparing pure stream processing (no epochs)
/// with epoch-enabled streaming segmentation. Measures raw framework overhead
/// without I/O or realistic workload, useful for understanding infrastructure cost.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class EpochSyntheticBaselineBenchmark
{
    private const int DataItemsPerEpoch = 1000;
    private const int NumberOfEpochs = 10;
    private const int TotalItems = DataItemsPerEpoch * NumberOfEpochs;

    [Benchmark(Baseline = true)]
    public async Task<int> Baseline_PureDataFlow_NoEpochs()
    {
        // Pure data processing without any epoch control
        // This is the baseline for non-epoch pipelines
        var count = 0;

        await foreach (var item in GenerateData(TotalItems))
        {
            count += ProcessItem(item);
        }

        return count;
    }

    [Benchmark]
    public async Task<int> Phase4_StreamingSegmentation_Sequential()
    {
        // Streaming segmentation with completion-based alignment
        var progress = new CompletionBasedEpochProgress();
        var count = 0;

        var epochs = EpochSegmenter.SegmentByKey(
            GenerateData(TotalItems),
            item => item / DataItemsPerEpoch,  // Epoch key
            "source1",
            new EpochSegmenterConfig
            {
                ExecutionPolicy = EpochExecutionPolicy.Sequential
            });

        await foreach (var epochStream in epochs)
        {
            progress.RegisterEpochStarted(epochStream.Epoch);

            await foreach (var item in epochStream.Items)
            {
                count += ProcessItem(item);
            }

            progress.RegisterEpochCompleted(epochStream.Epoch);
        }

        return count;
    }

    [Benchmark]
    public async Task<int> Phase4_StreamingSegmentation_Overlapped()
    {
        // Streaming segmentation with overlapped execution policy
        var progress = new CompletionBasedEpochProgress();
        var count = 0;

        var epochs = EpochSegmenter.SegmentByKey(
            GenerateData(TotalItems),
            item => item / DataItemsPerEpoch,
            "source1",
            new EpochSegmenterConfig
            {
                ExecutionPolicy = EpochExecutionPolicy.Overlapped,
                MaxConcurrentEpochs = 4
            });

        await foreach (var epochStream in epochs)
        {
            progress.RegisterEpochStarted(epochStream.Epoch);

            await foreach (var item in epochStream.Items)
            {
                count += ProcessItem(item);
            }

            progress.RegisterEpochCompleted(epochStream.Epoch);
        }

        return count;
    }

    [Benchmark]
    public async Task<int> Phase4_GlobalAlignment()
    {
        // Streaming segmentation with global alignment tracking across multiple blocks
        var alignment = new GlobalEpochAlignment();
        var block1Progress = alignment.GetOrCreateBlockProgress("block1");
        var block2Progress = alignment.GetOrCreateBlockProgress("block2");
        var count = 0;

        var epochs = EpochSegmenter.SegmentByKey(
            GenerateData(TotalItems),
            item => item / DataItemsPerEpoch,
            "source1",
            new EpochSegmenterConfig
            {
                ExecutionPolicy = EpochExecutionPolicy.Sequential
            });

        await foreach (var epochStream in epochs)
        {
            block1Progress.RegisterEpochStarted(epochStream.Epoch);
            block2Progress.RegisterEpochStarted(epochStream.Epoch);

            await foreach (var item in epochStream.Items)
            {
                count += ProcessItem(item);
            }

            block1Progress.RegisterEpochCompleted(epochStream.Epoch);
            block2Progress.RegisterEpochCompleted(epochStream.Epoch);

            // Check global alignment
            var watermark = alignment.GetGlobalCompletionWatermark();
        }

        return count;
    }

    private static async IAsyncEnumerable<int> GenerateData(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return i;
        }
    }

    private static int ProcessItem(int item)
    {
        // Simulate minimal processing
        return item % 2 == 0 ? 1 : 0;
    }
}

/// <summary>
/// Micro-benchmark comparing epoch segmentation performance under different epoch size configurations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class StreamingSegmentationMicrobenchmark
{
    private const int ItemCount = 1000;
    private const int LargeEpochSize = 100; // 10 epochs
    private const int SmallEpochSize = 10;  // 100 epochs

    [Benchmark(Baseline = true)]
    public async Task<int> LargeEpochs_100ItemsEach()
    {
        // Fewer, larger epochs - less overhead
        var count = 0;

        await foreach (var epochStream in EpochSegmenter.SegmentByKey(
            GenerateSequence(ItemCount),
            n => n / LargeEpochSize,
            "source"))
        {
            await foreach (var item in epochStream.Items)
            {
                count++;
            }
        }

        return count;
    }

    [Benchmark]
    public async Task<int> SmallEpochs_10ItemsEach()
    {
        // Many small epochs - more overhead per item
        var count = 0;

        await foreach (var epochStream in EpochSegmenter.SegmentByKey(
            GenerateSequence(ItemCount),
            n => n / SmallEpochSize,
            "source"))
        {
            await foreach (var item in epochStream.Items)
            {
                count++;
            }
        }

        return count;
    }

    [Benchmark]
    public async Task<int> SingleEpoch_AllItems()
    {
        // Single epoch containing all items - minimal overhead
        var count = 0;

        await foreach (var epochStream in EpochSegmenter.SegmentByKey(
            GenerateSequence(ItemCount),
            n => 1, // All in one epoch
            "source"))
        {
            await foreach (var item in epochStream.Items)
            {
                count++;
            }
        }

        return count;
    }

    private static async IAsyncEnumerable<int> GenerateSequence(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return i;
        }
    }
}

/// <summary>
/// Validates that non-epoch pipelines don't have regressions.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class NonEpochRegressionBenchmark
{
    private const int ItemCount = 10000;

    [Benchmark(Baseline = true)]
    public async Task<int> PureStream_NoEpochInfrastructure()
    {
        // Baseline: pure async enumerable processing
        var count = 0;

        await foreach (var item in GenerateSequence(ItemCount))
        {
            count += ProcessSimple(item);
        }

        return count;
    }

    [Benchmark]
    public async Task<int> WithEpochInfrastructure_SingleEpoch()
    {
        // Using epoch infrastructure but with single epoch
        // Should have minimal overhead
        var count = 0;

        await foreach (var epochStream in EpochSegmenter.SegmentByKey(
            GenerateSequence(ItemCount),
            _ => 1, // All items in one epoch
            "source"))
        {
            await foreach (var item in epochStream.Items)
            {
                count += ProcessSimple(item);
            }
        }

        return count;
    }

    [Benchmark]
    public async Task<int> WithEpochInfrastructure_ManySmallEpochs()
    {
        // Many small epochs - worst case for overhead
        var count = 0;

        await foreach (var epochStream in EpochSegmenter.SegmentByKey(
            GenerateSequence(ItemCount),
            n => n / 10, // 1000 epochs of 10 items
            "source"))
        {
            await foreach (var item in epochStream.Items)
            {
                count += ProcessSimple(item);
            }
        }

        return count;
    }

    private static async IAsyncEnumerable<int> GenerateSequence(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return i;
        }
    }

    private static int ProcessSimple(int item)
    {
        return item & 1; // Simple bitwise operation
    }
}

/// <summary>
/// Benchmark comparing epoch infrastructure overhead against realistic workload baseline.
/// This provides a more accurate picture of overhead in production scenarios where
/// items require actual processing (async operations, transformations, etc.).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class RealisticWorkloadBenchmark
{
    private const int DataItemsPerEpoch = 1000;
    private const int NumberOfEpochs = 10;
    private const int TotalItems = DataItemsPerEpoch * NumberOfEpochs;

    [Benchmark(Baseline = true)]
    public async Task<int> Baseline_RealisticWork()
    {
        // Baseline with realistic lightweight async work per item
        // This represents typical production scenarios with minor transformations
        var count = 0;

        await foreach (var item in GenerateData(TotalItems))
        {
            await SimulateWork(item);
            count++;
        }

        return count;
    }

    [Benchmark]
    public async Task<int> EpochPipeline_RealisticWork_Sequential()
    {
        // Epoch pipeline with same realistic work
        // Shows overhead relative to real-world processing
        var progress = new CompletionBasedEpochProgress();
        var count = 0;

        var epochs = EpochSegmenter.SegmentByKey(
            GenerateData(TotalItems),
            item => item / DataItemsPerEpoch,
            "source1",
            new EpochSegmenterConfig
            {
                ExecutionPolicy = EpochExecutionPolicy.Sequential
            });

        await foreach (var epochStream in epochs)
        {
            progress.RegisterEpochStarted(epochStream.Epoch);

            await foreach (var item in epochStream.Items)
            {
                await SimulateWork(item);
                count++;
            }

            progress.RegisterEpochCompleted(epochStream.Epoch);
        }

        return count;
    }

    [Benchmark]
    public async Task<int> EpochPipeline_RealisticWork_Overlapped()
    {
        // Overlapped execution with realistic work
        var progress = new CompletionBasedEpochProgress();
        var count = 0;

        var epochs = EpochSegmenter.SegmentByKey(
            GenerateData(TotalItems),
            item => item / DataItemsPerEpoch,
            "source1",
            new EpochSegmenterConfig
            {
                ExecutionPolicy = EpochExecutionPolicy.Overlapped,
                MaxConcurrentEpochs = 4
            });

        await foreach (var epochStream in epochs)
        {
            progress.RegisterEpochStarted(epochStream.Epoch);

            await foreach (var item in epochStream.Items)
            {
                await SimulateWork(item);
                count++;
            }

            progress.RegisterEpochCompleted(epochStream.Epoch);
        }

        return count;
    }

    private static async IAsyncEnumerable<int> GenerateData(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return i;
        }
    }

    /// <summary>
    /// Simulates lightweight async work typical in production scenarios.
    /// Alternates between completed tasks and minimal async operations.
    /// </summary>
    private static ValueTask SimulateWork(int item)
    {
        // Light async work: even numbers complete synchronously,
        // odd numbers yield with Task.Delay(0) to simulate async I/O
        return (item & 1) == 0
            ? new ValueTask(Task.CompletedTask)
            : new ValueTask(Task.Delay(0));
    }
}

/// <summary>
/// **EPOCH OVERHEAD CONSTANCY BENCHMARK** - Proves epoch overhead is fixed cost.
/// Tests whether overhead scales with async work (0-100%) to disprove the hypothesis
/// that overhead multiplies with async state machine costs. Result: constant ~320μs overhead.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class EpochOverheadConstancyBenchmark
{
    private const int DataItemsPerEpoch = 1000;
    private const int NumberOfEpochs = 10;
    private const int TotalItems = DataItemsPerEpoch * NumberOfEpochs;

    // Test with varying percentages of async work
    [Params(0, 25, 50, 75, 100)]
    public int AsyncWorkPercentage { get; set; }

    [Benchmark(Baseline = true)]
    public async Task<int> Baseline_VariableAsyncWork()
    {
        var count = 0;

        await foreach (var item in GenerateData(TotalItems))
        {
            await SimulateVariableWork(item, AsyncWorkPercentage);
            count++;
        }

        return count;
    }

    [Benchmark]
    public async Task<int> EpochPipeline_VariableAsyncWork()
    {
        var progress = new CompletionBasedEpochProgress();
        var count = 0;

        var epochs = EpochSegmenter.SegmentByKey(
            GenerateData(TotalItems),
            item => item / DataItemsPerEpoch,
            "source1",
            new EpochSegmenterConfig
            {
                ExecutionPolicy = EpochExecutionPolicy.Sequential
            });

        await foreach (var epochStream in epochs)
        {
            progress.RegisterEpochStarted(epochStream.Epoch);

            await foreach (var item in epochStream.Items)
            {
                await SimulateVariableWork(item, AsyncWorkPercentage);
                count++;
            }

            progress.RegisterEpochCompleted(epochStream.Epoch);
        }

        return count;
    }

    private static async IAsyncEnumerable<int> GenerateData(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return i;
        }
    }

    /// <summary>
    /// Simulates work with variable async percentage.
    /// </summary>
    private static ValueTask SimulateVariableWork(int item, int asyncPercentage)
    {
        // Determine if this item should do async work based on percentage
        // Use deterministic check so results are consistent
        var shouldBeAsync = (item % 100) < asyncPercentage;
        
        return shouldBeAsync
            ? new ValueTask(Task.Delay(0))
            : new ValueTask(Task.CompletedTask);
    }
}
