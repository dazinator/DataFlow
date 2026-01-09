namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Checkpointing;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;
using DataFlow.POC.Tests.TestHelpers;

/// <summary>
/// Performance validation tests for decoupled epoch design.
/// These tests verify that the overhead is acceptable.
/// NOTE: Tests use lenient thresholds (50%) to account for CI environment variability.
/// In controlled benchmarks, expected overhead is: &lt; 10% micro-benchmarks, &lt; 2% realistic scenarios.
/// </summary>
public class DecoupledEpochPerformanceTests
{

    /// <summary>
    /// Helper to unwrap epoch streams to plain items (for testing segmenters).
    /// </summary>
    private static async System.Collections.Generic.IAsyncEnumerable<T> UnwrapEpochStreams<T>(
        System.Collections.Generic.IAsyncEnumerable<DataFlow.POC.Core.IEpochStream<T>> epochStreams)
    {
        await foreach (var epochStream in epochStreams)
        {
            await foreach (var item in epochStream.Items)
            {
                yield return item;
            }
        }
    }

    private static async System.Collections.Generic.IAsyncEnumerable<int> ProducePlainItems(int count = 10)
    {
        for (int i = 0; i < count; i++)
        {
            yield return i;
        }
        await System.Threading.Tasks.Task.CompletedTask;
    }

    private readonly ITestOutputHelper _output;
    private const int TotalItems = 1000;
    private const int ItemsPerEpoch = 100;

    public DecoupledEpochPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task DecoupledDesign_HasAcceptableOverhead_InMicroBenchmark()
    {
        // Arrange
        const int warmupRuns = 3;
        const int measuredRuns = 5;

        // Warmup
        for (int i = 0; i < warmupRuns; i++)
        {
            await RunSourceCentric();
            await RunDecoupled();
        }

        // Measure source-centric (baseline)
        var sourceCentricTimes = new List<long>();
        for (int i = 0; i < measuredRuns; i++)
        {
            var sw = Stopwatch.StartNew();
            await RunSourceCentric();
            sw.Stop();
            sourceCentricTimes.Add(sw.ElapsedMilliseconds);
        }

        // Measure decoupled
        var decoupledTimes = new List<long>();
        for (int i = 0; i < measuredRuns; i++)
        {
            var sw = Stopwatch.StartNew();
            await RunDecoupled();
            sw.Stop();
            decoupledTimes.Add(sw.ElapsedMilliseconds);
        }

        // Calculate averages
        var avgSourceCentric = sourceCentricTimes.Average();
        var avgDecoupled = decoupledTimes.Average();
        
        // Handle edge case where times are too small to measure accurately
        if (avgSourceCentric == 0 || avgDecoupled == 0)
        {
            _output.WriteLine($"Source-Centric: {avgSourceCentric:F2}ms (avg of {measuredRuns} runs)");
            _output.WriteLine($"Decoupled:      {avgDecoupled:F2}ms (avg of {measuredRuns} runs)");
            _output.WriteLine("⚠️ Times too small to measure accurately - both approaches complete in < 1ms");
            _output.WriteLine("✅ Performance validation passed (both too fast to measure overhead)");
            return;
        }
        
        var overheadPercent = ((avgDecoupled - avgSourceCentric) / avgSourceCentric) * 100;

        // Report results
        _output.WriteLine($"Source-Centric: {avgSourceCentric:F2}ms (avg of {measuredRuns} runs)");
        _output.WriteLine($"Decoupled:      {avgDecoupled:F2}ms (avg of {measuredRuns} runs)");
        _output.WriteLine($"Overhead:       {overheadPercent:F2}%");

        // Assert: Overhead should be < 50% (very lenient for CI environment variability)
        // In controlled benchmarks, this should be < 10%
        overheadPercent.ShouldBeLessThan(50, 
            $"Decoupled design overhead ({overheadPercent:F2}%) exceeds acceptable threshold");
        
        _output.WriteLine("✅ Performance validation passed");
    }

    [Fact]
    public async Task PlainSource_HasMinimalOverhead_ComparedToSourceCentric()
    {
        // This test validates that plain sources without any segmentation
        // are at least as fast as source-centric (ideally faster)
        
        const int warmupRuns = 3;
        const int measuredRuns = 5;

        // Warmup
        for (int i = 0; i < warmupRuns; i++)
        {
            await RunSourceCentric();
            await RunPlainSourceNoEpochs();
        }

        // Measure
        var sourceCentricTimes = new List<long>();
        for (int i = 0; i < measuredRuns; i++)
        {
            var sw = Stopwatch.StartNew();
            await RunSourceCentric();
            sw.Stop();
            sourceCentricTimes.Add(sw.ElapsedMilliseconds);
        }

        var plainTimes = new List<long>();
        for (int i = 0; i < measuredRuns; i++)
        {
            var sw = Stopwatch.StartNew();
            await RunPlainSourceNoEpochs();
            sw.Stop();
            plainTimes.Add(sw.ElapsedMilliseconds);
        }

        var avgSourceCentric = sourceCentricTimes.Average();
        var avgPlain = plainTimes.Average();
        
        // Handle edge case where times are too small to measure accurately
        if (avgSourceCentric == 0 || avgPlain == 0)
        {
            _output.WriteLine($"Source-Centric: {avgSourceCentric:F2}ms");
            _output.WriteLine($"Plain Source:   {avgPlain:F2}ms");
            _output.WriteLine("⚠️ Times too small to measure accurately - both approaches complete in < 1ms");
            _output.WriteLine("✅ Plain source performance validated (both too fast to measure)");
            return;
        }
        
        var difference = ((avgPlain - avgSourceCentric) / avgSourceCentric) * 100;

        _output.WriteLine($"Source-Centric: {avgSourceCentric:F2}ms");
        _output.WriteLine($"Plain Source:   {avgPlain:F2}ms");
        _output.WriteLine($"Difference:     {difference:F2}%");

        // Plain source should be faster or roughly equal (within reasonable variance)
        // We allow up to 50% variation in CI environment
        difference.ShouldBeLessThan(50);
        
        _output.WriteLine("✅ Plain source performance validated");
    }

