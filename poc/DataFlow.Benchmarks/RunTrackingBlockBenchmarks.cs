using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using DataFlow.POC.Benchmarks;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Registry;

namespace DataFlow.POC.Benchmarks;

public static class RunTrackingBlockBenchmarks
{
    public static void Run()
    {
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        Console.WriteLine("Running Epoch Tracking Block Benchmarks...\n");

        // Run the main tracking block benchmark
        var summary1 = BenchmarkRunner.Run<EpochTrackingBlockBenchmark>(config);

        Console.WriteLine("\n\nRunning Memory Benchmarks...\n");

        // Run memory benchmark
        var summary2 = BenchmarkRunner.Run<TrackingBlockMemoryBenchmark>(config);

        Console.WriteLine("\n\nRunning Commit Latency Benchmarks...\n");

        // Run commit latency benchmark
        var summary3 = BenchmarkRunner.Run<TrackingBlockCommitLatencyBenchmark>(config);

        Console.WriteLine("\n\nAll benchmarks complete!");
    }
}

