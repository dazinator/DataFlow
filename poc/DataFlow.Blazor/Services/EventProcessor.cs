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

        foreach (var (blockName, blockSnapshot) in snapshot.Blocks)
        {
            var blockState = new Models.BlockState
            {
                BlockName = blockSnapshot.BlockName,
                BlockType = blockSnapshot.BlockType,
                State = blockSnapshot.State,
                ItemsProcessed = blockSnapshot.ItemsProcessed,
                StartTime = blockSnapshot.StartTime,
                EndTime = blockSnapshot.EndTime,
                ErrorMessage = blockSnapshot.ErrorMessage
            };
            _state.Blocks[blockName] = blockState;
        }

        foreach (var (blockName, channelSnapshot) in snapshot.Channels)
        {
            var channelState = new ChannelState
            {
                BlockName = channelSnapshot.BlockName,
                BufferCapacity = channelSnapshot.BufferCapacity,
                CurrentCount = channelSnapshot.CurrentCount,
                LastUpdate = channelSnapshot.LastUpdate
            };
            _state.Channels[blockName] = channelState;
        }
    }

    /// <summary>
    /// Processes an event and updates the state accordingly.
    /// </summary>
    public void ProcessEvent(IDataFlowEvent evt)
    {
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
        }
    }

    private void ProcessFlowStarted(FlowStartedEvent e)
    {
        _state.InvocationId = e.InvocationId;
        _state.FlowName = e.FlowName;
        _state.StartTime = e.Timestamp;
        _state.State = FlowState.Running;
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
        }
    }

    private void ProcessBlockProgress(BlockProgressEvent e)
    {
        if (_state.Blocks.TryGetValue(e.BlockName, out var blockState))
        {
            blockState.ItemsProcessed = e.ItemsProcessed;
        }
    }

    private void ProcessChannelStats(ChannelStatsEvent e)
    {
        if (!_state.Channels.TryGetValue(e.BlockName, out var channelState))
        {
            channelState = new ChannelState
            {
                BlockName = e.BlockName
            };
            _state.Channels[e.BlockName] = channelState;
        }

        channelState.BufferCapacity = e.BufferCapacity;
        channelState.CurrentCount = e.CurrentCount;
        channelState.LastUpdate = e.Timestamp;
    }
}
