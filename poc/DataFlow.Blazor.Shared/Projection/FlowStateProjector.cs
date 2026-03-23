namespace DataFlow.Blazor.Projection;

using System.Collections.Immutable;
using DataFlow.Blazor.Events;

/// <summary>
/// Pure static projector that folds DataFlow events onto a FlowRunState read model.
/// Runs identically on the ASP.NET Core server (snapshot materialization) and
/// on the Blazor WASM client (applying delta events received via SignalR).
/// </summary>
public static class FlowStateProjector
{
    public static FlowRunState Apply(FlowRunState state, IDataFlowEvent evt) => evt switch
    {
        FlowStartedEvent e => state with
        {
            FlowRunId = e.InvocationId,
            FlowName = e.FlowName,
            Status = FlowState.Running,
            StartedAt = e.Timestamp,
            TriggerParamsJson = e.TriggerParamsJson,
            CorrelationId = e.CorrelationId,
            AttemptNumber = e.AttemptNumber
        },

        FlowCompletedEvent e => state with
        {
            Status = e.Success ? FlowState.Completed : FlowState.Failed,
            CompletedAt = e.Timestamp,
            ErrorMessage = e.ErrorMessage
        },

        FlowGraphDefinedEvent e => state with
        {
            BlockOrder = e.Blocks.Select(b => b.BlockName).ToImmutableList(),
            Blocks = e.Blocks.Aggregate(state.Blocks, (blocks, bd) =>
                blocks.SetItem(bd.BlockName,
                    blocks.TryGetValue(bd.BlockName, out var existing)
                        ? existing with
                        {
                            InputItemLabel  = bd.InputItemLabel,
                            OutputItemLabel = bd.OutputItemLabel,
                            IsSource        = bd.IsSource
                        }
                        : new BlockRunState
                        {
                            BlockName       = bd.BlockName,
                            BlockType       = bd.BlockType,
                            InputItemLabel  = bd.InputItemLabel,
                            OutputItemLabel = bd.OutputItemLabel,
                            IsSource        = bd.IsSource
                        }))
        },

        BlockStartedEvent e => state with
        {
            Blocks = state.Blocks.SetItem(e.BlockName,
                state.Blocks.TryGetValue(e.BlockName, out var existing)
                    ? existing with { Status = BlockState.Running, StartedAt = e.Timestamp, IsSource = e.IsSource }
                    : new BlockRunState
                    {
                        BlockName = e.BlockName,
                        BlockType = e.BlockType,
                        Status = BlockState.Running,
                        StartedAt = e.Timestamp,
                        IsSource = e.IsSource
                    })
        },

        BlockCompletedEvent e when state.Blocks.TryGetValue(e.BlockName, out var block) => state with
        {
            Blocks = state.Blocks.SetItem(e.BlockName, block with
            {
                Status = e.Success ? BlockState.Completed : BlockState.Failed,
                CompletedAt = e.Timestamp,
                ErrorMessage = e.ErrorMessage
            })
        },

        BlockMetricsEvent e when state.Blocks.TryGetValue(e.BlockName, out var block) => state with
        {
            Blocks = state.Blocks.SetItem(e.BlockName, block with
            {
                ItemsConsumed = e.ItemsConsumed,
                ItemsProduced = e.ItemsProduced
            })
        },

        EdgeProgressEvent e => state with
        {
            Edges = state.Edges.SetItem($"{e.SourceBlock}->{e.TargetBlock}",
                ApplyEdgeProgress(
                    state.Edges.TryGetValue($"{e.SourceBlock}->{e.TargetBlock}", out var prevEdge)
                        ? prevEdge
                        : new EdgeRunState { SourceBlock = e.SourceBlock, TargetBlock = e.TargetBlock },
                    e))
        },

        ChannelStatsEvent e => state with
        {
            Channels = state.Channels.SetItem($"{e.SourceBlock}->{e.TargetBlock}", new ChannelRunState
            {
                SourceBlock = e.SourceBlock,
                TargetBlock = e.TargetBlock,
                BufferCapacity = e.BufferCapacity,
                CurrentCount = e.CurrentCount,
                MaxCount = Math.Max(
                    state.Channels.TryGetValue($"{e.SourceBlock}->{e.TargetBlock}", out var prev) ? prev.MaxCount : 0,
                    e.CurrentCount),
                MinCount = Math.Min(
                    state.Channels.TryGetValue($"{e.SourceBlock}->{e.TargetBlock}", out var prev2) ? prev2.MinCount : int.MaxValue,
                    e.CurrentCount),
                LastUpdate = e.Timestamp
            })
        },

        _ => state
    };

    public static FlowRunState Fold(FlowRunState seed, IEnumerable<IDataFlowEvent> events)
        => events.Aggregate(seed, Apply);

    /// <summary>
    /// Folds one <see cref="EdgeProgressEvent"/> into the running <see cref="EdgeRunState"/>.
    ///
    /// Rate is computed as <c>(ΔItems) / (ΔSeconds)</c> between consecutive ticks.
    /// The result updates the peak, trough, and running-average watermarks.
    ///
    /// The first event for an edge (or the first event after a snapshot restore) always
    /// skips the rate calculation because <see cref="EdgeRunState.PrevTimestamp"/> is
    /// <c>null</c> — there is no prior tick to diff against.  This is intentional: adding a
    /// sentinel "rate = 0" sample would artificially drag the average down.
    /// </summary>
    internal static EdgeRunState ApplyEdgeProgress(EdgeRunState prev, EdgeProgressEvent e)
    {
        var next = prev with
        {
            ItemsTransmitted = e.ItemsTransmitted,
            PrevItemsForRate = e.ItemsTransmitted,
            PrevTimestamp = e.Timestamp
        };

        if (prev.PrevTimestamp.HasValue)
        {
            var elapsed = (e.Timestamp - prev.PrevTimestamp.Value).TotalSeconds;
            if (elapsed > 0)
            {
                var rate = (e.ItemsTransmitted - prev.PrevItemsForRate) / elapsed;
                if (rate > 0)
                    next = next with
                    {
                        MaxRatePerSecond = Math.Max(prev.MaxRatePerSecond, rate),
                        MinRatePerSecond = Math.Min(prev.MinRatePerSecond, rate),
                        RateSampleSum = prev.RateSampleSum + rate,
                        RateSampleCount = prev.RateSampleCount + 1
                    };
            }
        }

        return next;
    }

