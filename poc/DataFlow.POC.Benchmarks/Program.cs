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
else
{
    // Run simple performance tests
    await DataFlow.POC.Benchmarks.PerformanceTests.Main(args);
}
