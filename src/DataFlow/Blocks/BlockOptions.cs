// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks;

using System.Threading.Channels;

public class BlockOptions
{

    ///The maximum number of concurrent operations that a block will be able to spawn.
    ///Note: This is a limit, not a target. Not all blocks need multiple concurrent tasks, those that do will honour this maximum.
    public int MaxConcurrency { get; set; } = 1;
    public BoundedChannelOptions? ChannelOptions { get; set; }

    // Whether the concurrent block operations should have their own scoped DI IServiceProvider to resolve services from.
    public bool UseSeperateScopes { get; set; } = false;
}
