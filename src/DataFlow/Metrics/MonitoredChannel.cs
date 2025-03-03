namespace Uniun.DataFlow.Metrics;

using System.Diagnostics;
using System.Threading.Channels;

// Wrapper for a channel that provides metrics
public class MonitoredChannel<T> : IMonitoredChannel
{
    private readonly IDataFlowMetrics _metrics;
    private readonly Channel<T> _channel;
    private readonly string _blockName;
    private TagList _tags;


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
        //_additionalTags = additionalTags;
        //_dimensions = dimensions;
        Capacity = capacity;
        //_ownsChannel = ownsChannel;
        if (!_channel.Reader.CanCount)
        {
            throw new ArgumentException("Channel must support counting for monitoring", nameof(channel));
        }
        // Or initialize with initial tags
       

        // Register with metrics system
        _metrics.RegisterChannel(this);
    }

    public void StartMonitoring(IDataFlowContext context)
    {       
        _tags = new TagList
        {
            { DataFlowMetrics.TagNames.BlockName, _blockName },
            { DataFlowMetrics.TagNames.ChannelCapacity, Capacity.ToString() },
            { DataFlowMetrics.TagNames.FlowName, context.Name },
            { DataFlowMetrics.TagNames.FlowInvocationId, context.InvocationId }
        };

        //foreach (var item in context.CustomTags)
        //{
        //    _tags.Add(item.Key, item.Value);
        //}
        _metrics.RegisterChannel(this);
       
    }

    public ChannelMetricSnapshot? GetMetricSnapshot()
    {
        // _registrationLock.EnterReadLock();
        //try
        //{
        //if (_isDisposed)
        //{
        //    return null;
        //}

        // Create a copy of current dimensions and add block-specific ones
        //var allDimensions = new Dictionary<string, string>(_dimensions)
        //{
        //    ["block.name"] = _blockName,
        //    ["channel.capacity"] = _capacity.ToString()
        //};

        return new ChannelMetricSnapshot(
            GetCurrentCount(),
            Capacity,
            _tags
        );
        //}
        //finally
        //{
        //    _registrationLock.ExitReadLock();
        //}
    }

    private int GetCurrentCount()
    {
        // We've already validated in the constructor that counting is supported
        return _channel.Reader.Count;
    }

    //public void Dispose()
    //{
    //    _isDisposed = true;

    //    if (_ownsChannel)
    //    {
    //        _channel.Writer.TryComplete();
    //    }

    //    // _registrationLock.Dispose();
    //}

    // Channel operations delegated to inner channel
    public ChannelReader<T> Reader => _channel.Reader;
    public ChannelWriter<T> Writer => _channel.Writer;

    public int Capacity { get; }
}
