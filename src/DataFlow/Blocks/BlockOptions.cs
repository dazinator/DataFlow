// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks;

using System.Threading.Channels;

public class BlockOptions
{

    ///The maximum number of concurrent operations that a block will be able to spawn.
    ///Note: This is a limit, not a target. Not all blocks need multiple concurrent tasks, those that do will honour this maximum.
    public int MaxConcurrency { get; set; } = 1;
    // public BoundedChannelOptions? ChannelOptions { get; set; }
    /// <summary>
    /// For blocks that support it, the capacity of the block's output channel. if not set, <see cref="MonitoredChannelFactory.DefaultCapacity"/> will be used.
    /// </summary>
    public int? Capacity { get; set; } = null;
    // Whether the concurrent block operations should have their own scoped DI IServiceProvider to resolve services from.
    public bool UseSeperateScopes { get; set; } = true;

    /// <summary>
    /// Enable flow rate metrics (how fast the block processes stream operations)
    /// </summary>
    public bool EnableFlowRateMetrics { get; set; } = true;

    /// <summary>
    /// How many flow operations to sample per second for metrics
    /// </summary>
    public int FlowRateMetricsSamplesPerSecond { get; set; } = 1;
}


