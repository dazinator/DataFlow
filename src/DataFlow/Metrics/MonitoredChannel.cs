namespace Uniun.DataFlow.Metrics;
using System.Threading.Channels;

// Wrapper for a channel that provides metrics
public class MonitoredChannel<T> : IMonitoredChannel
{
    private readonly Channel<T> _channel;
    private readonly string _blockName;
    private readonly IReadOnlyDictionary<string, string> _dimensions;
    private readonly int _capacity;
    private readonly bool _ownsChannel;

    // private readonly ReaderWriterLockSlim _registrationLock = new ReaderWriterLockSlim();
    private bool _isDisposed;


    public MonitoredChannel(
        Channel<T> channel,
        string blockName,
        IReadOnlyDictionary<string, string> dimensions,
        int capacity,
        bool ownsChannel = false)
    {
        _channel = channel;
        _blockName = blockName;
        _dimensions = dimensions;
        _capacity = capacity;
        _ownsChannel = ownsChannel;
        if (!_channel.Reader.CanCount)
        {
            throw new ArgumentException("Channel must support counting for monitoring", nameof(channel));
        }

        // Register with metrics system
        DataFlowMetrics.RegisterChannel(this);
    }

    public ChannelMetricSnapshot GetMetricSnapshot()
    {
        // _registrationLock.EnterReadLock();
        //try
        //{
        if (_isDisposed)
        {
            return null;
        }

        // Create a copy of current dimensions and add block-specific ones
        var allDimensions = new Dictionary<string, string>(_dimensions)
        {
            ["block.name"] = _blockName,
            ["channel.capacity"] = _capacity.ToString()
        };

        return new ChannelMetricSnapshot(
            GetCurrentCount(),
            _capacity,
            allDimensions
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

    public void Dispose()
    {
        _isDisposed = true;

        if (_ownsChannel)
        {
            _channel.Writer.TryComplete();
        }

        // _registrationLock.Dispose();
    }

    // Channel operations delegated to inner channel
    // public ChannelReader<T> Reader => _channel.Reader;
    public ChannelWriter<T> Writer => _channel.Writer;
}
