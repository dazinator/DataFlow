// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Metrics;

public interface IDataFlowMetrics
{
    void RegisterChannel(IMonitoredChannel channel);

    void FlowCompleted(double durationTotalMs, string name, IDataFlowContext context);
}
