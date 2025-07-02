// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Metrics;

using System.Diagnostics;

public interface IDataFlowMetrics
{
    //void RegisterChannel(IMonitoredChannel channel);
    /// <summary>
    /// Registers a channel for monitoring and returns a lease that should be disposed when monitoring is no longer needed
    /// </summary>
    IChannelMonitoringLease RegisterChannel(IMonitoredChannel channel);
    // void FlowStarted(string flowName);
    // void FlowCompleted(double durationTotalMs, string name, IDataFlowContext context, bool successful);

    void FlowStarted(DataFlowMetricsTagsContext context);
    void FlowCompleted(DataFlowMetricsTagsContext metricsContext, double durationMs);

    void BlockStarted(BlockMetricsTagsContext metricsContext);
    void BlockCompleted(BlockMetricsTagsContext metricsContext, double durationTotalMs);
   
    void ItemsProcessed(DataItemMetricsContext context, long count);
    /// <summary>
    /// Tags that will be appended to all metrics and activities.
    /// </summary>
    public TagList GlobalTags { get; }
}
