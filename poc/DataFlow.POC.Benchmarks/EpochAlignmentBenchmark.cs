namespace DataFlow.POC.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DataFlow.POC.Core;
using System.Threading.Channels;

/// <summary>
/// Benchmarks comparing Phase 2 out-of-band epoch control plane
/// with Phase 3 stream segmentation approach.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class EpochAlignmentBenchmark
{
    private const int DataItemsPerEpoch = 1000;
    private const int NumberOfEpochs = 10;
    private const int TotalItems = DataItemsPerEpoch * NumberOfEpochs;

    [Benchmark(Baseline = true)]
    public async Task<int> Baseline_PureDataFlow()
    {
        // Pure data processing without any epoch control
        var count = 0;

        await foreach (var item in GenerateData(TotalItems))
        {
            count += ProcessItem(item);
        }

        return count;
    }

    [Benchmark]
    public async Task<int> Phase2_OutOfBandEpochBroadcast()
    {
        // Phase 2 approach: out-of-band epoch events
        var epochManager = new EpochManager();
        var progress = new EpochProgress();
        var count = 0;
        var itemsInCurrentEpoch = 0;

        var subscriber = new BenchmarkEpochSubscriber(progress, "block1");
        epochManager.RegisterSubscriber("block1", subscriber);

        await foreach (var item in GenerateData(TotalItems))
        {
            count += ProcessItem(item);
            itemsInCurrentEpoch++;

            // Emit epoch every DataItemsPerEpoch items
            if (itemsInCurrentEpoch >= DataItemsPerEpoch)
            {
                var epoch = epochManager.CreateEpochMarker("source1");
                await epochManager.BroadcastEpochAsync(epoch, CancellationToken.None);
                itemsInCurrentEpoch = 0;
            }
        }

        return count;
    }

    [Benchmark]
    public async Task<int> Phase3_StreamSegmentation()
    {
        // Phase 3 approach: stream-per-epoch with completion-based alignment
        var progress = new CompletionBasedEpochProgress();
        var count = 0;

        var epochs = EpochSegmenter.SegmentByKey(
            GenerateData(TotalItems),
            item => item / DataItemsPerEpoch,  // Epoch key
            "source1",
            new EpochSegmenterConfig
            {
                BufferCapacity = 1000,
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
    public async Task<int> Phase3_StreamSegmentation_Overlapped()
    {
        // Phase 3 with overlapped execution (simulated)
        var progress = new CompletionBasedEpochProgress();
        var count = 0;

        var epochs = EpochSegmenter.SegmentByKey(
            GenerateData(TotalItems),
            item => item / DataItemsPerEpoch,
            "source1",
            new EpochSegmenterConfig
            {
                BufferCapacity = 1000,
                ExecutionPolicy = EpochExecutionPolicy.Overlapped,
                MaxConcurrentEpochs = 4
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
    public async Task<int> Phase3_GlobalAlignment()
    {
        // Phase 3 with global alignment tracking across multiple blocks
        var alignment = new GlobalEpochAlignment();
        var block1Progress = alignment.GetOrCreateBlockProgress("block1");
        var block2Progress = alignment.GetOrCreateBlockProgress("block2");
        var count = 0;

        var epochs = EpochSegmenter.SegmentByKey(
            GenerateData(TotalItems),
            item => item / DataItemsPerEpoch,
            "source1",
            new EpochSegmenterConfig
            {
                BufferCapacity = 1000,
                ExecutionPolicy = EpochExecutionPolicy.Sequential
            });

        await foreach (var epochStream in epochs)
        {
            block1Progress.RegisterEpochStarted(epochStream.Epoch);
            block2Progress.RegisterEpochStarted(epochStream.Epoch);

            await foreach (var item in epochStream.Items)
            {
                count += ProcessItem(item);
            }

            block1Progress.RegisterEpochCompleted(epochStream.Epoch);
            block2Progress.RegisterEpochCompleted(epochStream.Epoch);

            // Check global alignment
            var watermark = alignment.GetGlobalCompletionWatermark();
        }

        return count;
    }

    private static async IAsyncEnumerable<int> GenerateData(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return i;
        }
    }

    private static int ProcessItem(int item)
    {
        // Simulate minimal processing
        return item % 2 == 0 ? 1 : 0;
    }

    private class BenchmarkEpochSubscriber : IEpochSubscriber
    {
        private readonly EpochProgress _progress;
        private readonly string _name;

        public BenchmarkEpochSubscriber(EpochProgress progress, string name)
        {
            _progress = progress;
            _name = name;
        }

        public ValueTask<bool> NotifyEpochAsync(EpochMarker marker, CancellationToken cancellationToken)
        {
            _progress.UpdateLastSeen(marker.SourceId, marker.Sequence);
            return new ValueTask<bool>(true);
        }

        public EpochProgress GetEpochProgress()
        {
            return _progress;
        }
    }
}

/// <summary>
/// Detailed comparison of alignment correctness between Phase 2 and Phase 3.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class AlignmentCorrectnessBenchmark
{
    private const int SlowProcessingDelayMs = 10;
    private const int ItemsPerEpoch = 100;

    [Benchmark(Baseline = true)]
    public async Task<AlignmentResult> Phase2_PrematureAlignmentIssue()
    {
        // Demonstrates the Phase 2 premature alignment issue
        var epochManager = new EpochManager();
        var processedItems = new List<int>();
        var alignmentTimes = new List<(long epoch, int itemsProcessed)>();

        var subscriber = new SlowProcessingSubscriber(processedItems, alignmentTimes);
        epochManager.RegisterSubscriber("slowBlock", subscriber);

        // Emit data and epochs
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < ItemsPerEpoch; i++)
            {
                await subscriber.ProcessItemAsync(i);
            }
        });

        await Task.Delay(SlowProcessingDelayMs);

        // Broadcast epoch before processing completes
        var marker = new EpochMarker("source1", 1, DateTime.UtcNow);
        await epochManager.BroadcastEpochAsync(marker, CancellationToken.None);

        await Task.Delay(SlowProcessingDelayMs * ItemsPerEpoch);

        return new AlignmentResult
        {
            TotalItems = ItemsPerEpoch,
            ItemsProcessedAtAlignment = alignmentTimes.FirstOrDefault().itemsProcessed,
            PrematureAlignment = alignmentTimes.Any() && 
                alignmentTimes.First().itemsProcessed < ItemsPerEpoch
        };
    }

    [Benchmark]
    public async Task<AlignmentResult> Phase3_CorrectAlignment()
    {
        // Phase 3 correct alignment - only completes after stream drains
        var progress = new CompletionBasedEpochProgress();
        var processedItems = new List<int>();
        var completionTime = 0;

        async IAsyncEnumerable<int> GenerateWithDelay()
        {
            for (int i = 0; i < ItemsPerEpoch; i++)
            {
                await Task.Delay(SlowProcessingDelayMs);
                yield return i;
            }
        }

        var epochs = EpochSegmenter.SegmentByKey(
            GenerateWithDelay(),
            _ => 1,
            "source1");

        await foreach (var epochStream in epochs)
        {
            progress.RegisterEpochStarted(epochStream.Epoch);

            await foreach (var item in epochStream.Items)
            {
                processedItems.Add(item);
            }

            completionTime = processedItems.Count;
            progress.RegisterEpochCompleted(epochStream.Epoch);
        }

        return new AlignmentResult
        {
            TotalItems = ItemsPerEpoch,
            ItemsProcessedAtAlignment = completionTime,
            PrematureAlignment = completionTime < ItemsPerEpoch
        };
    }

    public class AlignmentResult
    {
        public int TotalItems { get; set; }
        public int ItemsProcessedAtAlignment { get; set; }
        public bool PrematureAlignment { get; set; }
    }

    private class SlowProcessingSubscriber : IEpochSubscriber
    {
        private readonly List<int> _processedItems;
        private readonly List<(long epoch, int itemsProcessed)> _alignmentTimes;
        private readonly EpochProgress _progress = new();

        public SlowProcessingSubscriber(
            List<int> processedItems,
            List<(long epoch, int itemsProcessed)> alignmentTimes)
        {
            _processedItems = processedItems;
            _alignmentTimes = alignmentTimes;
        }

        public async Task ProcessItemAsync(int item)
        {
            await Task.Delay(SlowProcessingDelayMs);
            _processedItems.Add(item);
        }

        public ValueTask<bool> NotifyEpochAsync(EpochMarker marker, CancellationToken cancellationToken)
        {
            _progress.UpdateLastSeen(marker.SourceId, marker.Sequence);
            _alignmentTimes.Add((marker.Sequence, _processedItems.Count));
            return new ValueTask<bool>(true);
        }

        public EpochProgress GetEpochProgress() => _progress;
    }
}
