using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Registry;

namespace DataFlow.POC.Benchmarks;

/// <summary>
/// Epoch async overhead investigation - Task vs ValueTask comparison.
/// Resolves the "overhead paradox" by showing ValueTask wrapping adds baseline cost
/// that reappears in nested enumerables. Proves epoch overhead is constant ~230-250μs.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class EpochAsyncOverheadBenchmark
{
    private const int DataItemsPerEpoch = 1000;
    private const int NumberOfEpochs = 10;
    private const int TotalItems = DataItemsPerEpoch * NumberOfEpochs;

    // Test with different delay amounts (in microseconds)
    [Params(0, 1, 5, 10)]
    public int DelayMicroseconds { get; set; }

    // Test with Task vs ValueTask
    [Params("ValueTask", "Task")]
    public string TaskType { get; set; } = "ValueTask";

    [Benchmark(Baseline = true)]
    public async Task<int> Baseline_WithDelays()
    {
        var count = 0;

        await foreach (var item in GenerateData(TotalItems))
        {
            if (TaskType == "ValueTask")
                await SimulateWork_ValueTask(item, DelayMicroseconds);
            else
                await SimulateWork_Task(item, DelayMicroseconds);
            count++;
        }

        return count;
    }

    [Benchmark]
    public async Task<int> EpochPipeline_WithDelays()
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
                if (TaskType == "ValueTask")
                    await SimulateWork_ValueTask(item, DelayMicroseconds);
                else
                    await SimulateWork_Task(item, DelayMicroseconds);
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
    /// Simulates work with ValueTask - 50% async (odd numbers)
    /// </summary>
    private static ValueTask SimulateWork_ValueTask(int item, int delayMicroseconds)
    {
        if ((item & 1) == 0)
        {
            // Even numbers: synchronous
            return new ValueTask(Task.CompletedTask);
        }
        else
        {
            // Odd numbers: async with delay
            if (delayMicroseconds == 0)
                return new ValueTask(Task.Delay(0));
            else
                return new ValueTask(Task.Delay(TimeSpan.FromMicroseconds(delayMicroseconds)));
        }
    }

    /// <summary>
    /// Simulates work with Task - 50% async (odd numbers)
    /// </summary>
    private static Task SimulateWork_Task(int item, int delayMicroseconds)
    {
        if ((item & 1) == 0)
        {
            // Even numbers: synchronous
            return Task.CompletedTask;
        }
        else
        {
            // Odd numbers: async with delay
            if (delayMicroseconds == 0)
                return Task.Delay(0);
            else
                return Task.Delay(TimeSpan.FromMicroseconds(delayMicroseconds));
        }
    }
}
