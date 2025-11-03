namespace DataFlow.POC.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DataFlow.POC.Core;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

/// <summary>
/// Benchmarks measuring the overhead of EntityTrackingBlock pattern
/// compared to baseline and epoch-only processing.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class EpochTrackingBlockBenchmark
{
    private const int DataItemsPerEpoch = 1000;
    private const int NumberOfEpochs = 10;
    private const int TotalItems = DataItemsPerEpoch * NumberOfEpochs;

    [Benchmark(Baseline = true)]
    public async Task<int> Baseline_NoEpochs()
    {
        // Pure data processing without any epoch control or tracking
        var count = 0;

        await foreach (var item in GenerateData(TotalItems))
        {
            count += ProcessItem(item);
        }

        return count;
    }

    [Benchmark]
    public async Task<int> WithEpochs_NoTracking()
    {
        // Epoch segmentation only, no tracking block
        var progress = new CompletionBasedEpochProgress();
        var count = 0;

        var epochs = EpochSegmenter.SegmentByKey(
            GenerateData(TotalItems),
            item => item / DataItemsPerEpoch,  // Epoch key
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
                count += ProcessItem(item);
            }

            progress.RegisterEpochCompleted(epochStream.Epoch);
        }

        return count;
    }

    [Benchmark]
    public async Task<int> WithEpochs_AndTrackingBlock()
    {
        // Epoch segmentation with tracking block (per-epoch context management)
        var progress = new CompletionBasedEpochProgress();
        var trackingBlock = new BenchmarkTrackingBlock();
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
            
            // Tracking block processes the epoch stream
            await foreach (var item in trackingBlock.ProcessAsync(epochStream))
            {
                count += ProcessItem(item);
            }

            progress.RegisterEpochCompleted(epochStream.Epoch);
            
            // Simulate global alignment check
            if (progress.IsEpochCompleted(epochStream.Epoch))
            {
                await trackingBlock.CommitEpochAsync(epochStream.Epoch, CancellationToken.None);
            }
        }

        return count;
    }

    [Benchmark]
    public async Task<int> WithEpochs_TrackingBlock_GlobalAlignment()
    {
        // Full lifecycle: epochs + tracking + global alignment coordination
        var alignment = new GlobalEpochAlignment();
        var blockProgress = alignment.GetOrCreateBlockProgress("tracking-block");
        var coordinator = new EpochLifecycleCoordinator();
        var trackingBlock = new BenchmarkTrackingBlockWithLifecycle();
        coordinator.RegisterParticipant(trackingBlock);
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
            blockProgress.RegisterEpochStarted(epochStream.Epoch);
            
            // Notify epoch created
            await coordinator.NotifyEpochCreatedAsync(
                epochStream.Epoch, 
                BlockContext.Create("tracking-block"), 
                CancellationToken.None);

            // Process items through tracking block
            await foreach (var item in trackingBlock.ProcessAsync(epochStream))
            {
                count += ProcessItem(item);
            }

            blockProgress.RegisterEpochCompleted(epochStream.Epoch);
            
            // Notify epoch completed
            await coordinator.NotifyEpochCompletedAsync(
                epochStream.Epoch,
                BlockContext.Create("tracking-block"),
                CancellationToken.None);

            // Check global alignment and notify
            if (alignment.IsGloballyAligned(epochStream.Epoch))
            {
                var watermark = alignment.GetGlobalCompletionWatermark();
                await coordinator.NotifyGlobalEpochAlignedAsync(watermark, CancellationToken.None);
            }
        }

        return count;
    }

    [Benchmark]
    public async Task<int> MultiSink_WithTracking()
    {
        // Multi-sink scenario with different tracking blocks
        var alignment = new GlobalEpochAlignment();
        var block1Progress = alignment.GetOrCreateBlockProgress("tracking-block-1");
        var block2Progress = alignment.GetOrCreateBlockProgress("tracking-block-2");
        var trackingBlock1 = new BenchmarkTrackingBlock();
        var trackingBlock2 = new BenchmarkTrackingBlock();
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
            block1Progress.RegisterEpochStarted(epochStream.Epoch);
            block2Progress.RegisterEpochStarted(epochStream.Epoch);

            // Fork: process through both tracking blocks
            var items = new List<int>();
            await foreach (var item in trackingBlock1.ProcessAsync(epochStream))
            {
                items.Add(item);
            }

            foreach (var item in items)
            {
                count += ProcessItem(item);
            }

            // Process through second tracking block
            await foreach (var item in trackingBlock2.ProcessAsync(
                new BenchmarkEpochStream(epochStream.Epoch, ToAsyncEnumerable(items))))
            {
                count += ProcessItem(item);
            }

            block1Progress.RegisterEpochCompleted(epochStream.Epoch);
            block2Progress.RegisterEpochCompleted(epochStream.Epoch);

            // Commit both tracking blocks on global alignment
            if (alignment.IsGloballyAligned(epochStream.Epoch))
            {
                await trackingBlock1.CommitEpochAsync(epochStream.Epoch, CancellationToken.None);
                await trackingBlock2.CommitEpochAsync(epochStream.Epoch, CancellationToken.None);
            }
        }

        return count;
    }

    private static IAsyncEnumerable<int> GenerateData(int count)
    {
        return GenerateDataInternal();
        
        async IAsyncEnumerable<int> GenerateDataInternal()
        {
            for (int i = 0; i < count; i++)
            {
                yield return i;
            }
        }
    }

    private static IAsyncEnumerable<int> ToAsyncEnumerable(List<int> items)
    {
        return ToAsyncEnumerableInternal();
        
        IAsyncEnumerable<int> ToAsyncEnumerableInternal()
        {
            return ToAsyncEnumerableCore();
            
            async IAsyncEnumerable<int> ToAsyncEnumerableCore()
            {
                foreach (var item in items)
                {
                    yield return item;
                }
                await Task.CompletedTask; // Suppress compiler warning
            }
        }
    }

    private static int ProcessItem(int item)
    {
        // Simulate minimal processing
        return item % 2 == 0 ? 1 : 0;
    }

    /// <summary>
    /// Simulates a tracking block that maintains per-epoch context
    /// (e.g., DbContext, transaction, cache)
    /// </summary>
    private class BenchmarkTrackingBlock
    {
        private readonly ConcurrentDictionary<EpochVector, BenchmarkContext> _epochContexts = new();

        public async IAsyncEnumerable<int> ProcessAsync(IEpochStream<int> epochStream)
        {
            // Get or create context for this epoch
            var ctx = _epochContexts.GetOrAdd(epochStream.Epoch, _ => new BenchmarkContext());

            await foreach (var item in epochStream.Items)
            {
                // Track the item (simulates DbContext.Attach)
                ctx.Track(item);
                yield return item;
            }
        }

        public async ValueTask CommitEpochAsync(EpochVector epoch, CancellationToken ct)
        {
            if (_epochContexts.TryRemove(epoch, out var ctx))
            {
                // Simulate commit (e.g., SaveChangesAsync)
                await ctx.CommitAsync(ct);
            }
        }
    }

    /// <summary>
    /// Tracking block with full lifecycle participation
    /// </summary>
    private class BenchmarkTrackingBlockWithLifecycle : IEpochLifecycleParticipant
    {
        private readonly ConcurrentDictionary<EpochVector, BenchmarkContext> _epochContexts = new();

        public async IAsyncEnumerable<int> ProcessAsync(IEpochStream<int> epochStream)
        {
            var ctx = _epochContexts.GetOrAdd(epochStream.Epoch, _ => new BenchmarkContext());

            await foreach (var item in epochStream.Items)
            {
                ctx.Track(item);
                yield return item;
            }
        }

        public ValueTask OnEpochCreatedAsync(EpochVector epoch, IBlockContext block, CancellationToken ct)
        {
            // Create context when epoch starts
            _epochContexts.GetOrAdd(epoch, _ => new BenchmarkContext());
            return ValueTask.CompletedTask;
        }

        public ValueTask OnEpochCompletedAsync(EpochVector epoch, IBlockContext block, CancellationToken ct)
        {
            // Epoch completed locally, but don't commit yet
            return ValueTask.CompletedTask;
        }

        public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
        {
            // Commit all contexts up to watermark
            var ready = _epochContexts
                .Where(kvp => kvp.Key.IsLessThanOrEqual(watermark))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var epoch in ready)
            {
                if (_epochContexts.TryRemove(epoch, out var ctx))
                {
                    await ctx.CommitAsync(ct);
                }
            }
        }
    }

    /// <summary>
    /// Simulates a per-epoch context (e.g., DbContext)
    /// </summary>
    private class BenchmarkContext
    {
        private readonly List<int> _trackedItems = new();

        public void Track(int item)
        {
            _trackedItems.Add(item);
        }

        public ValueTask CommitAsync(CancellationToken ct)
        {
            // Simulate commit work (e.g., SaveChangesAsync)
            _trackedItems.Clear();
            return ValueTask.CompletedTask;
        }
    }

    private class BenchmarkEpochStream : IEpochStream<int>
    {
        public BenchmarkEpochStream(EpochVector epoch, IAsyncEnumerable<int> items)
        {
            Epoch = epoch;
            Items = items;
        }

        public EpochVector Epoch { get; }
        public IAsyncEnumerable<int> Items { get; }
    }
}

