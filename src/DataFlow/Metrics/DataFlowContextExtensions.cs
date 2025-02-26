namespace Uniun.DataFlow.Metrics;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Channels;

// Extension method for DataFlowContext
public static class DataFlowContextExtensions
{
    public static MonitoredChannel<T> CreateMonitoredChannel<T>(
     this IDataFlowContext context,
     string blockName,
     int capacity = 100)
    {
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        // Create an empty read-only dictionary if dimensions is null
        var dimensions = context.Dimensions ??
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        return new MonitoredChannel<T>(
            channel,
            blockName,
            new ReadOnlyDictionary<string, string>(dimensions),
            capacity
        );
    }

    public static MonitoredChannel<T> CreateMonitoredChannel<T>(this IBlock block,
        BoundedChannelOptions options,
    IDataFlowContext context,
    Channel<T> channel)
    {      

        // Create an empty read-only dictionary if dimensions is null
        var dimensions = context.Dimensions ??
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        return new MonitoredChannel<T>(
            channel,
            block.Name,
            new ReadOnlyDictionary<string, string>(dimensions),
            options.Capacity
        );
    }

    public static Activity AddDataFlowContextDimensions(this Activity activity, IDataFlowContext context)
    {
        foreach (var dimension in context.Dimensions)
        {
            activity?.SetTag(dimension.Key, dimension.Value);
        }
        return activity;
    }   
}