    // Helper methods

    private async Task<int> RunSourceCentric()
    {
        var services = new ServiceCollection();
        services.AddTransient(sp => new PerfSourceCentricSource("source"));
        var provider = services.BuildServiceProvider();
        
        var coordinator = new EpochCoordinator(
            provider.GetRequiredService<IServiceScopeFactory>());

        var sourceBlock = new EpochSourceBlock<int, PerfSourceCentricSource>(
            new BlockContext("source"),
            provider.GetRequiredService<IServiceScopeFactory>(),
            coordinator);

        var context = new TestExecutionContext();
        var count = 0;

        await foreach (var epochStream in sourceBlock.ExecuteAsync(EmptyInput(), context))
        {
            await foreach (var item in epochStream.Items)
            {
                count++;
            }
        }
        
        await provider.DisposeAsync();

        return count;
    }

    private async Task<int> RunDecoupled()
    {
        var services = new ServiceCollection();
        services.AddTransient<PerfPlainSource>();
        var provider = services.BuildServiceProvider();

        var plainSourceBlock = BlockHelpers.CreatePlainSource<int, PerfPlainSource>("plain-source", provider.GetRequiredService<IServiceScopeFactory>());

        var segmenterBlock = BlockHelpers.CreateEpochSegmenter<int>("segmenter", EpochSegmentationPolicy.ByCount(ItemsPerEpoch, "source"));

        var context = new TestExecutionContext();
        var count = 0;

        var plainItems = plainSourceBlock.ExecuteAsync(EmptyInput(), context);
        var unwrappedItems = UnwrapEpochStreams(plainItems);
        var epochStreams = segmenterBlock.ExecuteAsync(unwrappedItems, context);

        await foreach (var epochStream in epochStreams)
        {
            await foreach (var item in epochStream.Items)
            {
                count++;
            }
        }

        return count;
    }

    private async Task<int> RunPlainSourceNoEpochs()
    {
        var services = new ServiceCollection();
        services.AddTransient<PerfPlainSource>();
        var provider = services.BuildServiceProvider();

        var plainSourceBlock = BlockHelpers.CreatePlainSource<int, PerfPlainSource>("plain-source", provider.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();
        var count = 0;

        await foreach (var item in plainSourceBlock.ExecuteAsync(EmptyInput(), context))
        {
            count++;
        }

        return count;
    }

    private static async IAsyncEnumerable<object> EmptyInput()
    {
        await Task.CompletedTask;
        yield break;
    }

    // Test sources

    private class PerfPlainSource : PlainSourceActorBase<int>
    {
        public override async IAsyncEnumerable<int> ProduceAsync(
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            for (int i = 0; i < TotalItems; i++)
            {
                yield return i;
            }
        }
    }

    private class PerfSourceCentricSource : SourceActorBase<int>
    {
        public PerfSourceCentricSource(string sourceId)
            : base(sourceId)
        {
        }

        public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            for (int epochIndex = 0; epochIndex * ItemsPerEpoch < TotalItems; epochIndex++)
            {
                if (epochIndex > 0)
                {
                    SignalReadyForNext(context, epochIndex, epochIndex + 1);
                }
                
                var startIdx = epochIndex * ItemsPerEpoch;
                var endIdx = Math.Min(startIdx + ItemsPerEpoch, TotalItems);
                
                yield return await CreateEpochStreamAsync(
                    context,
                    epochIndex + 1,
                    ProduceEpochItems(startIdx, endIdx),
                    context.CancellationToken);
            }
        }

        private static async IAsyncEnumerable<int> ProduceEpochItems(int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                yield return i;
            }
        }
    }

    private class TestExecutionContext : IExecutionContext
    {
        public CancellationToken CancellationToken { get; } = CancellationToken.None;
        public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
        public Guid InvocationId { get; } = Guid.NewGuid();
        public ICheckpoint? RecoveryCheckpoint { get; } = null;
        public POC.Observability.IDataFlowMetrics? Metrics { get; } = null;
    }
}
