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

        BlockStartedEvent e => state with
        {
            Blocks = state.Blocks.SetItem(e.BlockName,
                state.Blocks.TryGetValue(e.BlockName, out var existing)
                    ? existing with { Status = BlockState.Running, StartedAt = e.Timestamp }
                    : new BlockRunState
                    {
                        BlockName = e.BlockName,
                        BlockType = e.BlockType,
                        Status = BlockState.Running,
                        StartedAt = e.Timestamp
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

        BlockProgressEvent e when state.Blocks.TryGetValue(e.BlockName, out var block) => state with
        {
            Blocks = state.Blocks.SetItem(e.BlockName, block with { ItemsProcessed = e.ItemsProcessed })
        },

        ChannelStatsEvent e => state with
        {
            Channels = state.Channels.SetItem(e.BlockName, new ChannelRunState
            {
                BlockName = e.BlockName,
                BufferCapacity = e.BufferCapacity,
                CurrentCount = e.CurrentCount,
                LastUpdate = e.Timestamp
            })
        },

        _ => state
    };

    public static FlowRunState Fold(FlowRunState seed, IEnumerable<IDataFlowEvent> events)
        => events.Aggregate(seed, Apply);

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
        Blocks: state.Blocks.ToDictionary(
            kv => kv.Key,
            kv => new BlockSnapshot(
                kv.Value.BlockName,
                kv.Value.BlockType,
                kv.Value.Status,
                kv.Value.ItemsProcessed,
                kv.Value.StartedAt,
                kv.Value.CompletedAt,
                kv.Value.ErrorMessage)),
        Channels: state.Channels.ToDictionary(
            kv => kv.Key,
            kv => new ChannelSnapshot(
                kv.Value.BlockName,
                kv.Value.BufferCapacity,
                kv.Value.CurrentCount,
                kv.Value.LastUpdate))
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
        Blocks = snapshot.Blocks
            .ToImmutableDictionary(
                kv => kv.Key,
                kv => new BlockRunState
                {
                    BlockName = kv.Value.BlockName,
                    BlockType = kv.Value.BlockType,
                    Status = kv.Value.State,
                    ItemsProcessed = kv.Value.ItemsProcessed,
                    StartedAt = kv.Value.StartTime,
                    CompletedAt = kv.Value.EndTime,
                    ErrorMessage = kv.Value.ErrorMessage
                }),
        Channels = snapshot.Channels
            .ToImmutableDictionary(
                kv => kv.Key,
                kv => new ChannelRunState
                {
                    BlockName = kv.Value.BlockName,
                    BufferCapacity = kv.Value.BufferCapacity,
                    CurrentCount = kv.Value.CurrentCount,
                    LastUpdate = kv.Value.LastUpdate
                })
    };
}
