// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Metrics;

using System.Diagnostics;

public interface IDataFlowMetrics
{
    void RegisterChannel(IMonitoredChannel channel);
    // void FlowStarted(string flowName);
    // void FlowCompleted(double durationTotalMs, string name, IDataFlowContext context, bool successful);
    
    void FlowStarted(DataFlowMetricsContext context);
    void FlowCompleted(DataFlowMetricsContext metricsContext, double durationMs, bool success);

    void BlockStarted(BlockMetricsContext metricsContext);
    void BlockCompleted(BlockMetricsContext metricsContext, double durationTotalMs, bool success);

    //void BlockItemProcessed(string flowName, string blockName);                                              // Automatic: +1 per stream item
    //void RecordDataItemsProcessed(string flowName, string blockName, string dataType, long count);
    /// <summary>
    /// Tags that will be appended to all metrics and activities.
    /// </summary>
    public TagList GlobalTags { get; }
}
