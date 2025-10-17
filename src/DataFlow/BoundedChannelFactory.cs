namespace Uniun.DataFlow;

using System.Diagnostics;
using System.Threading.Channels;
using Uniun.DataFlow.Metrics;

public class MonitoredChannelFactory : IBoundedChannelFactory
{
    private readonly IDataFlowMetrics _metrics;
    private const int DefaultCapacity = 100;

    public static BoundedChannelOptions DefaultOptions { get; set; } = GetBoundedChannelOptions(DefaultCapacity);

    public MonitoredChannelFactory(IDataFlowMetrics metrics)
    {
        _metrics = metrics;
    }

    private static BoundedChannelOptions GetBoundedChannelOptions(int? capacity = null)
    {
        var options = capacity is null ? DefaultOptions : new BoundedChannelOptions(capacity ?? DefaultCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            // AllowSynchronousContinuations - consider impact of this on channel priority and pulsing effects.
        };
        return options;
    }

    public MonitoredChannel<T> CreateMonitoredChannel<T>(string blockName, int? capacity = null)
    {
        var options = GetBoundedChannelOptions(capacity);
        var channel = Channel.CreateBounded<T>(options);

        //// Create an empty read-only dictionary if dimensions is null
        //var dimensions = context.Dimensions ??
        //    new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());        

        return new MonitoredChannel<T>(
            _metrics,
            channel,
            blockName,
            // new ReadOnlyDictionary<string, string>(dimensions),
            options.Capacity
        );

        // return Channel.CreateBounded<T>(new BoundedChannelOptions(capacity));
    }


}
