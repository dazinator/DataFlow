namespace DataFlow.Blazor.Models;

using DataFlow.Blazor.Events;

/// <summary>
/// Represents the runtime state of a DataFlow execution.
/// </summary>
public class FlowExecutionState
{
    public Guid InvocationId { get; set; }
    public string FlowName { get; set; } = string.Empty;
    /// <summary>Optional friendly display name configured via <c>df.DisplayName("…")</c>. Null when not set.</summary>
    public string? FlowDisplayName { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public FlowState State { get; set; } = FlowState.NotStarted;
    public string? ErrorMessage { get; set; }
    public string? TriggerParamsJson { get; set; }
    
    public Dictionary<string, BlockState> Blocks { get; } = new();
    public Dictionary<(string Source, string Target), ChannelState> Channels { get; } = new();
    public Dictionary<(string Source, string Target), EdgeState> Edges { get; } = new();
    public List<IDataFlowEvent> EventLog { get; } = new();
    /// <summary>Block names in topological order (source → sink). Populated from FlowGraphDefinedEvent.</summary>
    public List<string> BlockOrder { get; } = new();

    /// <summary>
    /// Gets the total duration of the flow execution.
    /// </summary>
    public TimeSpan? Duration
    {
        get
        {
            if (StartTime == null) return null;
            var endTime = EndTime ?? DateTime.UtcNow;
            return endTime - StartTime.Value;
        }
    }

    /// <summary>
    /// Gets the total number of items ingested by source blocks (blocks with no incoming edges).
    /// This is the correct flow-level total — summing all blocks double-counts items that pass
    /// through multiple stages.
    /// </summary>
    public long TotalSourceItemsIngested => Blocks.Values.Where(b => b.IsSource).Sum(b => b.ItemsProduced);
}

/// <summary>
/// Represents the runtime state of a block.
/// </summary>
public class BlockState
{
    public string BlockName { get; set; } = string.Empty;
    public string BlockType { get; set; } = string.Empty;
    /// <summary>Optional friendly display name configured via block metadata. Null when not set.</summary>
    public string? DisplayName { get; set; }
    public Events.BlockState State { get; set; } = Events.BlockState.Idle;
    public bool IsSource { get; set; }
    /// <summary>Human-readable label for the block's input item type. Null for source blocks.</summary>
    public string? InputItemLabel { get; set; }
    /// <summary>Human-readable label for the block's output item type. Null for sink blocks.</summary>
    public string? OutputItemLabel { get; set; }
    /// <summary>Items pulled from this block's input channel(s). 0 for source blocks.</summary>
    public long ItemsConsumed { get; set; }
    /// <summary>Items written to this block's output channel(s). 0 for pure sink blocks.</summary>
    public long ItemsProduced { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Current production throughput in items/second. Reset to 0 when the block completes.</summary>
    public double ProductionRatePerSecond { get; set; }
    /// <summary>Current consumption rate in items/second. Reset to 0 when the block completes.</summary>
    public double InputRatePerSecond { get; set; }

    // Internals used by EventProcessor to compute rates — not for external consumers.
    internal long PreviousItemsProducedForRate { get; set; }
    internal long PreviousItemsConsumedForRate { get; set; }
    internal DateTime? LastProgressTimestamp { get; set; }

    /// <summary>
    /// Gets the duration of the block execution.
    /// </summary>
    public TimeSpan? Duration
    {
        get
        {
            if (StartTime == null) return null;
            var endTime = EndTime ?? DateTime.UtcNow;
            return endTime - StartTime.Value;
        }
    }

    /// <summary>
    /// Total items handled: consumed for non-source blocks, output for source blocks.
    /// Useful for overall throughput calculations.
    /// </summary>
    public long TotalItemsHandled => ItemsConsumed > 0 ? ItemsConsumed : ItemsProduced;

    /// <summary>
    /// Gets the throughput (items/second) based on <see cref="TotalItemsHandled"/>.
    /// </summary>
    public double? ItemsPerSecond
    {
        get
        {
            var duration = Duration;
            if (duration == null || duration.Value.TotalSeconds == 0) return null;
            return TotalItemsHandled / duration.Value.TotalSeconds;
        }
    }
}

/// <summary>
/// Represents the live buffer state of a single edge channel.
/// </summary>
public class ChannelState
{
    public string SourceBlock { get; set; } = string.Empty;
    public string TargetBlock { get; set; } = string.Empty;
    public int BufferCapacity { get; set; }
    public int CurrentCount { get; set; }
    public int MaxCount { get; set; }
    public int MinCount { get; set; } = int.MaxValue;
    public DateTime LastUpdate { get; set; }

    /// <summary>
    /// True when this edge uses competing-consumer semantics: all target blocks
    /// that share this edge compete for items from a single shared channel, so each
    /// item is consumed by exactly one target (not broadcast to all).
    /// </summary>
    public bool IsCompeting { get; set; }

    /// <summary>
    /// Gets the buffer utilization as a percentage (0–100).
    /// Returns 0 for unbounded channels (Capacity == 0).
    /// </summary>
    public double BufferUtilizationPercent
    {
        get
        {
            if (BufferCapacity <= 0) return 0;
            return (double)CurrentCount / BufferCapacity * 100;
        }
    }

    /// <summary>Health tier: 0 = ok (&lt;60%), 1 = warn (60–85%), 2 = critical (&gt;85%).</summary>
    public int HealthTier => BufferCapacity <= 0 ? 0 : BufferUtilizationPercent switch
    {
        >= 85 => 2,
        >= 60 => 1,
        _ => 0
    };
}

/// <summary>
/// Represents the runtime throughput state of a single directed edge.
/// </summary>
public class EdgeState
{
    public string SourceBlock { get; set; } = string.Empty;
    public string TargetBlock { get; set; } = string.Empty;
    public long ItemsTransmitted { get; set; }
    public double TransmitRatePerSecond { get; set; }
    public double MaxTransmitRatePerSecond { get; set; }
    public double MinTransmitRatePerSecond { get; set; } = double.MaxValue;
    public double AverageTransmitRatePerSecond => _rateSampleCount > 0 ? _rateSampleSum / _rateSampleCount : 0;
    internal double _rateSampleSum;
    internal int _rateSampleCount;
    internal long PreviousItemsForRate { get; set; }
    internal DateTime? LastProgressTimestamp { get; set; }
}
