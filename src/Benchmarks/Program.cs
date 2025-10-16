namespace Benchmarks;

using System.Collections.Concurrent;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Benchmarks.Profiling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow;

public partial class Program
{

    public static async Task Main(string[] args)
    {
        Console.WriteLine("DataFlow Benchmarks Runner");
        Console.WriteLine("-------------------------");
        Console.WriteLine($"Arguments: {string.Join(" ", args)}");

        if (args.Length > 0)
        {
            var command = args[0].ToLowerInvariant();
            Console.WriteLine($"Running command: {command}");

            switch (command)
            {
                case "diagnose":
                    Console.WriteLine("Starting diagnostic test...");
                    await DiagnosticTest.RunTest();
                    break;                

                case "minimal":
                    Console.WriteLine("Starting minimal benchmark...");
                    BenchmarkRunner.Run<MinimalBenchmark>();
                    break;

                case "simple":
                    Console.WriteLine("Starting simple pipeline benchmark...");
                    BenchmarkRunner.Run<SimplePipelineBenchmarks>();
                    break;

                case "batch":
                    Console.WriteLine("Starting BatchBlock comparison benchmark...");
                    BenchmarkRunner.Run<BatchBlockBenchmark>();
                    break;

                case "transform-model":
                    Console.WriteLine("Starting TransformBlock execution model benchmark...");
                    BenchmarkRunner.Run<TransformBlockExecutionModelBenchmarks>();
                    break;

                case "transform-memory":
                    Console.WriteLine("Starting TransformBlock memory benchmark...");
                    BenchmarkRunner.Run<TransformBlockMemoryBenchmarks>();
                    break;

                case "memory-rate":
                    Console.WriteLine("Starting simple pipeline benchmark...");
                    var isDebug = true;
                    var config  = isDebug
                        ? new DebugInProcessConfig()                       
                        : DefaultConfig.Instance;

                    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
                        .Run(new[] { "RateLimitBlockMemoryBenchmarks" }, config);

                 //   BenchmarkRunner.Run<RateLimitBlockMemoryBenchmarks>(config);
                    break;

                case "etl-benchmark":
                    Console.WriteLine("Starting complex ETL benchmark with BenchmarkDotNet...");
                    BenchmarkRunner.Run<ComplexEtlBenchmark>();
                    break;

                case "etl-direct":
                    Console.WriteLine("Starting complex ETL direct execution (for external profiling)...");
                    await RunEtlDirectAsync(args);
                    break;

                //case "test":
                //    Console.WriteLine("Running super simple test...");
                //    await SuperSimpleTest.RunTest();
                //    break;

                default:
                    Console.WriteLine($"Unknown command: {command}");
                    ShowHelp();
                    break;
            }
        }
        else
        {
            Console.WriteLine("No command specified.");
            ShowHelp();
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("\nAvailable Benchmark Commands:");
        Console.WriteLine("================================================================================");
        Console.WriteLine("{0,-20} {1,-15} {2}", "Command", "Type", "Description");
        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.WriteLine("{0,-20} {1,-15} {2}", "diagnose", "Direct", "Run diagnostic test with detailed console output");
        Console.WriteLine("{0,-20} {1,-15} {2}", "minimal", "BenchmarkDotNet", "Run minimal benchmark");
        Console.WriteLine("{0,-20} {1,-15} {2}", "simple", "BenchmarkDotNet", "Run simple pipeline benchmark");
        Console.WriteLine("{0,-20} {1,-15} {2}", "batch", "BenchmarkDotNet", "BatchBlock comparison (old vs new)");
        Console.WriteLine("{0,-20} {1,-15} {2}", "transform-model", "BenchmarkDotNet", "TransformBlock execution model comparison");
        Console.WriteLine("{0,-20} {1,-15} {2}", "transform-memory", "BenchmarkDotNet", "TransformBlock memory usage");
        Console.WriteLine("{0,-20} {1,-15} {2}", "memory-rate", "BenchmarkDotNet", "RateLimitBlock memory usage");
        Console.WriteLine("{0,-20} {1,-15} {2}", "etl-benchmark", "BenchmarkDotNet", "Complex ETL pipeline benchmark");
        Console.WriteLine("{0,-20} {1,-15} {2}", "etl-direct", "Direct", "Complex ETL (for dotnet-trace/counters)");
        Console.WriteLine("================================================================================");
        Console.WriteLine();
        Console.WriteLine("Direct Execution for External Profiling:");
        Console.WriteLine("  etl-direct [recordCount] [iterations]");
        Console.WriteLine("  Examples:");
        Console.WriteLine("    dotnet run -c Release -- etl-direct 100000 3");
        Console.WriteLine("    dotnet-trace collect --process-id <PID> & dotnet run -c Release -- etl-direct 100000 1");
        Console.WriteLine();
        Console.WriteLine("For OpenTelemetry metrics export:");
        Console.WriteLine("  OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run -c Release -- etl-direct 50000 1");
        Console.WriteLine();
        Console.WriteLine("For detailed profiling guidance, see:");
        Console.WriteLine("  src/Benchmarks/Profiling/README.md");

    }

    private static async Task RunEtlDirectAsync(string[] args)
    {
        // Parse arguments: etl-direct [recordCount] [iterations]
        var recordCount = args.Length > 1 && int.TryParse(args[1], out var rc) ? rc : 50000;
        var iterations = args.Length > 2 && int.TryParse(args[2], out var it) ? it : 3;

        Console.WriteLine($"Record Count: {recordCount:N0}");
        Console.WriteLine($"Iterations: {iterations}");
        Console.WriteLine($"Max Concurrency: 4");
        Console.WriteLine($"Batch Size: 100");
        Console.WriteLine();

        // Setup services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddDataFlowMetrics();
        services.AddDataFlows();
        
        var serviceProvider = services.BuildServiceProvider();

        for (int i = 1; i <= iterations; i++)
        {
            Console.WriteLine($"=== Iteration {i}/{iterations} ===");
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Build and execute the dataflow
            var builder = Shared.ComplexEtlDataFlow.BuildDataFlow(serviceProvider, recordCount);
            var dataflow = builder.Build();
            
            var context = new DataFlowContext
            {
                InvocationId = Guid.NewGuid(),
                CancellationToken = CancellationToken.None,
                ServiceProvider = serviceProvider
            };

            await dataflow.ExecuteAsync(context);
            
            stopwatch.Stop();
            
            Console.WriteLine($"Completed in {stopwatch.ElapsedMilliseconds:N0} ms ({recordCount / stopwatch.Elapsed.TotalSeconds:F1} records/sec)");
            Console.WriteLine();
        }

        Console.WriteLine("All iterations complete.");
    }
}

