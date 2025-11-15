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
}
else if (args.Length > 0 && args[0] == "control-signal-strategies")
{
    // Run comprehensive control signal strategy comparison benchmarks
    //BenchmarkRunner.Run<DataFlow.POC.Benchmarks.ControlSignalStrategyComparison>(); // Disabled - needs refactoring
    Console.WriteLine("ControlSignalStrategyComparison is currently disabled - needs refactoring");
}
else if (args.Length > 0 && args[0] == "control-signal-routing")
{
    // Run microbenchmarks for control signal routing overhead
    //BenchmarkRunner.Run<DataFlow.POC.Benchmarks.ControlSignalRoutingMicrobenchmark>(); // Disabled - needs refactoring
    Console.WriteLine("ControlSignalRoutingMicrobenchmark is currently disabled - needs refactoring");
}
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
else if (args.Length > 0 && args[0] == "epoch-alignment")
{
    // Run Phase 3 epoch alignment benchmarks
    //BenchmarkRunner.Run<DataFlow.POC.Benchmarks.EpochAlignmentBenchmark>(); // Disabled - needs refactoring
    Console.WriteLine("EpochAlignmentBenchmark is currently disabled - needs refactoring");
}
else if (args.Length > 0 && args[0] == "phase4" || args.Length > 0 && args[0] == "epoch-synthetic")
{
    // Run synthetic baseline benchmark (pure stream vs epoch infrastructure)
    // Measures raw framework overhead without I/O or realistic workload
    BenchmarkRunner.Run<DataFlow.POC.Benchmarks.EpochSyntheticBaselineBenchmark>();
}
else if (args.Length > 0 && args[0] == "phase4-streaming")
{
    // Run Phase 4 streaming segmentation micro-benchmarks
    BenchmarkRunner.Run<DataFlow.POC.Benchmarks.StreamingSegmentationMicrobenchmark>();
}
else if (args.Length > 0 && args[0] == "phase4-regression")
{
    // Run Phase 4 non-epoch regression benchmarks
    BenchmarkRunner.Run<DataFlow.POC.Benchmarks.NonEpochRegressionBenchmark>();
}
else if (args.Length > 0 && args[0] == "phase4-realistic")
{
    // Run Phase 4 realistic workload benchmarks (nested in Phase4PerformanceBenchmarks)
    BenchmarkRunner.Run<DataFlow.POC.Benchmarks.RealisticWorkloadBenchmark>();
}
else if (args.Length > 0 && args[0] == "epoch-scaling")
{
    // Run epoch async workload scaling to prove overhead constancy
    BenchmarkRunner.Run<DataFlow.POC.Benchmarks.EpochOverheadConstancyBenchmark>();
}
else if (args.Length > 0 && args[0] == "epoch-async-overhead")
{
    // Run epoch async overhead investigation (Task vs ValueTask)
    BenchmarkRunner.Run<DataFlow.POC.Benchmarks.EpochAsyncOverheadBenchmark>();
}
else if (args.Length > 0 && args[0] == "epoch-granularity")
{
    // Run epoch granularity scaling benchmarks
    BenchmarkRunner.Run<DataFlow.POC.Benchmarks.EpochGranularityScalingBenchmark>();
}
else if (args.Length > 0 && args[0] == "epoch-production-io")
{
    // **KEY EPOCH BENCHMARK** - Production I/O context showing 1-2% overhead
    // This benchmark validates the ≤5% overhead goal in realistic scenarios
    BenchmarkRunner.Run<DataFlow.POC.Benchmarks.EpochProductionIOBenchmark>();
}
else if (args.Length > 0 && args[0] == "decoupled-epoch")
{
    // Run decoupled epoch segmentation benchmarks
    // Compares source-centric vs decoupled approach performance
    BenchmarkRunner.Run<DataFlow.POC.Benchmarks.DecoupledEpochBenchmark>();
}
else if (args.Length > 0 && args[0] == "tracking-block")
{
    // Run epoch tracking block benchmarks
    DataFlow.POC.Benchmarks.RunTrackingBlockBenchmarks.Run();
}
else if (args.Length > 0 && args[0] == "source-coordination")
{
    // Run source coordination performance benchmarks (Phase 5)
    BenchmarkRunner.Run<DataFlow.POC.Benchmarks.SourceCoordinationBenchmark>();
}
else if (args.Length > 0 && args[0] == "epoch-coordinator-contention")
{
    // Run epoch coordinator lock contention and memory benchmarks (Phase 5)
    BenchmarkRunner.Run<DataFlow.POC.Benchmarks.EpochCoordinatorContentionBenchmark>();
}
else if (args.Length > 0 && args[0] == "plain-blocks-baseline")
{
    // ARCHIVED: Plain blocks baseline benchmarks have been moved to research/flow-composability-unification/archived-benchmarks/
    Console.WriteLine("Plain blocks baseline benchmarks have been archived.");
    Console.WriteLine("See: research/flow-composability-unification/archived-benchmarks/");
}
else if (args.Length > 0 && args[0] == "actor-block-validation")
{
    // ARCHIVED: ActorBlock performance validation benchmarks have been moved to research/flow-composability-unification/archived-benchmarks/
    Console.WriteLine("ActorBlock performance validation benchmarks have been archived.");
    Console.WriteLine("See: research/flow-composability-unification/archived-benchmarks/");
}
else
{
    // Run simple performance tests
    await DataFlow.POC.Benchmarks.PerformanceTests.Main(args);
}
