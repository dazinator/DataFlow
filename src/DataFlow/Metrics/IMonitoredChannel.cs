namespace Uniun.DataFlow.Metrics;
using System;

public interface IMonitoredChannel  //: IDisposable
{
    // Method called by metrics collector to get current metrics
    ChannelMetricSnapshot GetMetricSnapshot();
}
