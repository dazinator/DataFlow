using BenchmarkDotNet.Running;

// Check command line arguments
if (args.Length > 0 && args[0] == "comparative")
{
    // Run comparative benchmark (outputs results compatible with Python benchmarks)
    await DataFlow.POC.Benchmarks.PythonComparativeBenchmark.RunComparativeBenchmarkAsync(args);
}
else if (args.Length > 0 && args[0] == "comparison")
{
    // Run standard comparison benchmark
    await DataFlow.POC.Benchmarks.ComparisonBenchmark.RunComparisonAsync();
}
else if (args.Length > 0 && args[0] == "extended")
{
    // Run extended comparison benchmark with various parameters
    await DataFlow.POC.Benchmarks.ExtendedComparisonBenchmark.RunExtendedComparisonAsync();
}
else if (args.Length > 0 && args[0] == "simple")
{
    // Run simplified comparison benchmark (DataSource → Validators → Enrichers only)
    await DataFlow.POC.Benchmarks.SimpleComparisonBenchmark.RunSimpleComparisonAsync();
}
else if (args.Length > 0 && args[0] == "sidechannel")
{
    // Run side-channel competing edge performance benchmark
    await DataFlow.POC.Benchmarks.SideChannelBenchmark.RunBenchmarkAsync();
}
else if (args.Length > 0 && args[0] == "sidechannel-micro")
{
    // Run BenchmarkDotNet microbenchmarks for side-channel mechanisms
    BenchmarkRunner.Run<DataFlow.POC.Benchmarks.SideChannelMicrobenchmark>();
else if (args.Length > 0 && args[0] == "direct-simple")
{
    // Run direct comparison (simple pipeline) for external profiling with dotnet-counters
    // Usage: direct-simple <recordCount> <maxConcurrency> <iterations>
    int recordCount = args.Length > 1 ? int.Parse(args[1]) : 10000;
    int maxConcurrency = args.Length > 2 ? int.Parse(args[2]) : 4;
    int iterations = args.Length > 3 ? int.Parse(args[3]) : 1;
    
    await DataFlow.POC.Benchmarks.DirectComparisonBenchmark.RunDirectComparisonAsync(
        recordCount, maxConcurrency, batchSize: 100, iterations, useSimplePipeline: true);
}
else if (args.Length > 0 && args[0] == "direct-extended")
{
    // Run direct comparison (complex pipeline) for external profiling with dotnet-counters
    // Usage: direct-extended <recordCount> <maxConcurrency> <batchSize> <iterations>
    int recordCount = args.Length > 1 ? int.Parse(args[1]) : 10000;
    int maxConcurrency = args.Length > 2 ? int.Parse(args[2]) : 4;
    int batchSize = args.Length > 3 ? int.Parse(args[3]) : 100;
    int iterations = args.Length > 4 ? int.Parse(args[4]) : 1;
    
    await DataFlow.POC.Benchmarks.DirectComparisonBenchmark.RunDirectComparisonAsync(
        recordCount, maxConcurrency, batchSize, iterations, useSimplePipeline: false);
}
else if (args.Length > 0 && args[0] == "actor-steady")
{
    // Run ActorBlock steady-state benchmark (no rotation) for external profiling with dotnet-counters
    // Usage: actor-steady <itemCount>
    int itemCount = args.Length > 1 ? int.Parse(args[1]) : 10000;
    
    await DataFlow.POC.Benchmarks.ActorBlockBenchmark.RunSteadyStateAsync(itemCount);
}
else if (args.Length > 0 && args[0] == "actor-rotation")
{
    // Run ActorBlock rotation benchmark for external profiling with dotnet-counters
    // Usage: actor-rotation <itemCount> <rotateAfter>
    int itemCount = args.Length > 1 ? int.Parse(args[1]) : 10000;
    int rotateAfter = args.Length > 2 ? int.Parse(args[2]) : 100;
    
    await DataFlow.POC.Benchmarks.ActorBlockBenchmark.RunRotationAsync(itemCount, rotateAfter);
}
else if (args.Length > 0 && args[0] == "actor-memory")
{
    // Run ActorBlock memory-intensive benchmark for external profiling with dotnet-counters
    // Usage: actor-memory <itemCount> <rotateAfter>
    int itemCount = args.Length > 1 ? int.Parse(args[1]) : 10000;
    int rotateAfter = args.Length > 2 ? int.Parse(args[2]) : 50;
    
    await DataFlow.POC.Benchmarks.ActorBlockBenchmark.RunMemoryIntensiveAsync(itemCount, rotateAfter);
}
else
{
    // Run simple performance tests
    await DataFlow.POC.Benchmarks.PerformanceTests.Main(args);
}
