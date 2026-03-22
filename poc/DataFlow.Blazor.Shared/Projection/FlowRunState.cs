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

    public static FlowRunState Empty(Guid flowRunId) => new() { FlowRunId = flowRunId };
}

public record BlockRunState
{
    public string BlockName { get; init; } = string.Empty;
    public string BlockType { get; init; } = string.Empty;
    public BlockState Status { get; init; } = BlockState.Idle;
    public long ItemsProcessed { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsSource { get; init; }
}

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
    // Carried forward for rate delta computation during fold — not persisted in snapshot
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
