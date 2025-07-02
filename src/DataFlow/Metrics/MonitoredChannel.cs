namespace Uniun.DataFlow.Metrics;

using System.Diagnostics;
using System.Threading.Channels;

public class MonitoredChannel<T> : IMonitoredChannel
{
    private readonly IDataFlowMetrics _metrics;
    private readonly Channel<T> _channel;
    private readonly string _blockName;
    private TagList _tags;
    private bool isMonitoringStarted = false;

    public MonitoredChannel(      
        IDataFlowMetrics metrics,
        Channel<T> channel,
        string blockName,
        // IReadOnlyDictionary<string, string> dimensions,
        int capacity)
    {
        _metrics = metrics;
        _channel = channel;
        _blockName = blockName;
        Capacity = capacity;
        if (!_channel.Reader.CanCount)
        {
            throw new ArgumentException("Channel must support counting for monitoring", nameof(channel));
        }      

        // Register with metrics system
        _metrics.RegisterChannel(this);       
    }  

    /// <summary>
    /// Starts monitoring this channel and returns a lease that should be disposed when monitoring is no longer needed
    /// </summary>
    /// <param name="context">The data flow context containing flow information</param>
    /// <returns>A disposable lease that unregisters the channel when disposed</returns>
    public IChannelMonitoringLease StartMonitoring(IDataFlowContext context)
    {
        isMonitoringStarted = true;
        _tags = new TagList();       
        // Add global tags first
        foreach (var tag in _metrics.GlobalTags)
        {
            _tags.Add(tag.Key, tag.Value);
        }

        // Add channel-specific tags
        _tags.Add(DataFlowMetrics.TagNames.BlockName, _blockName);
        _tags.Add(DataFlowMetrics.TagNames.ChannelCapacity, Capacity.ToString());
        _tags.Add(DataFlowMetrics.TagNames.FlowName, context.Name);
        _tags.Add(DataFlowMetrics.TagNames.FlowInvocationId, context.InvocationId);
       
        // Register with metrics - return lease
        return _metrics.RegisterChannel(this); 
    }

    public ChannelMetricSnapshot? GetMetricSnapshot()
    {
        // Only return snapshot if monitoring has been started (tags exist)
        if (!isMonitoringStarted)
        {
            return null;
        }

        return new ChannelMetricSnapshot(
            GetCurrentCount(),
            Capacity,
            _tags.ToArray()
        );
    }

    private int GetCurrentCount()
    {
        // We've already validated in the constructor that counting is supported
        return _channel.Reader.Count;
    }

    // Channel operations delegated to inner channel
    public ChannelReader<T> Reader => _channel.Reader;
    public ChannelWriter<T> Writer => _channel.Writer;

    public int Capacity { get; }
}
