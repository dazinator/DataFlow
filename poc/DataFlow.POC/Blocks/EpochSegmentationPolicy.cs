namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;

/// <summary>
/// Configuration for epoch segmentation policies.
/// Defines how continuous data streams should be segmented into epochs.
/// </summary>
public sealed class EpochSegmentationPolicy
{
    /// <summary>
    /// No segmentation - wraps entire input in a single epoch.
    /// Useful for pipelines that need epoch infrastructure but don't need segmentation.
    /// </summary>
    public static readonly EpochSegmentationPolicy None = new() { Mode = SegmentationMode.None };
    
    /// <summary>
    /// The segmentation mode.
    /// </summary>
    public SegmentationMode Mode { get; init; }
    
    /// <summary>
    /// For Count mode: number of items per epoch.
    /// </summary>
    public int? ItemsPerEpoch { get; init; }
    
    /// <summary>
    /// For Time mode: time window per epoch.
    /// </summary>
    public TimeSpan? TimeWindow { get; init; }
    
    /// <summary>
    /// For Key mode: key selector function.
    /// </summary>
    public Delegate? KeySelector { get; init; }
    
    /// <summary>
    /// For Clock mode: epoch clock.
    /// </summary>
    public IEpochClock? Clock { get; init; }
    
    /// <summary>
    /// For Custom mode: custom segmentation function.
    /// </summary>
    public Delegate? CustomSegmenter { get; init; }
    
    /// <summary>
    /// Source identifier for epoch vectors.
    /// </summary>
    public string SourceId { get; init; } = "default-source";
    
    /// <summary>
    /// Execution policy for epoch streams.
    /// </summary>
    public EpochExecutionPolicy ExecutionPolicy { get; init; } = EpochExecutionPolicy.Sequential;
    
    /// <summary>
    /// Maximum concurrent epochs (for Overlapped policy).
    /// </summary>
    public int MaxConcurrentEpochs { get; init; } = 4;

    /// <summary>
    /// Creates a count-based segmentation policy.
    /// </summary>
    public static EpochSegmentationPolicy ByCount(int itemsPerEpoch, string sourceId = "default-source")
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(itemsPerEpoch, 1);
        ArgumentNullException.ThrowIfNull(sourceId);
        
        return new EpochSegmentationPolicy
        {
            Mode = SegmentationMode.Count,
            ItemsPerEpoch = itemsPerEpoch,
            SourceId = sourceId
        };
    }
    
    /// <summary>
    /// Creates a key-based segmentation policy.
    /// </summary>
    public static EpochSegmentationPolicy ByKey<T, TKey>(
        Func<T, TKey> keySelector,
        string sourceId = "default-source")
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(sourceId);
        
        return new EpochSegmentationPolicy
        {
            Mode = SegmentationMode.Key,
            KeySelector = keySelector,
            SourceId = sourceId
        };
    }
    
    /// <summary>
    /// Creates a clock-based segmentation policy.
    /// </summary>
    public static EpochSegmentationPolicy ByClock(
        IEpochClock clock,
        string sourceId = "default-source")
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(sourceId);
        
        return new EpochSegmentationPolicy
        {
            Mode = SegmentationMode.Clock,
            Clock = clock,
            SourceId = sourceId
        };
    }
    
    /// <summary>
    /// Creates a custom segmentation policy.
    /// </summary>
    public static EpochSegmentationPolicy Custom<T>(
        Func<IAsyncEnumerable<T>, IAsyncEnumerable<IEpochStream<T>>> customSegmenter,
        string sourceId = "default-source")
    {
        ArgumentNullException.ThrowIfNull(customSegmenter);
        ArgumentNullException.ThrowIfNull(sourceId);
        
        return new EpochSegmentationPolicy
        {
            Mode = SegmentationMode.Custom,
            CustomSegmenter = customSegmenter,
            SourceId = sourceId
        };
    }
}

/// <summary>
/// Segmentation modes supported by EpochSegmenterBlock.
/// </summary>
public enum SegmentationMode
{
    /// <summary>
    /// No segmentation - wrap entire input in single epoch.
    /// </summary>
    None,
    
    /// <summary>
    /// Segment by item count.
    /// </summary>
    Count,
    
    /// <summary>
    /// Segment by time window.
    /// </summary>
    Time,
    
    /// <summary>
    /// Segment by key selector.
    /// </summary>
    Key,
    
    /// <summary>
    /// Segment by epoch clock.
    /// </summary>
    Clock,
    
    /// <summary>
    /// Custom segmentation logic.
    /// </summary>
    Custom
}
