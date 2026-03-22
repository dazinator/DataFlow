namespace DataFlow.Blazor.Models;

using DataFlow.Blazor.Events;

/// <summary>
/// Represents the runtime state of a DataFlow execution.
/// </summary>
public class FlowExecutionState
{
    public Guid InvocationId { get; set; }
    public string FlowName { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public FlowState State { get; set; } = FlowState.NotStarted;
    public string? ErrorMessage { get; set; }
    public string? TriggerParamsJson { get; set; }
    
    public Dictionary<string, BlockState> Blocks { get; } = new();
    public Dictionary<string, ChannelState> Channels { get; } = new();
    public Dictionary<(string Source, string Target), EdgeState> Edges { get; } = new();
    public List<IDataFlowEvent> EventLog { get; } = new();

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
    public long TotalSourceItemsIngested => Blocks.Values.Where(b => b.IsSource).Sum(b => b.ItemsProcessed);
}

/// <summary>
/// Represents the runtime state of a block.
/// </summary>
public class BlockState
{
    public string BlockName { get; set; } = string.Empty;
    public string BlockType { get; set; } = string.Empty;
    public Events.BlockState State { get; set; } = Events.BlockState.Idle;
    public bool IsSource { get; set; }
    public long ItemsProcessed { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Current output throughput in items/second, derived from consecutive 500 ms BlockProgressEvent ticks.
    /// Reset to 0 when the block completes.
    /// </summary>
    public double OutputRatePerSecond { get; set; }

    // Internals used by EventProcessor to compute the rate — not for external consumers.
    internal long PreviousItemsForRate { get; set; }
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
    /// Gets the throughput (items/second) for this block.
    /// </summary>
    public double? ItemsPerSecond
    {
        get
        {
            var duration = Duration;
            if (duration == null || duration.Value.TotalSeconds == 0) return null;
            return ItemsProcessed / duration.Value.TotalSeconds;
        }
    }
}

/// <summary>
/// Represents the runtime state of a channel.
/// </summary>
public class ChannelState
{
    public string BlockName { get; set; } = string.Empty;
    public int BufferCapacity { get; set; }
    public int CurrentCount { get; set; }
    public DateTime LastUpdate { get; set; }

    /// <summary>
    /// Gets the buffer utilization as a percentage (0-100).
    /// </summary>
    public double BufferUtilizationPercent
    {
        get
        {
            if (BufferCapacity == 0) return 0;
            return (double)CurrentCount / BufferCapacity * 100;
        }
    }

    /// <summary>
    /// Indicates if the buffer is approaching full capacity (>80%).
    /// </summary>
    public bool IsNearCapacity => BufferUtilizationPercent > 80;
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
    internal long PreviousItemsForRate { get; set; }
    internal DateTime? LastProgressTimestamp { get; set; }
}
