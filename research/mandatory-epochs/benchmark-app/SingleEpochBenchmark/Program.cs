using System.Diagnostics;
using System.Runtime.CompilerServices;
using DataFlow.POC.Core;

namespace SingleEpochBenchmark;

class Program
{
    private const int ItemCount = 1_000_000; // 1M items
    private const int Iterations = 5;

    static async Task Main(string[] args)
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("Single-Epoch Wrapping Overhead Benchmark");
        Console.WriteLine($"Items: {ItemCount:N0}, Iterations: {Iterations}");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        var plainTimes = new List<double>();
        var singleEpochTimes = new List<double>();

        // Warm-up
        Console.WriteLine("Warming up...");
        await RunPlainStreamTest(ItemCount / 10);
        await RunSingleEpochStreamTest(ItemCount / 10);
        Console.WriteLine();

        // Run benchmarks
        for (int i = 0; i < Iterations; i++)
        {
            Console.WriteLine($"Iteration {i + 1}/{Iterations}");

            // Plain stream
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(100);

            var plainTime = await RunPlainStreamTest(ItemCount);
            plainTimes.Add(plainTime);
            Console.WriteLine($"  Plain stream: {plainTime:F2}ms");

            // Single-epoch stream
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(100);

            var epochTime = await RunSingleEpochStreamTest(ItemCount);
            singleEpochTimes.Add(epochTime);
            Console.WriteLine($"  Single-epoch: {epochTime:F2}ms");

            var overhead = ((epochTime - plainTime) / plainTime) * 100;
            Console.WriteLine($"  Overhead: {overhead:F2}%");
            Console.WriteLine();
        }

        // Calculate statistics
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("Results Summary");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        var plainAvg = Average(plainTimes);
        var plainMin = Min(plainTimes);
        var plainMax = Max(plainTimes);

        var epochAvg = Average(singleEpochTimes);
        var epochMin = Min(singleEpochTimes);
        var epochMax = Max(singleEpochTimes);

        Console.WriteLine($"Plain Stream:");
        Console.WriteLine($"  Average: {plainAvg:F2}ms");
        Console.WriteLine($"  Min: {plainMin:F2}ms");
        Console.WriteLine($"  Max: {plainMax:F2}ms");
        Console.WriteLine();

        Console.WriteLine($"Single-Epoch Stream:");
        Console.WriteLine($"  Average: {epochAvg:F2}ms");
        Console.WriteLine($"  Min: {epochMin:F2}ms");
        Console.WriteLine($"  Max: {epochMax:F2}ms");
        Console.WriteLine();

        var avgOverhead = ((epochAvg - plainAvg) / plainAvg) * 100;
        Console.WriteLine($"Average Overhead: {avgOverhead:F2}%");
        Console.WriteLine();

        // Throughput
        var plainThroughput = (ItemCount / plainAvg) * 1000; // items per second
        var epochThroughput = (ItemCount / epochAvg) * 1000;

        Console.WriteLine($"Throughput:");
        Console.WriteLine($"  Plain: {plainThroughput:N0} items/sec");
        Console.WriteLine($"  Single-Epoch: {epochThroughput:N0} items/sec");
        Console.WriteLine();

        // Conclusion
        if (avgOverhead < 1.0)
        {
            Console.WriteLine("✅ PASS: Overhead < 1% - Negligible performance impact");
        }
        else if (avgOverhead < 5.0)
        {
            Console.WriteLine("⚠️  WARNING: Overhead 1-5% - Acceptable but measurable");
        }
        else
        {
            Console.WriteLine("❌ FAIL: Overhead > 5% - Significant performance impact");
        }
    }

    private static async Task<double> RunPlainStreamTest(int itemCount)
    {
        var sw = Stopwatch.StartNew();

        long sum = 0;
        await foreach (var item in GeneratePlainStream(itemCount))
        {
            sum += item; // Do minimal work to prevent optimization
        }

        sw.Stop();

        // Prevent optimization
        if (sum == 0) throw new InvalidOperationException();

        return sw.Elapsed.TotalMilliseconds;
    }

    private static async Task<double> RunSingleEpochStreamTest(int itemCount)
    {
        var sw = Stopwatch.StartNew();

        long sum = 0;
        await foreach (var epochStream in GenerateSingleEpochStream(itemCount))
        {
            await foreach (var item in epochStream.Items)
            {
                sum += item; // Do minimal work to prevent optimization
            }
        }

        sw.Stop();

        // Prevent optimization
        if (sum == 0) throw new InvalidOperationException();

        return sw.Elapsed.TotalMilliseconds;
    }

    private static async IAsyncEnumerable<int> GeneratePlainStream(
        int count,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int i = 0; i < count; i++)
        {
            yield return i;
            if (i % 10000 == 0)
                await Task.Yield(); // Simulate async operation occasionally
        }
    }

    private static async IAsyncEnumerable<IEpochStream<int>> GenerateSingleEpochStream(
        int count,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var plainStream = GeneratePlainStream(count, ct);
        // Use the extension method from SingleEpochExtensions
        await foreach (var epochStream in plainStream.WrapInSingleEpoch("benchmark-source", ct))
        {
            yield return epochStream;
        }
    }

    // Helper methods
    private static double Average(List<double> values)
    {
        double sum = 0;
        foreach (var value in values)
            sum += value;
        return sum / values.Count;
    }

    private static double Min(List<double> values)
    {
        double min = double.MaxValue;
        foreach (var value in values)
            if (value < min) min = value;
        return min;
    }

    private static double Max(List<double> values)
    {
        double max = double.MinValue;
        foreach (var value in values)
            if (value > max) max = value;
        return max;
    }
}