    /// <summary>
    /// Projects a FlowRunState into a FlowSnapshot for storage or transport.
    /// </summary>
    public static FlowSnapshot ToSnapshot(FlowRunState state) => new(
        InvocationId: state.FlowRunId,
        FlowName: state.FlowName,
        StartTime: state.StartedAt ?? DateTime.UtcNow,
        State: state.Status,
        CompletedAt: state.CompletedAt,
        ErrorMessage: state.ErrorMessage,
        TriggerParamsJson: state.TriggerParamsJson,
        CorrelationId: state.CorrelationId,
        AttemptNumber: state.AttemptNumber,
        BlockOrder: state.BlockOrder.Count > 0 ? [.. state.BlockOrder] : null,
        Blocks: state.Blocks.ToDictionary(
            kv => kv.Key,
            kv => new BlockSnapshot(
                kv.Value.BlockName,
                kv.Value.BlockType,
                kv.Value.Status,
                kv.Value.ItemsConsumed,
                kv.Value.ItemsProduced,
                kv.Value.StartedAt,
                kv.Value.CompletedAt,
                kv.Value.ErrorMessage,
                kv.Value.IsSource,
                kv.Value.InputItemLabel,
                kv.Value.OutputItemLabel)),
        Channels: state.Channels.ToDictionary(
            kv => kv.Key,
            kv => new ChannelSnapshot(
                kv.Value.SourceBlock,
                kv.Value.TargetBlock,
                kv.Value.BufferCapacity,
                kv.Value.CurrentCount,
                kv.Value.LastUpdate,
                kv.Value.MaxCount,
                kv.Value.MinCount)),
        Edges: state.Edges.ToDictionary(
            kv => kv.Key,
            kv => new EdgeSnapshot(
                kv.Value.SourceBlock,
                kv.Value.TargetBlock,
                kv.Value.ItemsTransmitted,
                kv.Value.MaxRatePerSecond,
                kv.Value.MinRatePerSecond,
                kv.Value.RateSampleSum,
                kv.Value.RateSampleCount))
    );

    /// <summary>
    /// Reconstructs a FlowRunState from a FlowSnapshot (for client-side deserialization).
    /// </summary>
    public static FlowRunState FromSnapshot(FlowSnapshot snapshot) => new()
    {
        FlowRunId = snapshot.InvocationId,
        FlowName = snapshot.FlowName,
        Status = snapshot.State,
        StartedAt = snapshot.StartTime,
        CompletedAt = snapshot.CompletedAt,
        ErrorMessage = snapshot.ErrorMessage,
        TriggerParamsJson = snapshot.TriggerParamsJson,
        CorrelationId = snapshot.CorrelationId,
        AttemptNumber = snapshot.AttemptNumber,
        BlockOrder = snapshot.BlockOrder is { Length: > 0 }
            ? [.. snapshot.BlockOrder]
            : ImmutableList<string>.Empty,
        Blocks = snapshot.Blocks
            .ToImmutableDictionary(
                kv => kv.Key,
                kv => new BlockRunState
                {
                    BlockName       = kv.Value.BlockName,
                    BlockType       = kv.Value.BlockType,
                    Status          = kv.Value.State,
                    ItemsConsumed   = kv.Value.ItemsConsumed,
                    ItemsProduced   = kv.Value.ItemsProduced,
                    StartedAt       = kv.Value.StartTime,
                    CompletedAt     = kv.Value.EndTime,
                    ErrorMessage    = kv.Value.ErrorMessage,
                    IsSource        = kv.Value.IsSource,
                    InputItemLabel  = kv.Value.InputItemLabel,
                    OutputItemLabel = kv.Value.OutputItemLabel
                }),
        Channels = snapshot.Channels
            .ToImmutableDictionary(
                kv => kv.Key,
                kv => new ChannelRunState
                {
                    SourceBlock = kv.Value.SourceBlock,
                    TargetBlock = kv.Value.TargetBlock,
                    BufferCapacity = kv.Value.BufferCapacity,
                    CurrentCount = kv.Value.CurrentCount,
                    MaxCount = kv.Value.MaxCount,
                    MinCount = kv.Value.MinCount,
                    LastUpdate = kv.Value.LastUpdate
                }),
        Edges = (snapshot.Edges ?? new Dictionary<string, EdgeSnapshot>())
            .ToImmutableDictionary(
                kv => kv.Key,
                kv => new EdgeRunState
                {
                    SourceBlock = kv.Value.SourceBlock,
                    TargetBlock = kv.Value.TargetBlock,
                    ItemsTransmitted = kv.Value.ItemsTransmitted,
                    MaxRatePerSecond = kv.Value.MaxRatePerSecond,
                    MinRatePerSecond = kv.Value.MinRatePerSecond,
                    RateSampleSum = kv.Value.RateSampleSum,
                    RateSampleCount = kv.Value.RateSampleCount
                    // PrevItemsForRate / PrevTimestamp not restored — first post-snapshot
                    // rate sample is skipped, which is acceptable
                })
    };
}
