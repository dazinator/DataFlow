// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Metrics;

/// <summary>
/// Represents a lease for monitoring a channel. When disposed, the channel is unregistered from monitoring.
/// </summary>
public interface IChannelMonitoringLease : IDisposable
{
    /// <summary>
    /// The monitored channel associated with this lease
    /// </summary>
    IMonitoredChannel Channel { get; }
}
