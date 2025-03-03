// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Metrics;

using System.Diagnostics;

public interface IDataFlowMetrics
{
    void RegisterChannel(IMonitoredChannel channel);

    void FlowCompleted(double durationTotalMs, string name, IDataFlowContext context, bool successful);
    void BlockCompleted(double durationTotalMs, string flowName, string blockName, IDataFlowContext context, bool successful);
    /// <summary>
    /// Tags that will be appended to all metrics and activities.
    /// </summary>
    public TagList GlobalTags { get; }
}
