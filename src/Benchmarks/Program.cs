namespace Benchmarks;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

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
        Console.WriteLine("\nAvailable commands:");
        Console.WriteLine("  diagnose         - Run diagnostic test with detailed console output");
        Console.WriteLine("  minimal          - Run minimal benchmark");
        Console.WriteLine("  simple           - Run simple pipeline benchmark");
        Console.WriteLine("  batch            - Run BatchBlock comparison benchmark (old vs new implementation)");
        Console.WriteLine("  transform-model  - Run TransformBlock execution model comparison benchmark");
        Console.WriteLine("  transform-memory - Run TransformBlock memory usage benchmark");
        Console.WriteLine("  memory-rate      - Run RateLimitBlock memory usage benchmark");
        Console.WriteLine("  test             - Run super simple test (no benchmarking)");

    }
}

