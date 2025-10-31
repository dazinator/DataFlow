// Check command line arguments
if (args.Length > 0 && args[0] == "comparison")
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
else
{
    // Run simple performance tests
    await DataFlow.POC.Benchmarks.PerformanceTests.Main(args);
}
