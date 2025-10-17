namespace Uniun.DataFlow;
using Uniun.DataFlow.Metrics;

public interface IBoundedChannelFactory
{
    MonitoredChannel<T> CreateMonitoredChannel<T>(string blockName, int? capacity);
}
