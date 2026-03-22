namespace DataFlow.POC.Core;

/// <summary>
/// Provides a live snapshot of a single edge's channel buffer depth.
/// Polled by the progress timer to emit ChannelStatsEvent ticks.
/// </summary>
public interface IBufferMonitor
{
    string SourceBlock { get; }
    string TargetBlock { get; }

    /// <summary>
    /// Maximum items the channel can hold. 0 means unbounded.
    /// </summary>
    int Capacity { get; }

    /// <summary>
    /// Items currently waiting in the channel (not yet consumed by the target block).
    /// Read directly from ChannelReader&lt;T&gt;.Count — no locking, no boxing.
    /// </summary>
    int CurrentCount { get; }
}
