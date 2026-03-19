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
    public ImmutableDictionary<string, BlockRunState> Blocks { get; init; } =
        ImmutableDictionary<string, BlockRunState>.Empty;
    public ImmutableDictionary<string, ChannelRunState> Channels { get; init; } =
        ImmutableDictionary<string, ChannelRunState>.Empty;

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
}

public record ChannelRunState
{
    public string BlockName { get; init; } = string.Empty;
    public int BufferCapacity { get; init; }
    public int CurrentCount { get; init; }
    public DateTime LastUpdate { get; init; }
}