/// <summary>
/// Memory allocation analysis for tracking block pattern
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class TrackingBlockMemoryBenchmark
{
    [Params(10, 100, 1000)]
    public int EpochSize { get; set; }

    [Params(1, 10, 100)]
    public int NumberOfEpochs { get; set; }

    [Benchmark(Baseline = true)]
    public async Task<int> Baseline_NoTracking()
    {
        var count = 0;
        var totalItems = EpochSize * NumberOfEpochs;

        await foreach (var item in GenerateData(totalItems))
        {
            count += item;
        }

        return count;
    }

    [Benchmark]
    public async Task<int> WithTracking_PerEpochContext()
    {
        var trackingBlock = new SimpleTrackingBlock();
        var count = 0;
        var totalItems = EpochSize * NumberOfEpochs;

        var epochs = EpochSegmenter.SegmentByKey(
            GenerateData(totalItems),
            item => item / EpochSize,
            "source1");

        await foreach (var epochStream in epochs)
        {
            await foreach (var item in trackingBlock.ProcessAsync(epochStream))
            {
                count += item;
            }

            await trackingBlock.CommitEpochAsync(epochStream.Epoch, CancellationToken.None);
        }

        return count;
    }

    private static IAsyncEnumerable<int> GenerateData(int count)
    {
        return GenerateDataInternal();
        
        async IAsyncEnumerable<int> GenerateDataInternal()
        {
            for (int i = 0; i < count; i++)
            {
                yield return i;
            }
        }
    }

    private class SimpleTrackingBlock
    {
        private readonly ConcurrentDictionary<EpochVector, List<int>> _epochContexts = new();

        public async IAsyncEnumerable<int> ProcessAsync(IEpochStream<int> epochStream)
        {
            var ctx = _epochContexts.GetOrAdd(epochStream.Epoch, _ => new List<int>());

            await foreach (var item in epochStream.Items)
            {
                ctx.Add(item);
                yield return item;
            }
        }

        public ValueTask CommitEpochAsync(EpochVector epoch, CancellationToken ct)
        {
            _epochContexts.TryRemove(epoch, out _);
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>
/// Transaction commit latency benchmark
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class TrackingBlockCommitLatencyBenchmark
{
    private const int EpochSize = 100;
    private const int NumberOfEpochs = 10;

    [Benchmark]
    public async Task<long> SingleEpoch_ImmediateCommit()
    {
        var trackingBlock = new LatencyTrackingBlock();
        var start = DateTime.UtcNow.Ticks;

        var epochs = EpochSegmenter.SegmentByKey(
            GenerateData(EpochSize),
            _ => 1,
            "source1");

        await foreach (var epochStream in epochs)
        {
            await foreach (var item in trackingBlock.ProcessAsync(epochStream))
            {
                // Process
            }

            await trackingBlock.CommitEpochAsync(epochStream.Epoch, CancellationToken.None);
        }

        return DateTime.UtcNow.Ticks - start;
    }

    [Benchmark]
    public async Task<long> MultipleEpochs_BatchCommit()
    {
        var trackingBlock = new LatencyTrackingBlock();
        var start = DateTime.UtcNow.Ticks;
        var pendingEpochs = new List<EpochVector>();

        var epochs = EpochSegmenter.SegmentByKey(
            GenerateData(EpochSize * NumberOfEpochs),
            item => item / EpochSize,
            "source1");

        await foreach (var epochStream in epochs)
        {
            await foreach (var item in trackingBlock.ProcessAsync(epochStream))
            {
                // Process
            }

            pendingEpochs.Add(epochStream.Epoch);
        }

        // Batch commit all epochs
        foreach (var epoch in pendingEpochs)
        {
            await trackingBlock.CommitEpochAsync(epoch, CancellationToken.None);
        }

        return DateTime.UtcNow.Ticks - start;
    }

    [Benchmark]
    public async Task<long> GlobalAlignment_DeferredCommit()
    {
        var alignment = new GlobalEpochAlignment();
        var blockProgress = alignment.GetOrCreateBlockProgress("tracking-block");
        var trackingBlock = new LatencyTrackingBlock();
        var start = DateTime.UtcNow.Ticks;

        var epochs = EpochSegmenter.SegmentByKey(
            GenerateData(EpochSize * NumberOfEpochs),
            item => item / EpochSize,
            "source1");

        await foreach (var epochStream in epochs)
        {
            blockProgress.RegisterEpochStarted(epochStream.Epoch);

            await foreach (var item in trackingBlock.ProcessAsync(epochStream))
            {
                // Process
            }

            blockProgress.RegisterEpochCompleted(epochStream.Epoch);

            // Only commit when globally aligned
            if (alignment.IsGloballyAligned(epochStream.Epoch))
            {
                var watermark = alignment.GetGlobalCompletionWatermark();
                await trackingBlock.CommitUpToWatermarkAsync(watermark, CancellationToken.None);
            }
        }

        return DateTime.UtcNow.Ticks - start;
    }

    private static IAsyncEnumerable<int> GenerateData(int count)
    {
        return GenerateDataInternal();
        
        async IAsyncEnumerable<int> GenerateDataInternal()
        {
            for (int i = 0; i < count; i++)
            {
                yield return i;
            }
        }
    }

    private class LatencyTrackingBlock
    {
        private readonly ConcurrentDictionary<EpochVector, List<int>> _epochContexts = new();

        public async IAsyncEnumerable<int> ProcessAsync(IEpochStream<int> epochStream)
        {
            var ctx = _epochContexts.GetOrAdd(epochStream.Epoch, _ => new List<int>());

            await foreach (var item in epochStream.Items)
            {
                ctx.Add(item);
                yield return item;
            }
        }

        public ValueTask CommitEpochAsync(EpochVector epoch, CancellationToken ct)
        {
            if (_epochContexts.TryRemove(epoch, out var ctx))
            {
                // Simulate commit latency
                Thread.SpinWait(1000);
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask CommitUpToWatermarkAsync(EpochVector watermark, CancellationToken ct)
        {
            var ready = _epochContexts
                .Where(kvp => kvp.Key.IsLessThanOrEqual(watermark))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var epoch in ready)
            {
                if (_epochContexts.TryRemove(epoch, out _))
                {
                    // Simulate commit latency
                    Thread.SpinWait(1000);
                }
            }

            return ValueTask.CompletedTask;
        }
    }
}
