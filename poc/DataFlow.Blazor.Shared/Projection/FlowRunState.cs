namespace DataFlow.Blazor.Projection;

using System.Collections.Immutable;
using DataFlow.Blazor.Events;

/// <summary>
/// Immutable read model produced by folding DataFlow events.
/// Used server-side for snapshot materialization and shared with the client
/// as the basis for the catch-up HTTP response.
/// </summary>
public record FlowRunState
{
    public Guid FlowRunId { get; init; }
    public string FlowName { get; init; } = string.Empty;
    public FlowState Status { get; init; } = FlowState.NotStarted;
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public string? TriggerParamsJson { get; init; }
    public Guid? CorrelationId { get; init; }
    public int AttemptNumber { get; init; } = 1;
    public ImmutableDictionary<string, BlockRunState> Blocks { get; init; } =
        ImmutableDictionary<string, BlockRunState>.Empty;
    public ImmutableDictionary<string, ChannelRunState> Channels { get; init; } =
        ImmutableDictionary<string, ChannelRunState>.Empty;
    public ImmutableDictionary<string, EdgeRunState> Edges { get; init; } =
        ImmutableDictionary<string, EdgeRunState>.Empty;
    /// <summary>
    /// Block names in the order they appear in the pipeline (source → sink).
    /// Populated from <see cref="FlowGraphDefinedEvent"/>.
    /// </summary>
    public ImmutableList<string> BlockOrder { get; init; } = ImmutableList<string>.Empty;

    public static FlowRunState Empty(Guid flowRunId) => new() { FlowRunId = flowRunId };
}

public record BlockRunState
{
    public string BlockName { get; init; } = string.Empty;
    public string BlockType { get; init; } = string.Empty;
    public BlockState Status { get; init; } = BlockState.Idle;
    /// <summary>Items pulled from this block's input channel(s). 0 for source blocks.</summary>
    public long ItemsConsumed { get; init; }
    /// <summary>Items written to this block's output channel(s). 0 for pure sink blocks.</summary>
    public long ItemsProduced { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsSource { get; init; }
    /// <summary>Human-readable label for the block's input item type. Null for source blocks or when not configured.</summary>
    public string? InputItemLabel { get; init; }
    /// <summary>Human-readable label for the block's output item type. Null for sink blocks or when not configured.</summary>
    public string? OutputItemLabel { get; init; }
    /// <summary>Optional friendly display name configured via block metadata.</summary>
    public string? DisplayName { get; init; }
}

/// <summary>
/// Projection of an edge's runtime statistics, accumulated by folding
/// <see cref="EdgeProgressEvent"/>s via <see cref="FlowStateProjector"/>.
///
/// <para><b>Snapshot-safe fields</b> (<c>public</c>) — serialised into <see cref="EdgeSnapshot"/>
/// and restored on the other side of a snapshot/restore cycle.  These are the values that
/// appear in the UI when viewing a completed flow: peak rate, trough rate, running average,
/// and total items transmitted.</para>
///
/// <para><b>Fold-state fields</b> (<c>internal</c>) — carried forward in memory during event
/// folding so each tick can compute <c>Δitems / Δtime</c>, but intentionally omitted from the
/// snapshot.  On snapshot restore <c>PrevTimestamp</c> is <c>null</c>, which causes
/// <see cref="FlowStateProjector.ApplyEdgeProgress"/> to skip the rate calculation for the
/// first post-snapshot event tick.  This one-sample gap is acceptable; for a completed flow
/// (the common reload case) there are no post-snapshot ticks at all.</para>
/// </summary>
public record EdgeRunState
{
    public string SourceBlock { get; init; } = string.Empty;
    public string TargetBlock { get; init; } = string.Empty;
    public long ItemsTransmitted { get; init; }
    public double MaxRatePerSecond { get; init; }
    public double MinRatePerSecond { get; init; } = double.MaxValue;
    public double RateSampleSum { get; init; }
    public int RateSampleCount { get; init; }
    public double AverageRatePerSecond => RateSampleCount > 0 ? RateSampleSum / RateSampleCount : 0;
    // Fold-state only — not persisted in EdgeSnapshot (see class summary).
    internal long PrevItemsForRate { get; init; }
    internal DateTime? PrevTimestamp { get; init; }
}

public record ChannelRunState
{
    public string SourceBlock { get; init; } = string.Empty;
    public string TargetBlock { get; init; } = string.Empty;
    public int BufferCapacity { get; init; }
    public int CurrentCount { get; init; }
    public int MaxCount { get; init; }
    public int MinCount { get; init; } = int.MaxValue;
    public DateTime LastUpdate { get; init; }
}
