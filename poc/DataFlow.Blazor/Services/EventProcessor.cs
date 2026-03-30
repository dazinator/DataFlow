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
        _state.EndTime = snapshot.CompletedAt;
        _state.State = snapshot.State;
        _state.TriggerParamsJson = snapshot.TriggerParamsJson;

        if (snapshot.BlockOrder is { Length: > 0 })
        {
            _state.BlockOrder.Clear();
            _state.BlockOrder.AddRange(snapshot.BlockOrder);
        }

        foreach (var (blockName, blockSnapshot) in snapshot.Blocks)
        {
            var blockState = new Models.BlockState
            {
                BlockName = blockSnapshot.BlockName,
                BlockType = blockSnapshot.BlockType,
                DisplayName = blockSnapshot.DisplayName,
                State = blockSnapshot.State,
                IsSource = blockSnapshot.IsSource,
                InputItemLabel = blockSnapshot.InputItemLabel,
                OutputItemLabel = blockSnapshot.OutputItemLabel,
                ItemsConsumed = blockSnapshot.ItemsConsumed,
                ItemsProduced = blockSnapshot.ItemsProduced,
                StartTime = blockSnapshot.StartTime,
                EndTime = blockSnapshot.EndTime,
                ErrorMessage = blockSnapshot.ErrorMessage
            };
            _state.Blocks[blockName] = blockState;
        }

        if (snapshot.Edges is not null)
        {
            foreach (var (_, edgeSnapshot) in snapshot.Edges)
            {
                var edgeState = new EdgeState
                {
                    SourceBlock = edgeSnapshot.SourceBlock,
                    TargetBlock = edgeSnapshot.TargetBlock,
                    ItemsTransmitted = edgeSnapshot.ItemsTransmitted,
                    MaxTransmitRatePerSecond = edgeSnapshot.MaxRatePerSecond,
                    MinTransmitRatePerSecond = edgeSnapshot.MinRatePerSecond
                };
                edgeState._rateSampleSum = edgeSnapshot.RateSampleSum;
                edgeState._rateSampleCount = edgeSnapshot.RateSampleCount;
                _state.Edges[(edgeSnapshot.SourceBlock, edgeSnapshot.TargetBlock)] = edgeState;
            }
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
                LastUpdate = channelSnapshot.LastUpdate,
                IsCompeting = channelSnapshot.IsCompeting
            };
            _state.Channels[(channelSnapshot.SourceBlock, channelSnapshot.TargetBlock)] = channelState;
        }
    }

    /// <summary>
    /// Appends already-processed audit events to EventLog without re-applying state changes.
    /// Use this for structural events that are already encoded in the snapshot — replaying
    /// them through <see cref="ProcessEvent"/> would regress state (e.g. reset a completed
    /// block back to Running when BlockStartedEvent is replayed after the snapshot).
    /// </summary>
    public void ApplyAuditLog(IReadOnlyList<IDataFlowEvent> events)
    {
        foreach (var evt in events)
            _state.EventLog.Add(evt);
    }

    /// <summary>
    /// Processes an event and updates the state accordingly.
    /// </summary>
    public void ProcessEvent(IDataFlowEvent evt)
    {
        // FlowGraphDefinedEvent is structural metadata — not an audit log entry.
        if (evt is not FlowGraphDefinedEvent)
            _state.EventLog.Add(evt);

        switch (evt)
        {
            case FlowStartedEvent e:
                ProcessFlowStarted(e);
                break;
            case FlowCompletedEvent e:
                ProcessFlowCompleted(e);
                break;
            case FlowGraphDefinedEvent e:
                ProcessFlowGraphDefined(e);
                break;
            case BlockStartedEvent e:
                ProcessBlockStarted(e);
                break;
            case BlockCompletedEvent e:
                ProcessBlockCompleted(e);
                break;
            case BlockMetricsEvent e:
                ProcessBlockMetrics(e);
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

    private void ProcessFlowGraphDefined(FlowGraphDefinedEvent e)
    {
        _state.BlockOrder.Clear();
        foreach (var bd in e.Blocks)
        {
            _state.BlockOrder.Add(bd.BlockName);

            if (!_state.Blocks.TryGetValue(bd.BlockName, out var blockState))
            {
                blockState = new Models.BlockState { BlockName = bd.BlockName, BlockType = bd.BlockType };
                _state.Blocks[bd.BlockName] = blockState;
            }
            blockState.DisplayName     = bd.DisplayName;
            blockState.InputItemLabel  = bd.InputItemLabel;
            blockState.OutputItemLabel = bd.OutputItemLabel;
            blockState.IsSource        = bd.IsSource;
        }

        // Pre-seed channel stubs so FlowTopology.Analyze has the full connection graph
        // immediately — before any ChannelStatsEvents arrive — preventing the "blocks
        // appearing one at a time" visual as channels fire up during execution.
        foreach (var ed in e.Edges)
        {
            var key = (ed.SourceBlock, ed.TargetBlock);
            if (!_state.Channels.ContainsKey(key))
            {
                _state.Channels[key] = new ChannelState
                {
                    SourceBlock    = ed.SourceBlock,
                    TargetBlock    = ed.TargetBlock,
                    BufferCapacity = ed.BufferCapacity ?? 0,
                    IsCompeting    = string.Equals(ed.EdgeType, "Competing", StringComparison.OrdinalIgnoreCase)
                };
            }
        }
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
            blockState.ProductionRatePerSecond = 0; // block finished — no more production
            blockState.InputRatePerSecond = 0;
        }

        // Reset transmit rates for all edges originating from this block.
        foreach (var key in _state.Edges.Keys.Where(k => k.Source == e.BlockName).ToList())
            _state.Edges[key].TransmitRatePerSecond = 0;

        // A completed block has fully drained its input channels. Zero the counts now
        // so the buffer pills clear immediately rather than waiting for a ChannelStatsEvent
        // that may never arrive (the last event often fires before the final drain).
        foreach (var key in _state.Channels.Keys.Where(k => k.Target == e.BlockName).ToList())
            _state.Channels[key].CurrentCount = 0;
    }

    private void ProcessBlockMetrics(BlockMetricsEvent e)
    {
        if (_state.Blocks.TryGetValue(e.BlockName, out var blockState))
        {
            // Compute rates from deltas since the previous progress tick.
            if (blockState.LastProgressTimestamp.HasValue)
            {
                var elapsed = (e.Timestamp - blockState.LastProgressTimestamp.Value).TotalSeconds;
                if (elapsed > 0)
                {
                    blockState.ProductionRatePerSecond =
                        (e.ItemsProduced - blockState.PreviousItemsProducedForRate) / elapsed;
                    blockState.InputRatePerSecond =
                        (e.ItemsConsumed - blockState.PreviousItemsConsumedForRate) / elapsed;
                }
            }
            blockState.PreviousItemsProducedForRate = e.ItemsProduced;
            blockState.PreviousItemsConsumedForRate = e.ItemsConsumed;
            blockState.LastProgressTimestamp = e.Timestamp;
            blockState.ItemsProduced = e.ItemsProduced;
            blockState.ItemsConsumed = e.ItemsConsumed;
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
                var rate = delta / elapsed;
                edgeState.TransmitRatePerSecond = rate;
                if (rate > 0)
                {
                    edgeState.MaxTransmitRatePerSecond = Math.Max(edgeState.MaxTransmitRatePerSecond, rate);
                    edgeState.MinTransmitRatePerSecond = Math.Min(edgeState.MinTransmitRatePerSecond, rate);
                    edgeState._rateSampleSum += rate;
                    edgeState._rateSampleCount++;
                }
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
