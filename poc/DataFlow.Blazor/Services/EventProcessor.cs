namespace DataFlow.Blazor.Services;

using DataFlow.Blazor.Events;
using DataFlow.Blazor.Models;

/// <summary>
/// Processes DataFlow events and maintains the current execution state.
/// </summary>
public class EventProcessor
{
    private readonly FlowExecutionState _state;

    public EventProcessor(Guid invocationId)
    {
        _state = new FlowExecutionState { InvocationId = invocationId };
    }

    public FlowExecutionState State => _state;

    /// <summary>
    /// Applies a snapshot to initialize the state.
    /// </summary>
    public void ApplySnapshot(FlowSnapshot snapshot)
    {
        _state.InvocationId = snapshot.InvocationId;
        _state.FlowName = snapshot.FlowName;
        _state.StartTime = snapshot.StartTime;
        _state.State = snapshot.State;
        _state.TriggerParamsJson = snapshot.TriggerParamsJson;

        foreach (var (blockName, blockSnapshot) in snapshot.Blocks)
        {
            var blockState = new Models.BlockState
            {
                BlockName = blockSnapshot.BlockName,
                BlockType = blockSnapshot.BlockType,
                State = blockSnapshot.State,
                IsSource = blockSnapshot.IsSource,
                ItemsProcessed = blockSnapshot.ItemsProcessed,
                StartTime = blockSnapshot.StartTime,
                EndTime = blockSnapshot.EndTime,
                ErrorMessage = blockSnapshot.ErrorMessage
            };
            _state.Blocks[blockName] = blockState;
        }

        foreach (var (_, channelSnapshot) in snapshot.Channels)
        {
            var channelState = new ChannelState
            {
                SourceBlock = channelSnapshot.SourceBlock,
                TargetBlock = channelSnapshot.TargetBlock,
                BufferCapacity = channelSnapshot.BufferCapacity,
                CurrentCount = channelSnapshot.CurrentCount,
                MaxCount = channelSnapshot.MaxCount,
                MinCount = channelSnapshot.MinCount,
                LastUpdate = channelSnapshot.LastUpdate
            };
            _state.Channels[(channelSnapshot.SourceBlock, channelSnapshot.TargetBlock)] = channelState;
        }
    }

    /// <summary>
    /// Processes an event and updates the state accordingly.
    /// </summary>
    public void ProcessEvent(IDataFlowEvent evt)
    {
        _state.EventLog.Add(evt);

        switch (evt)
        {
            case FlowStartedEvent e:
                ProcessFlowStarted(e);
                break;
            case FlowCompletedEvent e:
                ProcessFlowCompleted(e);
                break;
            case BlockStartedEvent e:
                ProcessBlockStarted(e);
                break;
            case BlockCompletedEvent e:
                ProcessBlockCompleted(e);
                break;
            case BlockProgressEvent e:
                ProcessBlockProgress(e);
                break;
            case ChannelStatsEvent e:
                ProcessChannelStats(e);
                break;
            case EdgeProgressEvent e:
                ProcessEdgeProgress(e);
                break;
        }
    }

    private void ProcessFlowStarted(FlowStartedEvent e)
    {
        _state.InvocationId = e.InvocationId;
        _state.FlowName = e.FlowName;
        _state.StartTime = e.Timestamp;
        _state.State = FlowState.Running;
        _state.TriggerParamsJson = e.TriggerParamsJson;
    }

    private void ProcessFlowCompleted(FlowCompletedEvent e)
    {
        _state.EndTime = e.Timestamp;
        _state.State = e.Success ? FlowState.Completed : FlowState.Failed;
        _state.ErrorMessage = e.ErrorMessage;
    }

    private void ProcessBlockStarted(BlockStartedEvent e)
    {
        if (!_state.Blocks.TryGetValue(e.BlockName, out var blockState))
        {
            blockState = new Models.BlockState
            {
                BlockName = e.BlockName,
                BlockType = e.BlockType
            };
            _state.Blocks[e.BlockName] = blockState;
        }

        blockState.IsSource = e.IsSource;
        blockState.StartTime = e.Timestamp;
        blockState.State = Events.BlockState.Running;
    }

    private void ProcessBlockCompleted(BlockCompletedEvent e)
    {
        if (_state.Blocks.TryGetValue(e.BlockName, out var blockState))
        {
            blockState.EndTime = e.Timestamp;
            blockState.State = e.Success ? Events.BlockState.Completed : Events.BlockState.Failed;
            blockState.ErrorMessage = e.ErrorMessage;
            blockState.OutputRatePerSecond = 0; // block finished — no more output
        }

        // Reset transmit rates for all edges originating from this block.
        foreach (var key in _state.Edges.Keys.Where(k => k.Source == e.BlockName).ToList())
            _state.Edges[key].TransmitRatePerSecond = 0;
    }

    private void ProcessBlockProgress(BlockProgressEvent e)
    {
        if (_state.Blocks.TryGetValue(e.BlockName, out var blockState))
        {
            // Compute output rate from the delta since the previous progress tick.
            if (blockState.LastProgressTimestamp.HasValue)
            {
                var elapsed = (e.Timestamp - blockState.LastProgressTimestamp.Value).TotalSeconds;
                if (elapsed > 0)
                {
                    var delta = e.ItemsProcessed - blockState.PreviousItemsForRate;
                    blockState.OutputRatePerSecond = delta / elapsed;
                }
            }
            blockState.PreviousItemsForRate = e.ItemsProcessed;
            blockState.LastProgressTimestamp = e.Timestamp;
            blockState.ItemsProcessed = e.ItemsProcessed;
        }
    }

    private void ProcessEdgeProgress(EdgeProgressEvent e)
    {
        var key = (e.SourceBlock, e.TargetBlock);
        if (!_state.Edges.TryGetValue(key, out var edgeState))
        {
            edgeState = new EdgeState { SourceBlock = e.SourceBlock, TargetBlock = e.TargetBlock };
            _state.Edges[key] = edgeState;
        }

        if (edgeState.LastProgressTimestamp.HasValue)
        {
            var elapsed = (e.Timestamp - edgeState.LastProgressTimestamp.Value).TotalSeconds;
            if (elapsed > 0)
            {
                var delta = e.ItemsTransmitted - edgeState.PreviousItemsForRate;
                edgeState.TransmitRatePerSecond = delta / elapsed;
            }
        }
        edgeState.PreviousItemsForRate = e.ItemsTransmitted;
        edgeState.LastProgressTimestamp = e.Timestamp;
        edgeState.ItemsTransmitted = e.ItemsTransmitted;
    }

    private void ProcessChannelStats(ChannelStatsEvent e)
    {
        var key = (e.SourceBlock, e.TargetBlock);
        if (!_state.Channels.TryGetValue(key, out var channelState))
        {
            channelState = new ChannelState
            {
                SourceBlock = e.SourceBlock,
                TargetBlock = e.TargetBlock
            };
            _state.Channels[key] = channelState;
        }

        channelState.BufferCapacity = e.BufferCapacity;
        channelState.CurrentCount = e.CurrentCount;
        channelState.MaxCount = Math.Max(channelState.MaxCount, e.CurrentCount);
        if (channelState.MinCount == int.MaxValue)
            channelState.MinCount = e.CurrentCount;
        else
            channelState.MinCount = Math.Min(channelState.MinCount, e.CurrentCount);
        channelState.LastUpdate = e.Timestamp;
    }
}
